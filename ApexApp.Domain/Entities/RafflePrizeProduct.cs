using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class RafflePrizeProduct
    {
        public long Id { get; set; }

        public long PrizeId { get; set; }

        public int ProductId { get; set; }
        public int? ProductVariantId { get; set; }

        public int Quantity { get; set; }

        public virtual RafflePrize Prize { get; set; } = null!;

        public virtual Product Product { get; set; } = null!;
        public virtual ProductVariant? ProductVariant { get; set; }
    }
}
