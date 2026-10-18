using ApexApp.Infrastructure.Auth;

namespace ApexApp.Infrastructure.Services;

public sealed class CurrentUserProfile
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

    public bool CanAccessAdminPanel => RoleCodes.CanAccessAdminPanel(Roles);
}
