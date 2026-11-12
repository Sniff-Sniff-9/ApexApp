using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs.Analytics;
using ApexApp.Infrastructure.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>
/// Аналитика вознаграждений: спрос на каталог, экономика баллов и заказы.
/// </summary>
[ApiController]
[Route("api/analytics/rewards")]
[Authorize(Policy = AppPolicies.ViewRewardAnalytics)]
public sealed class RewardAnalyticsController(
    IRewardAnalyticsService rewardService) : ApiControllerBase
{
    /// <summary>
    /// Спрос на товары каталога: единицы, уникальные покупатели, баллы и остатки.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Каталог со спросом.</returns>
    /// <response code="200">Каталог рассчитан.</response>
    /// <response code="400">Некорректные параметры.</response>
    [HttpGet("catalog")]
    [ProducesResponseType(typeof(RewardCatalogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RewardCatalogDto>> GetCatalog(
        [FromQuery] AnalyticsQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!AnalyticsQueryBinder.TryCreate(request, null, out var query, out var error))
        {
            return BadRequest(new { message = error });
        }

        var result = await rewardService.GetCatalogAsync(query, cancellationToken);

        return result.IsSuccess
            ? Ok(RewardCatalogDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }

    /// <summary>
    /// Спрос на варианты товаров: единицы, покупатели, баллы и остатки вариантов.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Варианты со спросом.</returns>
    /// <response code="200">Варианты рассчитаны.</response>
    /// <response code="400">Некорректные параметры.</response>
    [HttpGet("variants")]
    [ProducesResponseType(typeof(RewardVariantListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RewardVariantListDto>> GetVariants(
        [FromQuery] AnalyticsQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!AnalyticsQueryBinder.TryCreate(request, null, out var query, out var error))
        {
            return BadRequest(new { message = error });
        }

        var result = await rewardService.GetVariantsAsync(query, cancellationToken);

        return result.IsSuccess
            ? Ok(RewardVariantListDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }

    /// <summary>
    /// Спрос по категориям каталога.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Спрос по категориям.</returns>
    /// <response code="200">Категории рассчитаны.</response>
    /// <response code="400">Некорректные параметры.</response>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<RewardCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RewardCategoryDto>>> GetCategories(
        [FromQuery] AnalyticsQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!AnalyticsQueryBinder.TryCreate(request, null, out var query, out var error))
        {
            return BadRequest(new { message = error });
        }

        var result = await rewardService.GetCategoriesAsync(query, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value!.Select(RewardCategoryDto.From).ToList())
            : ToErrorResult(result.StatusCode, result.Error);
    }

    /// <summary>
    /// Экономика баллов: начисления по источникам, списания, остатки и сверка с заказами.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Экономика баллов.</returns>
    /// <response code="200">Экономика рассчитана.</response>
    /// <response code="400">Некорректные параметры.</response>
    [HttpGet("economy")]
    [ProducesResponseType(typeof(RewardEconomyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RewardEconomyDto>> GetEconomy(
        [FromQuery] AnalyticsQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!AnalyticsQueryBinder.TryCreate(request, null, out var query, out var error))
        {
            return BadRequest(new { message = error });
        }

        var result = await rewardService.GetEconomyAsync(query, cancellationToken);

        return result.IsSuccess
            ? Ok(RewardEconomyDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }

    /// <summary>
    /// Заказы: статусы, единицы, покупатели, стоимость в баллах и время обработки.
    /// </summary>
    /// <param name="request">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Аналитика заказов.</returns>
    /// <response code="200">Заказы рассчитаны.</response>
    /// <response code="400">Некорректные параметры.</response>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(OrderAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrderAnalyticsDto>> GetOrders(
        [FromQuery] AnalyticsQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!AnalyticsQueryBinder.TryCreate(request, null, out var query, out var error))
        {
            return BadRequest(new { message = error });
        }

        var result = await rewardService.GetOrdersAsync(query, cancellationToken);

        return result.IsSuccess
            ? Ok(OrderAnalyticsDto.From(result.Value!))
            : ToErrorResult(result.StatusCode, result.Error);
    }
}
