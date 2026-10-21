using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class Achievement
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? IconUrl { get; set; }

        public long RewardAmount { get; set; }

        public string ConditionType { get; set; } = string.Empty;

        public int ConditionValue { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserAchievement> UserAchievements { get; set; }
            = new List<UserAchievement>();
    }
}
