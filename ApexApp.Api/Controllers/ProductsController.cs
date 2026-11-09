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
    private readonly ProductVariantCategoryService _variantCategoryService;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="ProductsController"/>.
    /// </summary>
    /// <param name="productService">Сервис товаров.</param>
    /// <param name="variantCategoryService">Сервис категорий вариантов товаров.</param>
    public ProductsController(IProductService productService, ProductVariantCategoryService variantCategoryService)
    {
        _productService = productService;
        _variantCategoryService = variantCategoryService;

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
    [Authorize(Policy = AppPolicies.ManageCatalog)]
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
    [Authorize(Policy = AppPolicies.ManageCatalog)]
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
    [Authorize(Policy = AppPolicies.ManageCatalog)]
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
            product.UpdatedAt,
            product.IsActive
        };
    }

    /// <summary>
    /// Получить все товары, включая неактивные.
    /// </summary>
    /// <response code="200">Список товаров успешно получен.</response>
    /// <response code="400">Некорректный идентификатор категории.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    [HttpGet("all")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> GetAllProducts(
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

        var products = await _productService.GetAllAsync(
            categoryId,
            cancellationToken);

        return Ok(products.Select(ToResponse));
    }

    [HttpGet("{productId}/variants")]
    public async Task<IActionResult> GetVariants(int productId)
    {
        var variants = await _productService.GetVariantsAsync(productId);

        return Ok(variants.Select(v => new ProductVariantDto
        {
            Id = v.Id,
            ProductId = v.ProductId,
            Name = v.Name,
            CategoryId = v.CategoryId,
            ImageUrl = v.ImageUrl,
            Price = v.Price,
            Stock = v.Stock,
            IsActive = v.IsActive
        }));
    }

    [HttpGet("{productId}/variants/{variantId}")]
    public async Task<IActionResult> GetVariant(int productId, int variantId)
    {
        var variant = await _productService.GetVariantByIdAsync(productId, variantId);

        if (variant == null)
            return NotFound();

        return Ok(new ProductVariantDto
        {
            Id = variant.Id,
            ProductId = variant.ProductId,
            Name = variant.Name,
            CategoryId = variant.CategoryId,
            ImageUrl = variant.ImageUrl,
            Price = variant.Price,
            Stock = variant.Stock,
            IsActive = variant.IsActive
        });
    }

    [HttpPost("{productId}/variants")]
    public async Task<IActionResult> AddVariant(int productId, CreateProductVariantDto dto)
    {
        var variant = new ProductVariant
        {
            Name = dto.Name,
            CategoryId = dto.CategoryId,
            ImageUrl = dto.ImageUrl,
            Price = dto.Price,
            Stock = dto.Stock
        };

        var created = await _productService.AddVariantAsync(productId, variant);

        return CreatedAtAction(
            nameof(GetVariant),
            new { productId, variantId = created.Id },
            new ProductVariantDto
            {
                Id = created.Id,
                ProductId = created.ProductId,
                Name = created.Name,
                CategoryId = created.CategoryId,
                ImageUrl = created.ImageUrl,
                Price = created.Price,
                Stock = created.Stock,
                IsActive = created.IsActive
            });
    }

    [HttpPut("{productId}/variants/{variantId}")]
    public async Task<IActionResult> UpdateVariant(int productId, int variantId, UpdateProductVariantDto dto)
    {
        var variant = new ProductVariant
        {
            Name = dto.Name,
            CategoryId = dto.CategoryId,
            ImageUrl = dto.ImageUrl,
            Price = dto.Price,
            Stock = dto.Stock,
            IsActive = dto.IsActive
        };

        var updated = await _productService.UpdateVariantAsync(productId, variantId, variant);

        if (updated == null)
            return NotFound();

        return Ok(new ProductVariantDto
        {
            Id = updated.Id,
            ProductId = updated.ProductId,
            Name = updated.Name,
            CategoryId = updated.CategoryId,
            ImageUrl = updated.ImageUrl,
            Price = updated.Price,
            Stock = updated.Stock,
            IsActive = updated.IsActive
        });
    }

    [HttpDelete("{productId}/variants/{variantId}")]
    public async Task<IActionResult> DeleteVariant(int productId, int variantId)
    {
        var deleted = await _productService.DeleteVariantAsync(productId, variantId);

        if (!deleted)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Получить все активные категории вариантов товаров.
    /// </summary>
    [HttpGet("variant-categories")]
    public async Task<IActionResult> GetVariantCategories(
        CancellationToken cancellationToken)
    {
        var categories = await _variantCategoryService.GetAllAsync(
            cancellationToken);

        return Ok(categories.Select(c => new GetProductVariantCategoryRequest(c.Id, c.Name, c.IsActive)));
    }


    /// <summary>
    /// Получить категорию варианта по ID.
    /// </summary>
    [HttpGet("variant-categories/{id:int}")]
    public async Task<IActionResult> GetVariantCategoryById(
        int id,
        CancellationToken cancellationToken)
    {
        var category = await _variantCategoryService.GetByIdAsync(
            id, cancellationToken);

        if (category is null)
            return NotFound(new
            {
                message = "Variant category was not found."
            });

        return Ok(new GetProductVariantCategoryRequest(category.Id, category.Name, category.IsActive));
    }

    /// <summary>
    /// Создать категорию варианта товара.
    /// </summary>
    [HttpPost("variant-categories")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> CreateVariantCategory(
        CreateProductVariantCategoryRequest dto,
        CancellationToken cancellationToken)
    {
        var category = new ProductVariantCategory
        {
            Name = dto.Name
        };

        var result = await _variantCategoryService.CreateAsync(
            category, cancellationToken);

        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return CreatedAtAction(
            nameof(GetVariantCategoryById),
            new { id = result.Data!.Id },
            result.Data);
    }

    /// <summary>
    /// Обновить категорию варианта товара.
    /// </summary>
    [HttpPut("variant-categories/{id:int}")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> UpdateVariantCategory(
        int id,
        UpdateProductVariantCategoryRequest dto,
        CancellationToken cancellationToken)
    {
        var category = new ProductVariantCategory
        {
            Name = dto.Name,
            IsActive = dto.IsActive
        };

        var result = await _variantCategoryService.UpdateAsync(
            id, category, cancellationToken);

        if (!result.Success)
        {
            if (result.Error == "Category was not found.")
                return NotFound(new { message = result.Error });

            return BadRequest(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Удалить категорию варианта товара.
    /// </summary>
    [HttpDelete("variant-categories/{id:int}")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> DeleteVariantCategory(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _variantCategoryService.DeleteAsync(
            id, cancellationToken);

        if (!result.Success)
        {
            if (result.Error == "Category was not found.")
                return NotFound(new { message = result.Error });

            return BadRequest(new { message = result.Error });
        }

        return NoContent();
    }
}