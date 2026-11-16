using ApexApp.Api.Authorization;
using ApexApp.Api.DTOs;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexApp.Api.Controllers;

/// <summary>Administrator-managed product discounts.</summary>
[ApiController]
[Authorize(Policy = AppPolicies.ActiveUser)]
public sealed class ProductDiscountsController(
    IProductDiscountService discountService) : ApiControllerBase
{
    [HttpGet("api/products/{productId:int}/discounts")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> GetForProduct(
        int productId,
        CancellationToken cancellationToken)
    {
        var discounts = await discountService.GetForProductAsync(
            productId,
            cancellationToken);

        return Ok(discounts.Select(ToDto));
    }

    [HttpPost("api/products/{productId:int}/discounts")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> Create(
        int productId,
        [FromBody] UpsertProductDiscountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await discountService.CreateAsync(
            productId,
            new ProductDiscountInput(
                request.ProductVariantId,
                request.DiscountPercent,
                request.StartsAt,
                request.EndsAt,
                request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        var dto = ToDto(result.Value!);

        return Created($"/api/products/{productId}/discounts", dto);
    }

    [HttpPut("api/discounts/{id:long}")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpsertProductDiscountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await discountService.UpdateAsync(
            id,
            new ProductDiscountInput(
                request.ProductVariantId,
                request.DiscountPercent,
                request.StartsAt,
                request.EndsAt,
                request.IsActive),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToErrorResult(result.StatusCode, result.Error);
        }

        return Ok(ToDto(result.Value!));
    }

    [HttpDelete("api/discounts/{id:long}")]
    [Authorize(Policy = AppPolicies.ManageCatalog)]
    public async Task<IActionResult> Deactivate(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await discountService.DeactivateAsync(id, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : ToErrorResult(result.StatusCode, result.Error);
    }

    private static ProductDiscountDto ToDto(
        ApexApp.Domain.Entities.ProductDiscount discount) =>
        new(
            discount.Id,
            discount.ProductId,
            discount.ProductVariantId,
            discount.DiscountPercent,
            discount.StartsAt,
            discount.EndsAt,
            discount.IsActive,
            discount.CreatedAt,
            discount.UpdatedAt);
}
