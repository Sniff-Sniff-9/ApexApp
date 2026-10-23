using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Выдача наград за активности (квесты и мероприятия).
/// </summary>
public interface IActivityRewardService
{
    /// <summary>
    /// Начисляет награду за завершённый квест.
    /// Повторный вызов не начисляет награду повторно.
    /// Для квеста с нулевой наградой факт выдачи фиксируется без транзакции в ledger.
    /// </summary>
    /// <param name="questParticipationId">Идентификатор участия в квесте.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<ActivityRewardResult>> GrantQuestRewardAsync(
        long questParticipationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Начисляет награду за посещённое мероприятие.
    /// Повторный вызов не начисляет награду повторно.
    /// </summary>
    /// <param name="eventParticipantId">Идентификатор участия в мероприятии.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<ActivityRewardResult>> GrantEventRewardAsync(
        long eventParticipantId,
        CancellationToken cancellationToken = default);
}
