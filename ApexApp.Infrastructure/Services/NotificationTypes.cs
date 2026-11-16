namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Stable notification type codes. Values are stored in
/// <c>notifications.type</c> and must remain within the 50-character column limit.
/// </summary>
public static class NotificationTypes
{
    public const string OrderStatusChanged = "order_status_changed";
    public const string OrderCancelled = "order_cancelled";
    public const string AchievementAwarded = "achievement_awarded";
    public const string ThankReceived = "thank_received";
    public const string ManagerBonusReceived = "manager_bonus_received";
    public const string QuestProofAccepted = "quest_proof_accepted";
    public const string QuestProofRejected = "quest_proof_rejected";
    public const string QuestCompleted = "quest_completed";
    public const string EventCompleted = "event_completed";
    public const string SurveyCompleted = "survey_completed";
    public const string NewActivityLevel = "new_activity_level";
    public const string SupportReply = "support_reply";
    public const string BalanceAdjusted = "balance_adjusted";
}
