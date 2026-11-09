namespace ApexApp.Api.DTOs
{
    public class ProductVariantDto
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public string Name { get; set; } = null!;

        public int? CategoryId { get; set; }

        public string? ImageUrl { get; set; }

        public decimal? Price { get; set; }

        public int Stock { get; set; }

        public bool IsActive { get; set; }
    }

    public class CreateProductVariantDto
    {
        public string Name { get; set; } = null!;

        public int? CategoryId { get; set; }

        public string? ImageUrl { get; set; }

        public long Price { get; set; }

        public int Stock { get; set; }
    }

    public class UpdateProductVariantDto
    {
        public string Name { get; set; } = null!;

        public int? CategoryId { get; set; }

        public string? ImageUrl { get; set; }

        public long Price { get; set; }

        public int Stock { get; set; }

        public bool IsActive { get; set; }
    }
}
