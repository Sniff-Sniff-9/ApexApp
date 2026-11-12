using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Position administration backed by the existing <c>positions</c> table.
/// </summary>
public sealed class PositionService(AppDbContext dbContext) : IPositionService
{
    private const int NameMaxLength = 200;
    private const int DescriptionMaxLength = 1000;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PositionSummary>> ListAsync(
        bool includeInactive,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Positions.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(position => position.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(position => position.Name.ToLower().Contains(term));
        }

        var positions = await query
            .OrderBy(position => position.Name)
            .ThenBy(position => position.Id)
            .ToListAsync(cancellationToken);

        return await BuildSummariesAsync(positions, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PositionSummary?> GetAsync(
        int positionId,
        CancellationToken cancellationToken = default)
    {
        if (positionId <= 0)
        {
            return null;
        }

        var position = await dbContext.Positions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == positionId, cancellationToken);

        if (position is null)
        {
            return null;
        }

        var summaries = await BuildSummariesAsync([position], cancellationToken);

        return summaries[0];
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Position>> CreateAsync(
        PositionCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateCommand(command);

        if (validationError is not null)
        {
            return ServiceResult<Position>.Fail(400, validationError);
        }

        var name = command.Name!.Trim();
        var duplicate = await dbContext.Positions
            .AsNoTracking()
            .AnyAsync(
                position => position.Name.ToLower() == name.ToLower(),
                cancellationToken);

        if (duplicate)
        {
            return ServiceResult<Position>.Fail(409, "A position with this name already exists.");
        }

        var entity = new Position
        {
            Name = name,
            Description = NormalizeOptional(command.Description),
            IsActive = command.IsActive
        };

        dbContext.Positions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<Position>.Ok(entity);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Position>> UpdateAsync(
        int positionId,
        PositionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (positionId <= 0)
        {
            return ServiceResult<Position>.Fail(400, "A valid position ID is required.");
        }

        var validationError = ValidateCommand(command);

        if (validationError is not null)
        {
            return ServiceResult<Position>.Fail(400, validationError);
        }

        var position = await dbContext.Positions
            .FirstOrDefaultAsync(item => item.Id == positionId, cancellationToken);

        if (position is null)
        {
            return ServiceResult<Position>.Fail(404, "Position was not found.");
        }

        var name = command.Name!.Trim();
        var duplicate = await dbContext.Positions
            .AsNoTracking()
            .AnyAsync(
                item => item.Id != positionId
                        && item.Name.ToLower() == name.ToLower(),
                cancellationToken);

        if (duplicate)
        {
            return ServiceResult<Position>.Fail(409, "A position with this name already exists.");
        }

        if (!command.IsActive && position.IsActive)
        {
            var hasActiveEmployees = await dbContext.Users
                .AsNoTracking()
                .AnyAsync(
                    user => user.PositionId == positionId && user.IsActive,
                    cancellationToken);

            if (hasActiveEmployees)
            {
                return ServiceResult<Position>.Fail(
                    409,
                    "The position has active employees. Reassign them before archiving the position.");
            }
        }

        position.Name = name;
        position.Description = NormalizeOptional(command.Description);
        position.IsActive = command.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<Position>.Ok(position);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Position>> ArchiveAsync(
        int positionId,
        CancellationToken cancellationToken = default)
    {
        if (positionId <= 0)
        {
            return ServiceResult<Position>.Fail(400, "A valid position ID is required.");
        }

        var position = await dbContext.Positions
            .FirstOrDefaultAsync(item => item.Id == positionId, cancellationToken);

        if (position is null)
        {
            return ServiceResult<Position>.Fail(404, "Position was not found.");
        }

        if (!position.IsActive)
        {
            return ServiceResult<Position>.Ok(position);
        }

        var hasActiveEmployees = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.PositionId == positionId && user.IsActive,
                cancellationToken);

        if (hasActiveEmployees)
        {
            return ServiceResult<Position>.Fail(
                409,
                "The position has active employees. Reassign them before archiving the position.");
        }

        position.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<Position>.Ok(position);
    }

    private async Task<IReadOnlyList<PositionSummary>> BuildSummariesAsync(
        IReadOnlyList<Position> positions,
        CancellationToken cancellationToken)
    {
        if (positions.Count == 0)
        {
            return [];
        }

        var positionIds = positions.Select(position => position.Id).ToList();

        var employeeCounts = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive
                           && user.PositionId != null
                           && positionIds.Contains(user.PositionId.Value))
            .GroupBy(user => user.PositionId!.Value)
            .Select(group => new { PositionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.PositionId, row => row.Count, cancellationToken);

        return positions
            .Select(position => new PositionSummary(
                position,
                employeeCounts.GetValueOrDefault(position.Id)))
            .ToList();
    }

    private static string? ValidateCommand(PositionCommand command)
    {
        if (command is null)
        {
            return "A position command is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return "Position name is required.";
        }

        if (command.Name.Trim().Length > NameMaxLength)
        {
            return $"Position name must not exceed {NameMaxLength} characters.";
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
