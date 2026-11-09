using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Сервис расчёта вовлечённости пользователя по данным за скользящее окно.
/// </summary>
public interface IEngagementService
{
    /// <summary>
    /// Возвращает статус вовлечённости пользователя за последние
    /// <see cref="EngagementScoring.WindowDays"/> дней.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя из проверенных claims токена.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Рассчитанный статус или ошибка (400 — пустой идентификатор, 404 — пользователь не найден).</returns>
    Task<ServiceResult<EngagementSummary>> GetCurrentAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
