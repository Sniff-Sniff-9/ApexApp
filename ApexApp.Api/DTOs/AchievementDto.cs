namespace ApexApp.Api.DTOs
{
    public sealed record AchievementDto(
     int Id,
     string Code,
     string Name,
     string? Description,
     string? IconUrl,
     long RewardAmount,
     string ConditionType,
     int ConditionValue,
     bool IsActive,
     DateTime CreatedAt);
}
