namespace ApexApp.Infrastructure.Services;

public sealed class AdminUpdateUserCommand
{
    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? MiddleName { get; init; }

    public string? AvatarUrl { get; init; }

    public int? DepartmentId { get; init; }

    public int? PositionId { get; init; }

    public bool? IsActive { get; init; }
}
