namespace ApexApp.Infrastructure.Services;

public sealed class AdminUpdateUserCommand
{
    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? MiddleName { get; init; }

    public string? AvatarUrl { get; init; }

    public string? Department { get; init; }

    public string? Position { get; init; }

    public bool? IsActive { get; init; }
}
