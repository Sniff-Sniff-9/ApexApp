using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Department administration backed by the existing <c>departments</c> table.
/// </summary>
public sealed class DepartmentService(AppDbContext dbContext) : IDepartmentService
{
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 1000;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentSummary>> ListAsync(
        bool includeInactive,
        int? parentDepartmentId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Departments.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(department => department.IsActive);
        }

        if (parentDepartmentId is int parentId)
        {
            query = query.Where(department => department.ParentDepartmentId == parentId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(department => department.Name.ToLower().Contains(term));
        }

        var departments = await query
            .OrderBy(department => department.Name)
            .ThenBy(department => department.Id)
            .ToListAsync(cancellationToken);

        return await BuildSummariesAsync(departments, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DepartmentSummary?> GetAsync(
        int departmentId,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            return null;
        }

        var department = await dbContext.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == departmentId, cancellationToken);

        if (department is null)
        {
            return null;
        }

        var summaries = await BuildSummariesAsync([department], cancellationToken);

        return summaries[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DepartmentTreeNode>> GetTreeAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Departments.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(department => department.IsActive);
        }

        var departments = await query
            .OrderBy(department => department.Name)
            .ThenBy(department => department.Id)
            .ToListAsync(cancellationToken);

        var summaries = await BuildSummariesAsync(departments, cancellationToken);
        var summariesById = summaries.ToDictionary(item => item.Department.Id);

        var childrenByParent = departments
            .Where(department => department.ParentDepartmentId is not null)
            .GroupBy(department => department.ParentDepartmentId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Id).ToList());

        DepartmentTreeNode Build(int departmentId)
        {
            var summary = summariesById[departmentId];
            var children = childrenByParent.TryGetValue(departmentId, out var childIds)
                ? childIds.Select(Build).ToList()
                : [];

            return new DepartmentTreeNode(
                summary.Department,
                summary.EmployeeCount,
                summary.ChildDepartmentCount,
                children);
        }

        var includedIds = departments.Select(department => department.Id).ToHashSet();

        var rootIds = departments
            .Where(department => department.ParentDepartmentId is null
                                 || !includedIds.Contains(department.ParentDepartmentId.Value))
            .Select(department => department.Id)
            .ToList();

        return rootIds.Select(Build).ToList();
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Department>> CreateAsync(
        DepartmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateCommand(command);

        if (validationError is not null)
        {
            return ServiceResult<Department>.Fail(400, validationError);
        }

        var name = command.Name!.Trim();
        var duplicate = await dbContext.Departments
            .AsNoTracking()
            .AnyAsync(
                department => department.Name.ToLower() == name.ToLower(),
                cancellationToken);

        if (duplicate)
        {
            return ServiceResult<Department>.Fail(409, "A department with this name already exists.");
        }

        var parentError = await ValidateParentAsync(
            departmentId: null,
            command.ParentDepartmentId,
            cancellationToken);

        if (parentError is not null)
        {
            return ServiceResult<Department>.Fail(400, parentError);
        }

        var entity = new Department
        {
            Name = name,
            Description = NormalizeOptional(command.Description),
            ParentDepartmentId = command.ParentDepartmentId,
            IsActive = command.IsActive
        };

        dbContext.Departments.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<Department>.Ok(entity);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Department>> UpdateAsync(
        int departmentId,
        DepartmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            return ServiceResult<Department>.Fail(400, "A valid department ID is required.");
        }

        var validationError = ValidateCommand(command);

        if (validationError is not null)
        {
            return ServiceResult<Department>.Fail(400, validationError);
        }

        var department = await dbContext.Departments
            .FirstOrDefaultAsync(item => item.Id == departmentId, cancellationToken);

        if (department is null)
        {
            return ServiceResult<Department>.Fail(404, "Department was not found.");
        }

        var name = command.Name!.Trim();
        var duplicate = await dbContext.Departments
            .AsNoTracking()
            .AnyAsync(
                item => item.Id != departmentId
                        && item.Name.ToLower() == name.ToLower(),
                cancellationToken);

        if (duplicate)
        {
            return ServiceResult<Department>.Fail(409, "A department with this name already exists.");
        }

        var parentError = await ValidateParentAsync(
            departmentId,
            command.ParentDepartmentId,
            cancellationToken);

        if (parentError is not null)
        {
            return ServiceResult<Department>.Fail(400, parentError);
        }

        if (!command.IsActive && department.IsActive)
        {
            var archiveError = await ValidateArchiveAsync(departmentId, cancellationToken);

            if (archiveError is not null)
            {
                return ServiceResult<Department>.Fail(409, archiveError);
            }
        }

        department.Name = name;
        department.Description = NormalizeOptional(command.Description);
        department.ParentDepartmentId = command.ParentDepartmentId;
        department.IsActive = command.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<Department>.Ok(department);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Department>> ArchiveAsync(
        int departmentId,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            return ServiceResult<Department>.Fail(400, "A valid department ID is required.");
        }

        var department = await dbContext.Departments
            .FirstOrDefaultAsync(item => item.Id == departmentId, cancellationToken);

        if (department is null)
        {
            return ServiceResult<Department>.Fail(404, "Department was not found.");
        }

        if (!department.IsActive)
        {
            return ServiceResult<Department>.Ok(department);
        }

        var archiveError = await ValidateArchiveAsync(departmentId, cancellationToken);

        if (archiveError is not null)
        {
            return ServiceResult<Department>.Fail(409, archiveError);
        }

        department.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<Department>.Ok(department);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Department>> MoveAsync(
        int departmentId,
        int? parentDepartmentId,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            return ServiceResult<Department>.Fail(400, "A valid department ID is required.");
        }

        var department = await dbContext.Departments
            .FirstOrDefaultAsync(item => item.Id == departmentId, cancellationToken);

        if (department is null)
        {
            return ServiceResult<Department>.Fail(404, "Department was not found.");
        }

        var parentError = await ValidateParentAsync(
            departmentId,
            parentDepartmentId,
            cancellationToken);

        if (parentError is not null)
        {
            return ServiceResult<Department>.Fail(400, parentError);
        }

        department.ParentDepartmentId = parentDepartmentId;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<Department>.Ok(department);
    }

