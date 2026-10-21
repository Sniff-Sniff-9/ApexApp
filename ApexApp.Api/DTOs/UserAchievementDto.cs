namespace ApexApp.Api.DTOs
{
    public sealed record UserAchievementDto(
long Id,
Guid UserId,
int AchievementId,
DateTime EarnedAt,
AchievementDto Achievement);
}
