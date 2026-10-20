namespace ApexApp.Api.DTOs
{
    public class CartOrderRequests
    {
        public sealed record AddCartItemRequest(
    int ProductId,
    int Quantity);

        public sealed record UpdateCartItemRequest(
            int Quantity);

        public sealed record UpdateOrderStatusRequest(
            string Status);
    }
}
