namespace ApexApp.Api.Authorization;

/// <summary>
/// Параметры ограничения частоты обращений к модулю ИИ. Ограничение одно на
/// администратора и защищает ключ API от случайных циклов в интерфейсе.
/// </summary>
public static class AiRateLimiting
{
    /// <summary>Имя политики ограничения частоты.</summary>
    public const string PolicyName = "ai";

    /// <summary>Количество запросов в окно на одного администратора.</summary>
    public const int PermitLimit = 6;

    /// <summary>Длина окна ограничения.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}
