using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class ProductDiscount
    {
        public long Id { get; set; }

        public int ProductId { get; set; }
        public int? ProductVariantId { get; set; }

        public int DiscountPercent { get; set; } 

        public DateTime StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual Product Product { get; set; } = null!;
        public virtual ProductVariant? ProductVariant { get; set; }
    }
}
