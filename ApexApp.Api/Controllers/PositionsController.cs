using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Position (job title) administration.
/// </summary>
[ApiController]
[Route("api/positions")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class PositionsController(
    IPositionService positionService,
    IUserProfileService userProfileService,
    IAuthorizationService authorizationService) : ApiControllerBase
{
    /// <summary>
    /// Lists positions, optionally including archived positions.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PositionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PositionDto>>> GetAll(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var canSeeInactive = await CanManageOrganizationAsync();
        var positions = await positionService.ListAsync(
            includeInactive && canSeeInactive,
            search,
            cancellationToken);

        return Ok(positions.Select(PositionDto.From).ToList());
    }

    /// <summary>
    /// Returns one position.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var position = await positionService.GetAsync(id, cancellationToken);

        return position is null
            ? NotFound(new { message = "Position was not found." })
            : Ok(PositionDto.From(position));
    }

    /// <summary>
    /// Lists employees assigned to a position.
    /// </summary>
    [HttpGet("{id:int}/employees")]
    [Authorize(Policy = AppPolicies.ManageOrg)]
    [ProducesResponseType(typeof(IReadOnlyList<CurrentUserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CurrentUserProfileDto>>> GetEmployees(
        int id,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var position = await positionService.GetAsync(id, cancellationToken);

        if (position is null)
        {
            return NotFound(new { message = "Position was not found." });
        }

        var canManageUsers = await authorizationService.AuthorizeAsync(
            User,
            AppPolicies.ManageUsers);

        var profiles = await userProfileService.GetByPositionIdAsync(
            id,
            includeInactive && canManageUsers.Succeeded,
            cancellationToken);

        return Ok(profiles.Select(CurrentUserProfileDto.From).ToList());
    }

    /// <summary>
    /// Creates a position.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AppPolicies.ManageOrg)]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PositionDto>> Create(
        [FromBody] CreatePositionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "A request body is required." });
        }

        var result = await positionService.CreateAsync(
            new PositionCommand(
                request.Name,
                request.Description,
                request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var summary = await positionService.GetAsync(result.Value!.Id, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value.Id },
            PositionDto.From(summary!));
    }

    /// <summary>
    /// Updates a position.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AppPolicies.ManageOrg)]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PositionDto>> Update(
        int id,
        [FromBody] UpdatePositionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "A request body is required." });
        }

        var result = await positionService.UpdateAsync(
            id,
            new PositionCommand(
                request.Name,
                request.Description,
                request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var summary = await positionService.GetAsync(id, cancellationToken);

        return Ok(PositionDto.From(summary!));
    }

    /// <summary>
    /// Archives a position. Active employees assigned to it block the operation.
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
        var result = await positionService.ArchiveAsync(id, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : ToErrorResult(result.StatusCode, result.Error);
    }

    private async Task<bool> CanManageOrganizationAsync()
    {
        var result = await authorizationService.AuthorizeAsync(User, AppPolicies.ManageOrg);

        return result.Succeeded;
    }
}
