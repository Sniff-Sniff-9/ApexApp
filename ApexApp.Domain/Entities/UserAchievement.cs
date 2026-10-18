using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class UserAchievement
    {
        public long Id { get; set; }

        public Guid UserId { get; set; }

        public int AchievementId { get; set; }

        public DateTime EarnedAt { get; set; } = DateTime.UtcNow;

        public virtual Achievement Achievement { get; set; } = null!;

        public virtual User User { get; set; } = null!;
    }
}
