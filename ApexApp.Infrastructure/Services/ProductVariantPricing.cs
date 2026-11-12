using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Результат проверки выбранного товара (с вариантом или без).
/// </summary>
public enum ProductSelectionStatus
{
    /// <summary>Выбор корректен, товар можно добавить в корзину и оформить.</summary>
    Ok = 0,

    /// <summary>Количество должно быть положительным.</summary>
    QuantityInvalid = 1,

    /// <summary>Товар неактивен или является цифровым.</summary>
    ProductUnavailable = 2,

    /// <summary>Вариант неактивен или принадлежит другому товару.</summary>
    VariantUnavailable = 3,

    /// <summary>Недостаточно остатка.</summary>
    InsufficientStock = 4
}

/// <summary>
/// Рассчитанный выбор товара: цена за единицу, доступный остаток и статус проверки.
/// </summary>
/// <param name="Status">Статус проверки.</param>
/// <param name="UnitPrice">Цена за единицу в баллах.</param>
/// <param name="AvailableStock">Доступный остаток; <c>null</c> — без ограничения.</param>
public sealed record ProductSelection(
    ProductSelectionStatus Status,
    long UnitPrice,
    int? AvailableStock)
{
    /// <summary>Признак корректного выбора.</summary>
    public bool IsValid => Status == ProductSelectionStatus.Ok;

    /// <summary>Неудачный выбор без цены и остатка.</summary>
    /// <param name="status">Причина.</param>
    /// <returns>Результат проверки.</returns>
    public static ProductSelection Invalid(ProductSelectionStatus status) => new(status, 0, null);
}

/// <summary>
/// Единая точка расчёта цены и проверки доступности товара с учётом выбранного варианта.
/// Используется корзиной и оформлением заказа, поэтому цена и остаток не могут
/// разойтись между этими сценариями. Цена всегда берётся из базы данных.
/// </summary>
public static class ProductVariantPricing
{
    /// <summary>
    /// Возвращает цену за единицу: цена варианта, а при её отсутствии — цена товара.
    /// </summary>
    /// <param name="product">Товар.</param>
    /// <param name="variant">Выбранный вариант или <c>null</c>.</param>
    /// <returns>Цена в баллах.</returns>
    public static long ResolveUnitPrice(Product product, ProductVariant? variant) =>
        variant?.Price ?? product.Price;

    /// <summary>
    /// Проверяет выбранный товар и рассчитывает цену и остаток.
    /// </summary>
    /// <param name="product">Товар.</param>
    /// <param name="variant">Выбранный вариант или <c>null</c>.</param>
    /// <param name="quantity">Запрошенное количество.</param>
    /// <returns>Результат проверки.</returns>
    public static ProductSelection Evaluate(Product product, ProductVariant? variant, int quantity)
    {
        if (quantity <= 0)
        {
            return ProductSelection.Invalid(ProductSelectionStatus.QuantityInvalid);
        }

        if (!product.IsActive || product.IsDigital)
        {
            return ProductSelection.Invalid(ProductSelectionStatus.ProductUnavailable);
        }

        if (variant is not null)
        {
            // Вариант обязан принадлежать выбранному товару: подмена идентификатора
            // клиентом не должна позволять купить вариант другого товара.
            if (!variant.IsActive || variant.ProductId != product.Id)
            {
                return ProductSelection.Invalid(ProductSelectionStatus.VariantUnavailable);
            }

            var variantStock = (int?)variant.Stock;

            return variantStock < quantity
                ? ProductSelection.Invalid(ProductSelectionStatus.InsufficientStock)
                : new ProductSelection(
                    ProductSelectionStatus.Ok,
                    ResolveUnitPrice(product, variant),
                    variantStock);
        }

        if (product.Stock.HasValue && product.Stock.Value < quantity)
        {
            return ProductSelection.Invalid(ProductSelectionStatus.InsufficientStock);
        }

        return new ProductSelection(
            ProductSelectionStatus.Ok,
            ResolveUnitPrice(product, null),
            product.Stock);
    }
}
