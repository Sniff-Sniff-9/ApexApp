namespace ApexApp.Api.DTOs
{
    public sealed record CreateCategoryRequest(
    string Name,
    int SortOrder);

    public sealed record UpdateCategoryRequest(
        string Name,
        int SortOrder);

    public sealed record CreateProductRequest(
        int? CategoryId,
        string Name,
        string? Description,
        long Price,
        string? ImageUrl,
        int? Stock,
        bool IsActive,
        bool IsDigital,
        List<string>? Tags,
        string Metadata);

    public sealed record UpdateProductRequest(
        int? CategoryId,
        string Name,
        string? Description,
        long Price,
        string? ImageUrl,
        int? Stock,
        bool IsActive,
        bool IsDigital,
        List<string>? Tags,
        string Metadata);
}
