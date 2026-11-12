using ApexApp.Infrastructure.Services;

namespace ApexApp.Api.DTOs;

/// <summary>
/// Position data returned by the organization endpoints.
/// </summary>
public sealed record PositionDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    int EmployeeCount)
{
    public static PositionDto From(PositionSummary summary) =>
        new(
            summary.Position.Id,
            summary.Position.Name,
            summary.Position.Description,
            summary.Position.IsActive,
            summary.EmployeeCount);
}

/// <summary>
/// Request body for creating a position.
/// </summary>
public sealed record CreatePositionRequest(
    string? Name,
    string? Description,
    bool IsActive = true);

/// <summary>
/// Request body for updating a position.
/// </summary>
public sealed record UpdatePositionRequest(
    string? Name,
    string? Description,
    bool IsActive = true);
