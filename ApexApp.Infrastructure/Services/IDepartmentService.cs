using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Administrative scenarios for the organizational structure: departments, their
/// hierarchy, and the employees assigned to them.
/// </summary>
public interface IDepartmentService
{
    /// <summary>
    /// Returns departments ordered by name, optionally filtered by parent and search text.
    /// </summary>
    Task<IReadOnlyList<DepartmentSummary>> ListAsync(
        bool includeInactive,
        int? parentDepartmentId,
        string? search,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single department with employee and child counts.
    /// </summary>
    Task<DepartmentSummary?> GetAsync(
        int departmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the department hierarchy. Departments whose parent is filtered out become roots.
    /// </summary>
    Task<IReadOnlyList<DepartmentTreeNode>> GetTreeAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a department.
    /// </summary>
    Task<ServiceResult<Department>> CreateAsync(
        DepartmentCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a department's name, description, parent, or active flag.
    /// </summary>
    Task<ServiceResult<Department>> UpdateAsync(
        int departmentId,
        DepartmentCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a department. Active children and active employees block archiving.
    /// </summary>
    Task<ServiceResult<Department>> ArchiveAsync(
        int departmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a department under another parent (or to the root when null).
    /// </summary>
    Task<ServiceResult<Department>> MoveAsync(
        int departmentId,
        int? parentDepartmentId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Data required to create or update a department.
/// </summary>
public sealed record DepartmentCommand(
    string? Name,
    string? Description,
    int? ParentDepartmentId,
    bool IsActive);

/// <summary>
/// A department together with the counts used by the admin list.
/// </summary>
public sealed record DepartmentSummary(
    Department Department,
    int EmployeeCount,
    int ChildDepartmentCount);

/// <summary>
/// A node of the department hierarchy.
/// </summary>
public sealed record DepartmentTreeNode(
    Department Department,
    int EmployeeCount,
    int ChildDepartmentCount,
    IReadOnlyList<DepartmentTreeNode> Children);
