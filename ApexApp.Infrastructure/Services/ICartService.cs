using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Services;

public interface ICartService
{
    Task<List<CartItem>> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет товар в корзину. Вариант необязателен; при его указании проверяются
    /// принадлежность товару, активность и остаток варианта.
    /// </summary>
    /// <param name="userId">Пользователь из проверенных claims.</param>
    /// <param name="productId">Идентификатор товара.</param>
    /// <param name="variantId">Идентификатор варианта или <c>null</c>.</param>
    /// <param name="quantity">Количество.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат операции.</returns>
    Task<(bool Success, string? Error)> AddAsync(
        Guid userId,
        int productId,
        int? variantId,
        int quantity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет количество и, при необходимости, вариант товара в корзине.
    /// Если <paramref name="variantId"/> равно <c>null</c>, выбранный вариант не меняется.
    /// </summary>
    /// <param name="userId">Пользователь из проверенных claims.</param>
    /// <param name="itemId">Идентификатор строки корзины.</param>
    /// <param name="variantId">Новый вариант или <c>null</c>, чтобы оставить текущий.</param>
    /// <param name="quantity">Количество.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат операции.</returns>
    Task<(bool Success, string? Error)> UpdateAsync(
        Guid userId,
        long itemId,
        int? variantId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> RemoveAsync(
        Guid userId,
        long itemId,
        CancellationToken cancellationToken = default);
}
