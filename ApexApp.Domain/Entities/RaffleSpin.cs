using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class RaffleSpin
    {
        public long Id { get; set; }

        public long RaffleId { get; set; }

        public Guid UserId { get; set; }

        public long? RaffleItemId { get; set; }
        public long? PrizeId { get; set; }

        public decimal Cost { get; set; }

        public string Status { get; set; } = null!;

        public Guid IdempotencyKey { get; set; } //защищает от двойного списания валюты и повторной выдачи приза.

        public DateTime CreatedAt { get; set; }

        public virtual Raffle Raffle { get; set; } = null!;
        public virtual RaffleItem? RaffleItem { get; set; }
        public virtual RafflePrize? Prize { get; set; }

        public virtual UserReward? UserReward { get; set; }
    }
}
