using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Api.Extensions;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Department administration and the organization hierarchy.
/// </summary>
[ApiController]
[Route("api/departments")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class DepartmentsController(
    IDepartmentService departmentService,
    IUserProfileService userProfileService,
    IAuthorizationService authorizationService) : ApiControllerBase
{
    /// <summary>
    /// Lists departments, optionally filtered by parent, search text, or active state.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetAll(
        [FromQuery] bool includeInactive = false,
        [FromQuery] int? parentId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var canSeeInactive = await CanManageOrganizationAsync();
        var departments = await departmentService.ListAsync(
            includeInactive && canSeeInactive,
            parentId,
            search,
            cancellationToken);

        return Ok(departments.Select(DepartmentDto.From).ToList());
    }

    /// <summary>
    /// Returns the department hierarchy.
    /// </summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(IReadOnlyList<DepartmentTreeNodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DepartmentTreeNodeDto>>> GetTree(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var canSeeInactive = await CanManageOrganizationAsync();
        var nodes = await departmentService.GetTreeAsync(
            includeInactive && canSeeInactive,
            cancellationToken);

        return Ok(nodes.Select(DepartmentTreeNodeDto.From).ToList());
    }

    /// <summary>
    /// Returns one department.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var department = await departmentService.GetAsync(id, cancellationToken);

        return department is null
            ? NotFound(new { message = "Department was not found." })
            : Ok(DepartmentDto.From(department));
    }

    /// <summary>
    /// Lists employees assigned to a department.
    /// </summary>
    [HttpGet("{id:int}/employees")]
    [Authorize(Policy = AppPolicies.ViewTeam)]
    [ProducesResponseType(typeof(IReadOnlyList<CurrentUserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CurrentUserProfileDto>>> GetEmployees(
        int id,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var department = await departmentService.GetAsync(id, cancellationToken);

        if (department is null)
        {
            return NotFound(new { message = "Department was not found." });
        }

        var canManageOrganization = await authorizationService.AuthorizeAsync(
            User,
            AppPolicies.ManageOrg);

        // Managers may view their own team only; HR, admins, and superadmins may view any department.
        if (!canManageOrganization.Succeeded)
        {
            var callerId = User.GetUserId();

            if (callerId is null)
            {
                return Unauthorized();
            }

            var callerDepartmentId = await userProfileService.GetDepartmentIdAsync(
                callerId.Value,
                cancellationToken);

            if (callerDepartmentId != id)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "Managers can only view employees of their own department." });
            }
        }

        var canManageUsers = await authorizationService.AuthorizeAsync(
            User,
            AppPolicies.ManageUsers);

        var profiles = await userProfileService.GetByDepartmentIdAsync(
            id,
            includeInactive && canManageUsers.Succeeded,
            cancellationToken);

        return Ok(profiles.Select(CurrentUserProfileDto.From).ToList());
    }

    /// <summary>
    /// Creates a department.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.ManageOrg)]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartmentDto>> Create(
        [FromBody] CreateDepartmentRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "A request body is required." });
        }

        var result = await departmentService.CreateAsync(
            new DepartmentCommand(
                request.Name,
                request.Description,
                request.ParentDepartmentId,
                request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var summary = await departmentService.GetAsync(result.Value!.Id, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value.Id },
            DepartmentDto.From(summary!));
    }

    /// <summary>
    /// Updates a department.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AppPolicies.ManageOrg)]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartmentDto>> Update(
        int id,
        [FromBody] UpdateDepartmentRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "A request body is required." });
        }

        var result = await departmentService.UpdateAsync(
            id,
            new DepartmentCommand(
                request.Name,
                request.Description,
                request.ParentDepartmentId,
                request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var summary = await departmentService.GetAsync(id, cancellationToken);

        return Ok(DepartmentDto.From(summary!));
    }

    /// <summary>
    /// Archives a department. Active children or active employees block the operation.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AppPolicies.ManageOrg)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Archive(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await departmentService.ArchiveAsync(id, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : ToErrorResult(result.StatusCode, result.Error);
    }

    /// <summary>
    /// Moves a department under another parent, or to the root when the parent is null.
    /// </summary>
    [HttpPut("{id:int}/parent")]
    [Authorize(Policy = AppPolicies.ManageOrg)]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDto>> Move(
        int id,
        [FromBody] MoveDepartmentRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "A request body is required." });
        }

        var result = await departmentService.MoveAsync(
            id,
            request.ParentDepartmentId,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var summary = await departmentService.GetAsync(id, cancellationToken);

        return Ok(DepartmentDto.From(summary!));
    }

    private async Task<bool> CanManageOrganizationAsync()
    {
        var result = await authorizationService.AuthorizeAsync(User, AppPolicies.ManageOrg);

        return result.Succeeded;
    }
}
