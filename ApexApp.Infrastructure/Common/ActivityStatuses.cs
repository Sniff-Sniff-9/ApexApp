namespace ApexApp.Infrastructure.Common;

/// <summary>
/// Значения столбца <c>quest_participations.status</c>.
/// Соответствуют ограничению БД <c>quest_participations_status_check</c>.
/// </summary>
public static class QuestParticipationStatuses
{
    public const string InProgress = "in_progress";

    public const string Completed = "completed";

    public const string Cancelled = "cancelled";
}

/// <summary>
/// Значения столбца <c>event_participants.status</c>.
/// Соответствуют ограничению БД <c>event_participants_status_check</c>.
/// </summary>
public static class EventParticipantStatuses
{
    public const string Registered = "registered";

    public const string Attended = "attended";

    public const string Cancelled = "cancelled";

    public const string NoShow = "no_show";
}

/// <summary>
/// Значения столбца <c>quest_proofs.status</c>.
/// Соответствуют ограничению БД <c>quest_proofs_status_check</c>.
/// </summary>
public static class QuestProofStatuses
{
    public const string Pending = "pending";

    public const string Approved = "approved";

    public const string Rejected = "rejected";
}
