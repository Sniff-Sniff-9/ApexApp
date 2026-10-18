using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Api.Extensions;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

[Authorize]
[Route("api/users")]
public sealed class UsersController : ApiControllerBase
{
    private readonly IUserProfileService _userProfileService;
    private readonly IRoleManagementService _roleManagementService;
    private readonly IAuthorizationService _authorizationService;

    public UsersController(
        IUserProfileService userProfileService,
        IRoleManagementService roleManagementService,
        IAuthorizationService authorizationService)
    {
        _userProfileService = userProfileService;
        _roleManagementService = roleManagementService;
        _authorizationService = authorizationService;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentUserProfileDto>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var profile = await _userProfileService.GetCurrentAsync(userId.Value, cancellationToken);
        if (profile is null)
        {
            return NotFound(new { message = "User profile was not found." });
        }

        return Ok(CurrentUserProfileDto.From(profile));
    }

    [Authorize(Policy = AppPolicies.ActiveUser)]
    [HttpPatch("me")]
    [ProducesResponseType(typeof(CurrentUserProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserProfileDto>> UpdateCurrentUser(
        [FromBody] UpdateOwnProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _userProfileService.UpdateOwnProfileAsync(
            userId.Value,
            new UpdateOwnProfileCommand
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                MiddleName = request.MiddleName,
                AvatarUrl = request.AvatarUrl
            },
            cancellationToken);

        return ToDto(result);
    }

    [Authorize(Policy = AppPolicies.ActiveUser)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CurrentUserProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CurrentUserProfileDto>>> ListUsers(
        CancellationToken cancellationToken)
    {
        var canManageUsers = await _authorizationService.AuthorizeAsync(User, AppPolicies.ManageUsers);
        var profiles = await _userProfileService.ListAsync(
            includeInactive: canManageUsers.Succeeded,
            cancellationToken);

        return Ok(profiles.Select(CurrentUserProfileDto.From).ToList());
    }

    [Authorize(Policy = AppPolicies.ActiveUser)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CurrentUserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentUserProfileDto>> GetUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        var canManageUsers = await _authorizationService.AuthorizeAsync(User, AppPolicies.ManageUsers);
        var profile = await _userProfileService.GetByIdAsync(
            id,
            includeInactive: canManageUsers.Succeeded,
            cancellationToken);

        if (profile is null)
        {
            return NotFound(new { message = "User profile was not found." });
        }

        return Ok(CurrentUserProfileDto.From(profile));
    }

    [Authorize(Policy = AppPolicies.ManageUsers)]
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(CurrentUserProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserProfileDto>> AdminUpdateUser(
        Guid id,
        [FromBody] AdminUpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _userProfileService.AdminUpdateAsync(
            id,
            new AdminUpdateUserCommand
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                MiddleName = request.MiddleName,
                AvatarUrl = request.AvatarUrl,
                Department = request.Department,
                Position = request.Position,
                IsActive = request.IsActive
            },
            cancellationToken);

        return ToDto(result);
    }

    [Authorize(Policy = AppPolicies.ManageRoles)]
    [HttpPut("{id:guid}/roles")]
    [ProducesResponseType(typeof(CurrentUserProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserProfileDto>> SetUserRoles(
        Guid id,
        [FromBody] SetUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _roleManagementService.SetUserRolesAsync(
            userId.Value,
            id,
            request.RoleCodes,
            cancellationToken);

        return ToDto(result);
    }

    private ActionResult<CurrentUserProfileDto> ToDto(Infrastructure.Common.ServiceResult<CurrentUserProfile> result)
    {
        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(CurrentUserProfileDto.From(result.Value!));
    }
}
