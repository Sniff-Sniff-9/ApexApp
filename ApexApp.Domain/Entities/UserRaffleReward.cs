using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class UserReward
    {
        public long Id { get; set; }

        public Guid UserId { get; set; }

        public long PrizeId { get; set; }

        public long? RaffleSpinId { get; set; }

        public string Status { get; set; } = null!;

        public string? Metadata { get; set; }

        public DateTime? ExpiresAt { get; set; }
        public DateTime? ActivatedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual RafflePrize Prize { get; set; } = null!;
        public virtual RaffleSpin? RaffleSpin { get; set; }
    }
}
