using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Infrastructure.Services
{
    public interface IAiService
    {
        Task<string> GenerateContentAsync(
            string prompt,
            CancellationToken cancellationToken = default);

        Task<string> AnalyzeEngagementAsync(
            string statistics,
            CancellationToken cancellationToken = default);
    }
}