    private async Task<IReadOnlyList<DepartmentSummary>> BuildSummariesAsync(
        IReadOnlyList<Department> departments,
        CancellationToken cancellationToken)
    {
        if (departments.Count == 0)
        {
            return [];
        }

        var departmentIds = departments.Select(department => department.Id).ToList();

        var employeeCounts = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive
                           && user.DepartmentId != null
                           && departmentIds.Contains(user.DepartmentId.Value))
            .GroupBy(user => user.DepartmentId!.Value)
            .Select(group => new { DepartmentId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.DepartmentId, row => row.Count, cancellationToken);

        var childCounts = await dbContext.Departments
            .AsNoTracking()
            .Where(department => department.IsActive
                                 && department.ParentDepartmentId != null
                                 && departmentIds.Contains(department.ParentDepartmentId.Value))
            .GroupBy(department => department.ParentDepartmentId!.Value)
            .Select(group => new { DepartmentId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.DepartmentId, row => row.Count, cancellationToken);

        return departments
            .Select(department => new DepartmentSummary(
                department,
                employeeCounts.GetValueOrDefault(department.Id),
                childCounts.GetValueOrDefault(department.Id)))
            .ToList();
    }

    private async Task<string?> ValidateArchiveAsync(
        int departmentId,
        CancellationToken cancellationToken)
    {
        var hasActiveChildren = await dbContext.Departments
            .AsNoTracking()
            .AnyAsync(
                department => department.ParentDepartmentId == departmentId
                              && department.IsActive,
                cancellationToken);

        if (hasActiveChildren)
        {
            return "The department has active child departments. Move or archive them first.";
        }

        var hasActiveEmployees = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.DepartmentId == departmentId && user.IsActive,
                cancellationToken);

        return hasActiveEmployees
            ? "The department has active employees. Transfer them before archiving the department."
            : null;
    }

    private async Task<string?> ValidateParentAsync(
        int? departmentId,
        int? parentDepartmentId,
        CancellationToken cancellationToken)
    {
        if (parentDepartmentId is null)
        {
            return null;
        }

        if (parentDepartmentId.Value <= 0)
        {
            return "A valid parent department ID is required.";
        }

        if (departmentId is int currentId && currentId == parentDepartmentId.Value)
        {
            return "A department cannot be its own parent.";
        }

        var parent = await dbContext.Departments
            .AsNoTracking()
            .Where(item => item.Id == parentDepartmentId.Value)
            .Select(item => new { item.Id, item.ParentDepartmentId })
            .FirstOrDefaultAsync(cancellationToken);

        if (parent is null)
        {
            return "Parent department was not found.";
        }

        if (departmentId is not int id)
        {
            return null;
        }

        // Walk up from the proposed parent. Reaching the department itself means a cycle.
        var visited = new HashSet<int>();
        var cursor = parent;

        while (cursor.ParentDepartmentId is int nextParentId)
        {
            if (!visited.Add(nextParentId))
            {
                return "The department hierarchy contains a cycle.";
            }

            if (nextParentId == id)
            {
                return "A department cannot be moved under one of its descendants.";
            }

            cursor = await dbContext.Departments
                .AsNoTracking()
                .Where(item => item.Id == nextParentId)
                .Select(item => new { item.Id, item.ParentDepartmentId })
                .FirstOrDefaultAsync(cancellationToken);

            if (cursor is null)
            {
                break;
            }
        }

        return null;
    }

    private static string? ValidateCommand(DepartmentCommand command)
    {
        if (command is null)
        {
            return "A department command is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return "Department name is required.";
        }

        if (command.Name.Trim().Length > NameMaxLength)
        {
            return $"Department name must not exceed {NameMaxLength} characters.";
        }

        if (!string.IsNullOrWhiteSpace(command.Description)
            && command.Description.Trim().Length > DescriptionMaxLength)
        {
            return $"Description must not exceed {DescriptionMaxLength} characters.";
        }

        return null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
