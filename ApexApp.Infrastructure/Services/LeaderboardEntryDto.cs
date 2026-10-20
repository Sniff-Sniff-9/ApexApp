namespace ApexApp.Infrastructure.Services
{
    public sealed record LeaderboardEntryDto(
    int Rank,
    Guid UserId,
    string Name,
    string? Avatar,
    long ThanksCount);
}
