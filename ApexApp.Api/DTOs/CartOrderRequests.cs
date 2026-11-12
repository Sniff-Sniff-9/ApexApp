namespace ApexApp.Api.DTOs
{
    /// <summary>
    /// Запросы корзины и заказов.
    /// </summary>
    public class CartOrderRequests
    {
        /// <summary>
        /// Запрос на добавление товара в корзину.
        /// </summary>
        /// <param name="ProductId">Идентификатор товара.</param>
        /// <param name="Quantity">Количество.</param>
        /// <param name="VariantId">Идентификатор варианта товара или <c>null</c>.</param>
        public sealed record AddCartItemRequest(
            int ProductId,
            int Quantity,
            int? VariantId = null);

        /// <summary>
        /// Запрос на обновление строки корзины.
        /// </summary>
        /// <param name="Quantity">Количество.</param>
        /// <param name="VariantId">Новый вариант или <c>null</c>, чтобы оставить текущий.</param>
        public sealed record UpdateCartItemRequest(
            int Quantity,
            int? VariantId = null);

        /// <summary>
        /// Запрос на изменение статуса заказа.
        /// </summary>
        /// <param name="Status">Новый статус заказа.</param>
        public sealed record UpdateOrderStatusRequest(
            string Status);
    }
}
