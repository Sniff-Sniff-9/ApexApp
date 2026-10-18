namespace ApexApp.Infrastructure.Services;

public sealed class RoleInfo
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }
}
