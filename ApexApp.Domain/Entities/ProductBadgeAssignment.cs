using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class ProductBadgeAssignment
    {
        public long Id { get; set; }

        public int ProductId { get; set; }
        public int? ProductVariantId { get; set; }
        public int BadgeId { get; set; }

        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual Product Product { get; set; } = null!;
        public virtual ProductVariant? ProductVariant { get; set; }
        public virtual ProductBadge Badge { get; set; } = null!;
    }
}
