using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Infrastructure.Services
{
    public interface ILeaderboardService
    {
        Task<IReadOnlyList<LeaderboardEntryDto>> GetAsync(
            int days,
            int limit,
            CancellationToken cancellationToken = default);
    }
}
