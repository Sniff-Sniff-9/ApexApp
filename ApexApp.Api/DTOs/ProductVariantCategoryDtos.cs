namespace ApexApp.Api.DTOs
{
    public sealed record CreateProductVariantCategoryRequest(
    string Name);

    public sealed record UpdateProductVariantCategoryRequest(
        string Name, bool IsActive);

    public sealed record GetProductVariantCategoryRequest(
        int Id, string Name, bool IsActive);
}
