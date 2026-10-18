namespace ApexApp.Api.DTOs;

public sealed class UpdateOwnProfileRequest
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? MiddleName { get; set; }

    public string? AvatarUrl { get; set; }
}
