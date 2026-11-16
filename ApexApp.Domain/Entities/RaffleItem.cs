using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class RaffleItem
    {
        public long Id { get; set; }

        public long RaffleId { get; set; }
        public long PrizeId { get; set; }

        public decimal Probability { get; set; }

        public int? Quantity { get; set; }
        public int? RemainingQuantity { get; set; }

        public bool IsActive { get; set; }

        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual Raffle Raffle { get; set; } = null!;
        public virtual RafflePrize Prize { get; set; } = null!;

        public virtual ICollection<RaffleSpin> Spins { get; set; }
            = new List<RaffleSpin>();
    }
}
