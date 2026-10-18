using ApexApp.Infrastructure.Services;

namespace ApexApp.Api.DTOs;

public sealed class RoleDto
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public static RoleDto From(RoleInfo role)
    {
        return new RoleDto
        {
            Id = role.Id,
            Code = role.Code,
            Name = role.Name,
            Description = role.Description
        };
    }
}
