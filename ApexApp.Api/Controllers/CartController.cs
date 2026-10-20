using ApexApp.Api.Authorization;
using ApexApp.Api.Extensions;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static ApexApp.Api.DTOs.CartOrderRequests;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Контроллер, отвечающий за управление корзиной пользователя.
/// </summary>
[ApiController]
[Route("api/cart")]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class CartController : ApiControllerBase
{
    private readonly ICartService _cartService;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="CartController"/>.
    /// </summary>
    /// <param name="cartService">Сервис корзины.</param>
    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    /// <summary>
    /// Получить корзину текущего пользователя.
    /// </summary>
    /// <returns>Список товаров в корзине</returns>
    /// <response code="200">Корзина успешно получена.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var items = await _cartService.GetAsync(
            userId.Value, cancellationToken);

        return Ok(items.Select(x => new
        {
            x.Id,
            x.ProductId,
            ProductName = x.Product.Name,
            x.Product.ImageUrl,
            UnitPrice = x.Product.Price,
            x.Quantity,
            Total = (decimal)x.Product.Price * x.Quantity,
            x.AddedAt
        }));
    }

    /// <summary>
    /// Добавить товар в корзину текущего пользователя.
    /// </summary>
    /// <returns>Сообщение о результате операции.</returns>
    /// <response code="200">Товар успешно добавлен в корзину.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="400">Неверные данные запроса.</response>
    [HttpPost("items")]
    public async Task<IActionResult> Add(
        AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _cartService.AddAsync(
            userId.Value, request.ProductId, request.Quantity,
            cancellationToken);

        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Item added to cart." });
    }

    /// <summary>
    /// Обновить количество товара в корзине текущего пользователя.
    /// </summary>
    /// <returns>Сообщение о результате операции.</returns>
    /// <response code="200">Товар успешно обновлен в корзине.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="400">Неверные данные запроса.</response>
    /// <response code="404">Товар не найден.</response>
    [HttpPut("items/{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _cartService.UpdateAsync(
            userId.Value, id, request.Quantity, cancellationToken);

        if (!result.Success)
        {
            if (result.Error == "Cart item was not found.")
                return NotFound(new { message = result.Error });

            return BadRequest(new { message = result.Error });
        }

        return Ok(new { message = "Cart item updated." });
    }


    /// <summary>
    /// Удалить товар из корзины текущего пользователя.
    /// </summary>
    /// <response code="204">Товар успешно удален из корзины.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Товар не найден.</response>
    [HttpDelete("items/{id:long}")]
    public async Task<IActionResult> Remove(
        long id,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _cartService.RemoveAsync(
            userId.Value, id, cancellationToken);

        if (!result.Success)
        {
            if (result.Error == "Cart item was not found.")
                return NotFound(new { message = result.Error });

            return BadRequest(new { message = result.Error });
        }

        return NoContent();
    }
}