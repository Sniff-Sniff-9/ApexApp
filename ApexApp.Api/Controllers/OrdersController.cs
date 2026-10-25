using ApexApp.Api.Authorization;
using ApexApp.Api.Extensions;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static ApexApp.Api.DTOs.CartOrderRequests;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Контроллер, отвечающий за управление заказами пользователей.
/// </summary>
[ApiController]
[Route("api/orders")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class OrdersController : ApiControllerBase
{
    private readonly IOrderService _orderService;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="OrdersController"/>.
    /// </summary>
    /// <param name="orderService">Сервис заказов.</param>
    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Оформить заказ, содержащий текущие товары в текущей корзине, для текущего пользователя.
    /// </summary>
    /// <returns>Данные об оформленном заказе и содержащихся в нем товарах.</returns>
    /// <response code="201">Заказ успешно оформлен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="400">Некорректный запрос.</response>
    /// <response code="409">Конфликт ресурсов.</response>
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
            return Unauthorized();

        var result = await _orderService.CheckoutAsync(
            userId.Value, cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new
            {
                message = result.Error
            });
        }

        var order = result.Data!;
        var newAchievement = result.NewAchievement;

        return CreatedAtAction(
        nameof(GetById),
        new { id = order.Id },
        new
        {
            order.Id,
            order.Status,
            order.TotalAmount,
            order.CreatedAt,

            Items = order.OrderItems.Select(i => new
            {
                i.ProductId,
                i.Quantity,
                i.PriceAtPurchase
            }),

            NewAchievement = newAchievement is null
                ? null
                : new
                {
                    newAchievement.Id,
                    newAchievement.Code,
                    newAchievement.Name,
                    newAchievement.Description,
                    newAchievement.IconUrl,
                    newAchievement.RewardAmount
                }
        });
    }

    /// <summary>
    /// Получить историю заказов текущего пользователя, включая информацию о каждом заказе и содержащихся в нем товарах.
    /// </summary>
    /// <returns>Список заказов.</returns>
    /// <response code="200">Список заказов успешно получен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
            return Unauthorized();

        var orders = await _orderService.GetHistoryAsync(
            userId.Value, cancellationToken);

        return Ok(orders.Select(ToResponse));
    }

    /// <summary>
    /// Получить информацию о конкретном заказе текущего пользователя по его идентификатору, включая информацию о заказе и содержащихся в нем товарах.
    /// </summary>
    /// <returns>Информация о заказе.</returns>
    /// <response code="200">Заказ успешно получен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Заказ не найден.</response>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
            return Unauthorized();

        var order = await _orderService.GetByIdAsync(
            userId.Value, id, cancellationToken);

        if (order is null)
            return NotFound(new { message = "Order was not found." });

        return Ok(ToResponse(order));
    }

    private static object ToResponse(
        ApexApp.Domain.Entities.Order order)
    {
        return new
        {
            order.Id,
            order.Status,
            order.TotalAmount,
            order.CreatedAt,
            order.UpdatedAt,
            order.Comment,
            Items = order.OrderItems.Select(i => new
            {
                i.Id,
                i.ProductId,
                ProductName = i.Product?.Name,
                i.Quantity,
                i.PriceAtPurchase
            })
        };
    }

    private static object ToResponseAll(
        ApexApp.Domain.Entities.Order order)
    {
        return new
        {
            order.Id,
            order.UserId,
            order.Status,
            order.TotalAmount,
            order.CreatedAt,
            order.UpdatedAt,
            order.Comment,
            Items = order.OrderItems.Select(i => new
            {
                i.Id,
                i.ProductId,
                ProductName = i.Product?.Name,
                i.Quantity,
                i.PriceAtPurchase
            })
        };
    }

    /// <summary>
    /// Получить все заказы пользователей.
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        if (userId is null)
            return Unauthorized();

        var orders = await _orderService.GetAllAsync(cancellationToken);

        return Ok(orders.Select(ToResponseAll));
    }

    /// <summary>
    /// Обновить статус заказа.
    /// </summary>
    /// <returns>Информация о заказе.</returns>
    /// <response code="200">Заказ успешно обновлен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Заказ не найден.</response>
    [HttpPatch("{id:long}/status")]
    [Authorize(Policy = AppPolicies.AdjustBalances)]
    public async Task<IActionResult> UpdateStatus(
    long id,
    UpdateOrderStatusRequest request,
    CancellationToken cancellationToken)
    {
        var employeeId = User.GetUserId();

        if (employeeId is null)
            return Unauthorized();

        if (!Enum.TryParse<OrderStatus>(
                request.Status,
                ignoreCase: true,
                out var newStatus) ||
            !Enum.IsDefined(newStatus))
        {
            return BadRequest(new { message = "Invalid order status." });
        }

        var result = await _orderService.UpdateStatusAsync(
            id,
            employeeId.Value,
            newStatus,
            cancellationToken);

        if (!result.Success)
        {
            return StatusCode(
                result.StatusCode,
                new { message = result.Error });
        }

        var order = result.Data!;

        return Ok(new
        {
            order.Id,
            order.Status,
            order.ProcessedBy,
            order.UpdatedAt
        });
    }
}