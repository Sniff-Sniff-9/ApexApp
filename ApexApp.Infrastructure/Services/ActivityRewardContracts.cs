namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Типы сущностей, указываемые в <c>Transaction.RelatedEntityType</c> для наград за активности.
/// </summary>
public static class ActivityRewardEntityTypes
{
    public const string Quest = "quest";

    public const string Event = "event";

    public const string Survey = "survey";
}

/// <summary>
/// Результат выдачи награды за активность.
/// </summary>
/// <param name="UserId">Идентификатор пользователя, которому начислена награда.</param>
/// <param name="Amount">Начисленная сумма (0 для квеста с нулевой наградой или при повторной выдаче).</param>
/// <param name="BalanceAfter">Остаток личного баланса после начисления.</param>
/// <param name="AlreadyGranted">Признак того, что награда уже была выдана ранее.</param>
public sealed record ActivityRewardResult(
    Guid UserId,
    long Amount,
    long? BalanceAfter,
    bool AlreadyGranted);

/// <summary>
/// Данные награды за квест, факт выдачи которой вызывающий код уже зафиксировал.
/// </summary>
/// <param name="UserId">Идентификатор пользователя-участника.</param>
/// <param name="QuestParticipationId">Идентификатор участия в квесте.</param>
/// <param name="RewardAmount">Сумма награды.</param>
/// <param name="QuestTitle">Название квеста для описания транзакции.</param>
public sealed record QuestRewardGrant(
    Guid UserId,
    long QuestParticipationId,
    long RewardAmount,
    string QuestTitle);
