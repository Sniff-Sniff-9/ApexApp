using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class ProductVariant
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public string Name { get; set; } = null!;

        public int? CategoryId { get; set; }

        public string? ImageUrl { get; set; }

        public long? Price { get; set; }

        public int Stock { get; set; }

        public bool IsActive { get; set; }

        public ProductVariantCategory Category { get; set; } = null!;

        public virtual Product Product { get; set; } = null!;
    }
}
