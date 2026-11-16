using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>Badge definitions, automatic settings and manual assignments.</summary>
[ApiController]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class ProductBadgesController(
    IProductBadgeService badgeService) : ApiControllerBase
{
    [HttpGet("api/badges")]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var badges = await badgeService.GetActiveAsync(cancellationToken);

        return Ok(badges.Select(ToDto));
    }

    [HttpGet("api/badges/admin")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var badges = await badgeService.GetAllAsync(cancellationToken);

        return Ok(badges.Select(ToDto));
    }

    [HttpPost("api/badges")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductBadgeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await badgeService.CreateAsync(
            new CreateProductBadgeCommand(
                request.Code,
                request.Name,
                request.Color,
                request.AssignmentType,
                request.IsActive,
                request.SortOrder,
                ToInput(request.Settings)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var dto = ToDto(result.Value!);

        return Created("/api/badges/admin", dto);
    }

    [HttpPut("api/badges/{id:int}")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateProductBadgeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await badgeService.UpdateAsync(
            id,
            new UpdateProductBadgeCommand(
                request.Name,
                request.Color,
                request.AssignmentType,
                request.IsActive,
                request.SortOrder),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(ToDto(result.Value!));
    }

    [HttpDelete("api/badges/{id:int}")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> Deactivate(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await badgeService.DeactivateAsync(id, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : ToErrorResult(result.StatusCode, result.Error);
    }

    [HttpPut("api/badges/{id:int}/settings")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> UpsertSettings(
        int id,
        [FromBody] BadgeSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var input = ToInput(request);

        if (input is null)
        {
            return BadRequest(new { message = "Settings are required." });
        }

        var result = await badgeService.UpsertSettingsAsync(
            id,
            input,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(ToDto(result.Value!));
    }

    [HttpGet("api/products/{productId:int}/badges")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> GetAssignments(
        int productId,
        CancellationToken cancellationToken)
    {
        var assignments = await badgeService.GetAssignmentsAsync(
            productId,
            cancellationToken);

        return Ok(assignments.Select(ToDto));
    }

    [HttpPost("api/products/{productId:int}/badges")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> Assign(
        int productId,
        [FromBody] AssignProductBadgeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await badgeService.AssignManualAsync(
            productId,
            request.BadgeId,
            new AssignProductBadgeCommand(
                request.ProductVariantId,
                request.StartsAt,
                request.EndsAt),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Created(
            $"/api/products/{productId}/badges",
            ToDto(result.Value!));
    }

    [HttpDelete("api/products/{productId:int}/badges/{assignmentId:long}")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> RemoveAssignment(
        int productId,
        long assignmentId,
        CancellationToken cancellationToken)
    {
        var result = await badgeService.RemoveAssignmentAsync(
            productId,
            assignmentId,
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : ToErrorResult(result.StatusCode, result.Error);
    }

    private static BadgeSettingsInput? ToInput(BadgeSettingsRequest? request) =>
        request is null
            ? null
            : new BadgeSettingsInput(
                request.PeriodDays,
                request.Threshold,
                request.DurationDays,
                request.IsEnabled);

    private static ProductBadgeDto ToDto(ProductBadge badge) =>
        new(
            badge.Id,
            badge.Code,
            badge.Name,
            badge.Color,
            badge.AssignmentType,
            badge.IsActive,
            badge.SortOrder,
            badge.CreatedAt,
            badge.UpdatedAt,
            badge.Settings is null ? null : ToDto(badge.Settings));

    private static BadgeSettingsDto ToDto(ProductBadgeSetting settings) =>
        new(
            settings.BadgeId,
            settings.PeriodDays,
            settings.Threshold,
            settings.DurationDays,
            settings.IsEnabled,
            settings.CreatedAt,
            settings.UpdatedAt);

    private static ProductBadgeAssignmentDto ToDto(
        ProductBadgeAssignment assignment) =>
        new(
            assignment.Id,
            assignment.ProductId,
            assignment.ProductVariantId,
            assignment.BadgeId,
            assignment.Badge.Code,
            assignment.Badge.Name,
            assignment.Badge.Color,
            assignment.StartsAt,
            assignment.EndsAt,
            assignment.CreatedAt);
}
