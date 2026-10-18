using ApexApp.Infrastructure.Services;

namespace ApexApp.Api.DTOs;

public sealed class CurrentUserProfileDto
{
    public required Guid Id { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public string? MiddleName { get; init; }

    public string? AvatarUrl { get; init; }

    public required string Department { get; init; }

    public required string Position { get; init; }

    public required bool IsActive { get; init; }

    public DateTime? LastLoginAt { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required DateTime UpdatedAt { get; init; }

    public required IReadOnlyList<string> Roles { get; init; }

    public required bool CanAccessAdminPanel { get; init; }

    public static CurrentUserProfileDto From(CurrentUserProfile profile)
    {
        return new CurrentUserProfileDto
        {
            Id = profile.Id,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            MiddleName = profile.MiddleName,
            AvatarUrl = profile.AvatarUrl,
            Department = profile.Department,
            Position = profile.Position,
            IsActive = profile.IsActive,
            LastLoginAt = profile.LastLoginAt,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            Roles = profile.Roles,
            CanAccessAdminPanel = profile.CanAccessAdminPanel
        };
    }
}
