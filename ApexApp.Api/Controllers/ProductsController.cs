using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Контроллер, отвечающий за управление товарами.
/// </summary>
[Authorize(Policy = AppPolicies.ActiveUser)]
[ApiController]
[Route("api/products")]
public sealed class ProductsController : ApiControllerBase
{
    private readonly IProductService _productService;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="ProductsController"/>.
    /// </summary>
    /// <param name="productService">Сервис товаров.</param>
    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }
    /// <summary>
    /// Получить все активные товары, с возможностью фильтрации по категории.
    /// </summary>
    /// <returns>Список активных товаров.</returns>
    /// <response code="200">Товары успешно получены.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Категория не найдена.</response>
    /// <response code="400">Некорректный запрос.</response>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? categoryId,
        CancellationToken cancellationToken)
    {
        if (categoryId.HasValue && categoryId.Value <= 0)
        {
            return BadRequest(new
            {
                message = "Category ID must be positive."
            });
        }

        var products = await _productService.GetActiveAsync(
            categoryId, cancellationToken);

        return Ok(products.Select(ToResponse));
    }

    /// <summary>
    /// Получить информацию о конкретном товаре по его идентификатору.
    /// </summary>
    /// <returns>Информация о товаре.</returns>
    /// <response code="200">Товар успешно получен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Товар не найден.</response>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var product = await _productService.GetActiveByIdAsync(
            id, cancellationToken);

        if (product is null)
        {
            return NotFound(new
            {
                message = "Product was not found."
            });
        }

        return Ok(ToResponse(product));
    }

    /// <summary>
    /// Создать новый товар.
    /// </summary>
    /// <returns>Информация о товаре.</returns>
    /// <response code="201">Товар успешно создан.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="400">Некорректный запрос.</response>
    [HttpPost]
    [Authorize(Policy = AppPolicies.AdjustBalances)]
    public async Task<IActionResult> Create(
    CreateProductRequest request,
    CancellationToken cancellationToken)
    {
        var product = new Product
        {
            CategoryId = request.CategoryId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            ImageUrl = request.ImageUrl,
            Stock = request.Stock,
            IsActive = request.IsActive,
            IsDigital = request.IsDigital
        };

        var result = await _productService.CreateAsync(product, cancellationToken);

        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Data!.Id },
            ToResponse(result.Data));
    }

    /// <summary>
    /// Обновить информацию о товаре по его идентификатору.
    /// </summary>
    /// <returns>Информация о товаре.</returns>
    /// <response code="200">Товар успешно обновлен.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="400">Некорректный запрос.</response>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AppPolicies.AdjustBalances)]
    public async Task<IActionResult> Update(
        int id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = new Product
        {
            CategoryId = request.CategoryId,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            ImageUrl = request.ImageUrl,
            Stock = request.Stock,
            IsActive = request.IsActive,
            IsDigital = request.IsDigital
        };

        var result = await _productService.UpdateAsync(
            id, product, cancellationToken);

        if (!result.Success)
        {
            if (result.Error == "Product was not found.")
                return NotFound(new { message = result.Error });

            return BadRequest(new { message = result.Error });
        }

        return Ok(ToResponse(result.Data!));
    }


    /// <summary>
    /// Удалить товар по его идентификатору.
    /// </summary>
    /// <response code="204">Товар успешно удален.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Товар не найден.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AppPolicies.AdjustBalances)]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _productService.DeleteAsync(id, cancellationToken);

        if (!result.Success)
        {
            if (result.Error == "Product was not found.")
                return NotFound(new { message = result.Error });

            return BadRequest(new { message = result.Error });
        }

        return NoContent();
    }

    private static object ToResponse(
        ApexApp.Domain.Entities.Product product)
    {
        return new
        {
            product.Id,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Price,
            product.ImageUrl,
            product.Stock,
            product.IsDigital,
            product.CreatedAt,
            product.UpdatedAt
        };
    }
}