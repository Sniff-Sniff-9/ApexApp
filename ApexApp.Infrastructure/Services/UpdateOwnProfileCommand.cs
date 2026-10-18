namespace ApexApp.Infrastructure.Services;

public sealed class UpdateOwnProfileCommand
{
    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? MiddleName { get; init; }

    public string? AvatarUrl { get; init; }
}
