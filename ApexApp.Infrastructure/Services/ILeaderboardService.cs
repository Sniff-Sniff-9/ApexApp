using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services
{
    public interface ILeaderboardService
    {
        /// <summary>
        /// Возвращает лидерборд по баллу вовлечённости за каноническое окно
        /// <see cref="EngagementScoring.WindowDays"/>.
        /// </summary>
        /// <param name="limit">Максимальное число строк лидерборда.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        Task<IReadOnlyList<LeaderboardEntryDto>> GetAsync(
            int limit,
            CancellationToken cancellationToken = default);
    }
}
