using ApexApp.Infrastructure.Services;

namespace ApexApp.Api.DTOs;

/// <summary>
/// Department data returned by the organization endpoints.
/// </summary>
public sealed record DepartmentDto(
    int Id,
    string Name,
    string? Description,
    int? ParentDepartmentId,
    bool IsActive,
    int EmployeeCount,
    int ChildDepartmentCount)
{
    public static DepartmentDto From(DepartmentSummary summary) =>
        new(
            summary.Department.Id,
            summary.Department.Name,
            summary.Department.Description,
            summary.Department.ParentDepartmentId,
            summary.Department.IsActive,
            summary.EmployeeCount,
            summary.ChildDepartmentCount);
}

/// <summary>
/// A node of the department hierarchy.
/// </summary>
public sealed record DepartmentTreeNodeDto(
    int Id,
    string Name,
    string? Description,
    int? ParentDepartmentId,
    bool IsActive,
    int EmployeeCount,
    int ChildDepartmentCount,
    IReadOnlyList<DepartmentTreeNodeDto> Children)
{
    public static DepartmentTreeNodeDto From(DepartmentTreeNode node) =>
        new(
            node.Department.Id,
            node.Department.Name,
            node.Department.Description,
            node.Department.ParentDepartmentId,
            node.Department.IsActive,
            node.EmployeeCount,
            node.ChildDepartmentCount,
            node.Children.Select(From).ToList());
}

/// <summary>
/// Request body for creating a department.
/// </summary>
public sealed record CreateDepartmentRequest(
    string? Name,
    string? Description,
    int? ParentDepartmentId,
    bool IsActive = true);

/// <summary>
/// Request body for updating a department.
/// </summary>
public sealed record UpdateDepartmentRequest(
    string? Name,
    string? Description,
    int? ParentDepartmentId,
    bool IsActive = true);

/// <summary>
/// Request body for moving a department under another parent.
/// </summary>
public sealed record MoveDepartmentRequest(int? ParentDepartmentId);
