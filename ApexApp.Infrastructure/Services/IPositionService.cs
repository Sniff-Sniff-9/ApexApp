using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Administrative scenarios for employee positions (job titles).
/// </summary>
public interface IPositionService
{
    /// <summary>
    /// Returns positions ordered by name, optionally including archived positions.
    /// </summary>
    Task<IReadOnlyList<PositionSummary>> ListAsync(
        bool includeInactive,
        string? search,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single position with its employee count.
    /// </summary>
    Task<PositionSummary?> GetAsync(
        int positionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a position.
    /// </summary>
    Task<ServiceResult<Position>> CreateAsync(
        PositionCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a position's name, description, or active flag.
    /// </summary>
    Task<ServiceResult<Position>> UpdateAsync(
        int positionId,
        PositionCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a position. Active employees assigned to it block archiving.
    /// </summary>
    Task<ServiceResult<Position>> ArchiveAsync(
        int positionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Data required to create or update a position.
/// </summary>
public sealed record PositionCommand(
    string? Name,
    string? Description,
    bool IsActive);

/// <summary>
/// A position together with its active employee count.
/// </summary>
public sealed record PositionSummary(Position Position, int EmployeeCount);
