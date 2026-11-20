namespace ApexApp.Api.DTOs
{
    /// <summary>
    /// Параметры запроса лидерборда.
    /// </summary>
    public sealed class LeaderboardQuery
    {
        /// <summary>Максимальное число строк лидерборда (1–100).</summary>
        public int Limit { get; set; } = 10;
    }
}
