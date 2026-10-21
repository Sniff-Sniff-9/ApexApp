namespace ApexApp.Api.DTOs
{
    public class UpdateAchievementRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public long RewardAmount { get; set; }
        public bool IsActive { get; set; }
    }
}
