namespace ApexApp.Api.DTOs
{
    public sealed record LeaderboardQuery(
    int Days = 30,
    int Limit = 10);
}
