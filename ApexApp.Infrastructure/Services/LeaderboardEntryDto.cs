namespace ApexApp.Infrastructure.Services
{
    /// <summary>
    /// Строка лидерборда: сотрудник, его балл вовлечённости и уровень.
    /// </summary>
    public sealed record LeaderboardEntryDto(
        int Rank,
        Guid UserId,
        string Name,
        string? Avatar,
        double Score,
        string Status,
        string StatusTitle,
        int StatusRank);
}
