using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Контроллер, отвечающий за управление категориями продуктов.
/// </summary>
[Authorize(Policy = AppPolicies.ActiveUser)]
[ApiController]
[Route("api/categories")]
public sealed class CategoriesController : ApiControllerBase
{

    private readonly ICategoryService _categoryService;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="CategoriesController"/>.
    /// </summary>
    /// <param name="categoryService">Сервис категорий.</param>
    public CategoriesController(ICategoryService categoryService)

    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// Получить все категории.
    /// </summary>
    /// <returns>Список категорий.</returns>
    /// <response code="200">Категории успешно получены.</response>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var categories = await _categoryService.GetAllAsync(
            cancellationToken);

        return Ok(categories.Select(c => new
        {
            c.Id,
            c.Name,
            c.SortOrder
        }));
    }

    /// <summary>
    /// Получить информацию о конкретной категории по её идентификатору.
    /// </summary>
    /// <returns>Информация о категории.</returns>
    /// <response code="200">Категория успешно получена.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Категория не найдена.</response>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var category = await _categoryService.GetByIdAsync(
            id, cancellationToken);

        if (category is null)
        {
            return NotFound(new
            {
                message = "Category was not found."
            });
        }

        return Ok(new
        {
            category.Id,
            category.Name,
            category.SortOrder
        });
    }

    /// <summary>
    /// Создать новую категорию.
    /// </summary>
    /// <returns>Информация о категории.</returns>
    /// <response code="201">Категория успешно создана.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="400">Некорректный запрос.</response>
    [HttpPost]
    [Authorize(Policy = AppPolicies.AdjustBalances)]
    public async Task<IActionResult> Create(
    CreateCategoryRequest request,
    CancellationToken cancellationToken)
    {
        var result = await _categoryService.CreateAsync(
            request.Name, request.SortOrder, cancellationToken);

        if (!result.Success)
            return BadRequest(new { message = result.Error });

        var category = result.Data!;

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, new
        {
            category.Id,
            category.Name,
            category.SortOrder
        });
    }

    /// <summary>
    /// Обновить информацию о категории по её идентификатору.
    /// </summary>
    /// <returns>Информация о категории.</returns>
    /// <response code="200">Категория успешно обновлена.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="400">Некорректный запрос.</response>
    [HttpPut("{id:int}")]
    [Authorize(Policy = AppPolicies.AdjustBalances)]
    public async Task<IActionResult> Update(
        int id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _categoryService.UpdateAsync(
            id, request.Name, request.SortOrder, cancellationToken);

        if (!result.Success)
        {
            if (result.Error == "Category was not found.")
                return NotFound(new { message = result.Error });

            return BadRequest(new { message = result.Error });
        }

        var category = result.Data!;

        return Ok(new
        {
            category.Id,
            category.Name,
            category.SortOrder
        });
    }

    /// <summary>
    /// Удалить категорию по её идентификатору.
    /// </summary>
    /// <response code="204">Категория успешно удалена.</response>
    /// <response code="401">Пользователь не авторизован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Категория не найдена.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = AppPolicies.AdjustBalances)]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _categoryService.DeleteAsync(id, cancellationToken);

        if (!result.Success)
        {
            if (result.Error == "Category was not found.")
                return NotFound(new { message = result.Error });

            return Conflict(new { message = result.Error });
        }

        return NoContent();
    }
}