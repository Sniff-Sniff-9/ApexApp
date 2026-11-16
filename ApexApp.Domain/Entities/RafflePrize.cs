using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ApexApp.Domain.Entities
{
    public class RafflePrize
    {
        public long Id { get; set; }

        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public string PrizeType { get; set; } = null!;

        public string? ImageUrl { get; set; }
        public string? AssetUrl { get; set; }

        public JsonDocument? Metadata { get; set; }

        public int? DurationDays { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual ICollection<RaffleItem> RaffleItems { get; set; }
            = new List<RaffleItem>();

        public virtual ICollection<RaffleSpin> Spins { get; set; }
            = new List<RaffleSpin>();

        public virtual ICollection<UserReward> UserRewards { get; set; }
            = new List<UserReward>();

        public virtual RafflePrizeProduct? Product { get; set; }
    }
}
