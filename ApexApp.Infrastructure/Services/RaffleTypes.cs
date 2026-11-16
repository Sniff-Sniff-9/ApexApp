using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Infrastructure.Services
{
    public static class RafflePrizeTypes
    {
        public const string ProfileIcon = "profile_icon";
        public const string ProfileFrame = "profile_frame";
        public const string GameBonus = "game_bonus";
        public const string Discount = "discount";
        public const string RandomGift = "random_gift";
        public const string Currency = "currency";
    }

    public static class RaffleSpinStatuses
    {
        public const string Completed = "completed";
        public const string Failed = "failed";
        public const string Pending = "pending";
    }

    public static class UserRewardStatuses
    {
        public const string Available = "available";
        public const string Activated = "activated";
        public const string Used = "used";
        public const string Expired = "expired";
    }
}
