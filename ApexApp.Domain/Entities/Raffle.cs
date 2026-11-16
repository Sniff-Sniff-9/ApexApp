using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class Raffle
    {
        public long Id { get; set; }

        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public decimal SpinCost { get; set; }

        public bool IsActive { get; set; }

        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }

        public int? MaxSpinsPerUser { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual ICollection<RaffleItem> Items { get; set; }
            = new List<RaffleItem>();

        public virtual ICollection<RaffleSpin> Spins { get; set; }
            = new List<RaffleSpin>();
    }
}
