using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Api.Extensions;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>In-app notifications of the current user.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class NotificationsController(
    INotificationService notificationService) : ApiControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var pagingError = ValidatePaging(page, pageSize);

        if (pagingError is not null)
        {
            return BadRequest(new { message = pagingError });
        }

        var result = await notificationService.GetAsync(
            userId.Value,
            page,
            pageSize,
            unreadOnly,
            cancellationToken);

        return Ok(new PagedResult<NotificationDto>(
            result.Items.Select(ToDto).ToList(),
            result.TotalCount,
            result.Page,
            result.PageSize));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var count = await notificationService.GetUnreadCountAsync(
            userId.Value,
            cancellationToken);

        return Ok(new NotificationUnreadCountDto(count));
    }

    [HttpPatch("{id:long}/read")]
    public async Task<IActionResult> MarkRead(
        long id,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var marked = await notificationService.MarkReadAsync(
            userId.Value,
            id,
            cancellationToken);

        return marked
            ? NoContent()
            : NotFound(new { message = "Notification was not found." });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var count = await notificationService.MarkAllReadAsync(
            userId.Value,
            cancellationToken);

        return Ok(new { marked = count });
    }

    private static NotificationDto ToDto(Notification notification) =>
        new(
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.Data?.RootElement.Clone(),
            notification.IsRead,
            notification.CreatedAt);

    private static string? ValidatePaging(int page, int pageSize) =>
        page < 1
            ? "Page must be greater than or equal to 1."
            : pageSize is < 1 or > MaxPageSize
                ? $"PageSize must be between 1 and {MaxPageSize}."
                : null;
}
