namespace ApexApp.Infrastructure.Services;

public sealed record CreateEmployeeCommand
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? MiddleName { get; init; }
    public int? DepartmentId { get; init; }
    public int? PositionId { get; init; }
}
