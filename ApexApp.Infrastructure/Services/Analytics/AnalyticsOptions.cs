namespace ApexApp.Infrastructure.Services.Analytics;

/// <summary>
/// Настройки аналитического модуля. Секция конфигурации: <c>Analytics</c>.
/// Значения не хранятся в БД, поэтому их изменение не требует миграций.
/// </summary>
public sealed class AnalyticsOptions
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "Analytics";

    /// <summary>Длина окна по умолчанию, если клиент не передал даты.</summary>
    public int DefaultRangeDays { get; set; } = 30;

    /// <summary>Максимальная длина запрашиваемого окна в днях.</summary>
    public int MaxRangeDays { get; set; } = 400;

    /// <summary>Минимальный размер выборки, при котором показываются доли и рейтинги.</summary>
    public int MinCohort { get; set; } = 5;

    /// <summary>Максимальное количество элементов в списках «топ N».</summary>
    public int MaxTop { get; set; } = 50;

    /// <summary>Максимальное количество временных корзин в одном ряду.</summary>
    public int MaxBuckets { get; set; } = 62;

    /// <summary>Часовой пояс, в котором считаются границы суток, недель и месяцев.</summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>Количество дней без активности, после которого сотрудник считается «молчащим».</summary>
    public int SilentEmployeeDays { get; set; } = 30;

    /// <summary>Падение балла вовлечённости, начиная с которого сотрудник считается снижающим активность.</summary>
    public double DecliningScoreThreshold { get; set; } = 15d;

    /// <summary>Максимальное количество сотрудников в списке снижающих активность.</summary>
    public int MaxDecliningEmployees { get; set; } = 100;

    /// <summary>Количество периодов в тренде вовлечённости по умолчанию.</summary>
    public int EngagementTrendPeriods { get; set; } = 6;

    /// <summary>Максимальное количество периодов в тренде вовлечённости.</summary>
    public int MaxEngagementTrendPeriods { get; set; } = 12;

    /// <summary>Время жизни кеша справочных данных (штат, отделы, каталог).</summary>
    public TimeSpan ReferenceTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Время жизни кеша обзора платформы.</summary>
    public TimeSpan OverviewTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Время жизни кеша временных рядов.</summary>
    public TimeSpan SeriesTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Время жизни кеша сравнения отделов.</summary>
    public TimeSpan DepartmentTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Время жизни кеша аналитики вознаграждений.</summary>
    public TimeSpan RewardTtl { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Время жизни кеша тренда вовлечённости.</summary>
    public TimeSpan EngagementTtl { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Время жизни кеша выводов (инсайтов).</summary>
    public TimeSpan InsightTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Время жизни кеша данных по сотрудникам.</summary>
    public TimeSpan EmployeeTtl { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Время жизни кеша проверок качества данных.</summary>
    public TimeSpan HealthTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Время жизни кеша агрегатов активности пользователей.</summary>
    public TimeSpan ActivityRollupTtl { get; set; } = TimeSpan.FromMinutes(5);
}
