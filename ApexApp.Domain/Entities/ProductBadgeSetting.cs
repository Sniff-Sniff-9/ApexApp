using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class ProductBadgeSetting
    {
        public int BadgeId { get; set; }

        public int? PeriodDays { get; set; }
        public long? Threshold { get; set; }
        public int? DurationDays { get; set; }

        public bool IsEnabled { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual ProductBadge Badge { get; set; } = null!;
    }
}
