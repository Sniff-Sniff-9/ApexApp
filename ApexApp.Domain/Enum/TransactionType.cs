using NpgsqlTypes;

public enum TransactionType
{
    [PgName("activity_reward")]
    ActivityReward,

    [PgName("thanks")]
    Thanks,

    [PgName("manager_bonus")]
    ManagerBonus,

    [PgName("purchase")]
    Purchase,

    [PgName("refund")]
    Refund,

    [PgName("admin_adjustment")]
    AdminAdjustment,

    [PgName("daily_bonus")]
    DailyBonus,

    [PgName("other")]
    Other,

    [PgName("achievement_reward")]
    AchievementReward
}