using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class UserProfileCustomization
    {
        public Guid UserId { get; set; }

        public long? ActiveIconRewardId { get; set; }
        public long? ActiveFrameRewardId { get; set; }

        public DateTime UpdatedAt { get; set; }

        public virtual UserReward? ActiveIconReward { get; set; }
        public virtual UserReward? ActiveFrameReward { get; set; }
    }
}
