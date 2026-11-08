namespace ApexApp.Api.DTOs;

public sealed class AdminUpdateUserRequest
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? MiddleName { get; set; }

    public string? AvatarUrl { get; set; }

    public int? DepartmentId { get; set; }

    public int? PositionId { get; set; }

    public bool? IsActive { get; set; }
}
