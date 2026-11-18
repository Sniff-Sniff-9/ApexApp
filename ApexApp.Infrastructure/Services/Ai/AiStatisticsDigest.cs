using System.Globalization;
using System.Text;

namespace ApexApp.Infrastructure.Services.Ai;

/// <summary>Сравнение формата активности в сводке для модели.</summary>
/// <param name="Format">Машинный код формата.</param>
/// <param name="Title">Название формата.</param>
/// <param name="Activities">Количество активностей в окне.</param>
/// <param name="Participants">Количество уникальных участников.</param>
/// <param name="ReachRate">Доля сотрудников, участвовавших хотя бы раз.</param>
/// <param name="CompletionRate">Доля завершивших от начавших.</param>
/// <param name="RewardClaimRate">Доля полученных наград.</param>
/// <param name="IsReliable">Признак достаточной выборки.</param>
public sealed record AiFormatSnapshot(
    string Format,
    string Title,
    long Activities,
    long Participants,
    double? ReachRate,
    double? CompletionRate,
    double? RewardClaimRate,
    bool IsReliable);

/// <summary>Показатели вовлечённости, рассчитанные Backend-ом.</summary>
/// <param name="Period">Описание окна расчёта.</param>
/// <param name="ActiveEmployees">Активные сотрудники.</param>
/// <param name="ParticipatingEmployees">Участники активностей.</param>
/// <param name="ParticipationRate">Доля участия.</param>
/// <param name="AverageEngagementScore">Средний балл вовлечённости.</param>
/// <param name="ChangePoints">Изменение балла к предыдущему периоду.</param>
/// <param name="SilentEmployees">Сотрудники без действий в окне.</param>
/// <param name="DecliningEmployees">Сотрудники со снижением вовлечённости.</param>
/// <param name="Formats">Сравнение форматов.</param>
public sealed record AiEngagementSnapshot(
    string Period,
    long ActiveEmployees,
    long ParticipatingEmployees,
    double? ParticipationRate,
    double? AverageEngagementScore,
    double? ChangePoints,
    long? SilentEmployees,
    long? DecliningEmployees,
    IReadOnlyList<AiFormatSnapshot> Formats);

/// <summary>Наблюдение правил аналитики, на которое опирается модель.</summary>
/// <param name="Code">Машинный код правила.</param>
/// <param name="Severity">Важность.</param>
/// <param name="Title">Заголовок.</param>
/// <param name="Description">Описание.</param>
public sealed record AiFinding(string Code, string Severity, string Title, string Description);

/// <summary>Существующая активность, которую можно рекомендовать.</summary>
/// <param name="Format">Формат: <c>event</c>, <c>quest</c> или <c>survey</c>.</param>
/// <param name="Id">Идентификатор активности.</param>
/// <param name="Title">Название.</param>
/// <param name="IsActive">Признак активности.</param>
/// <param name="Participants">Участники.</param>
/// <param name="Completed">Завершения.</param>
/// <param name="CompletionRate">Доля завершения.</param>
/// <param name="RewardAmount">Награда в баллах.</param>
/// <param name="StartsAt">Начало.</param>
/// <param name="EndsAt">Окончание.</param>
public sealed record AiActivityRow(
    string Format,
    long Id,
    string Title,
    bool IsActive,
    long Participants,
    long Completed,
    double? CompletionRate,
    long RewardAmount,
    DateTime? StartsAt,
    DateTime? EndsAt);

/// <summary>Готовая сводка вовлечённости для промпта.</summary>
/// <param name="Snapshot">Показатели.</param>
/// <param name="Findings">Наблюдения правил.</param>
/// <param name="Limitations">Ограничения интерпретации.</param>
/// <param name="Activities">Существующие активности для рекомендаций.</param>
/// <param name="Text">Текст сводки, передаваемый модели.</param>
public sealed record AiEngagementDigest(
    AiEngagementSnapshot Snapshot,
    IReadOnlyList<AiFinding> Findings,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<AiActivityRow> Activities,
    string Text);

/// <summary>Спрос по категории каталога.</summary>
/// <param name="Name">Название категории.</param>
/// <param name="Products">Количество товаров.</param>
/// <param name="Units">Заказанные единицы.</param>
/// <param name="UniqueBuyers">Уникальные покупатели.</param>
/// <param name="IsReliable">Признак достаточной выборки.</param>
public sealed record AiCategorySnapshot(
    string Name,
    long Products,
    long Units,
    long UniqueBuyers,
    bool IsReliable);

/// <summary>Строка товара каталога для рекомендаций.</summary>
/// <param name="ProductId">Идентификатор товара; <c>null</c> для удалённого.</param>
/// <param name="Name">Название товара.</param>
/// <param name="CategoryId">Идентификатор категории.</param>
/// <param name="Category">Категория.</param>
/// <param name="Price">Цена в баллах.</param>
/// <param name="Units">Заказанные единицы.</param>
/// <param name="UniqueBuyers">Уникальные покупатели.</param>
/// <param name="LiveCartUnits">Единицы в текущих корзинах.</param>
/// <param name="Stock">Остаток; <c>null</c> — без ограничения.</param>
/// <param name="SellThrough">Доля проданного от доступного.</param>
/// <param name="IsActive">Признак активности товара.</param>
/// <param name="IsOutOfStock">Признак нулевого остатка.</param>
/// <param name="IsDigital">Признак цифрового товара.</param>
public sealed record AiDemandRow(
    int? ProductId,
    string Name,
    int? CategoryId,
    string? Category,
    long Price,
    long Units,
    long UniqueBuyers,
    long LiveCartUnits,
    int? Stock,
    double? SellThrough,
    bool IsActive,
    bool IsOutOfStock,
    bool IsDigital);

/// <summary>Показатели каталога и заказов, рассчитанные Backend-ом.</summary>
/// <param name="TotalProducts">Всего товаров в каталоге.</param>
/// <param name="Orders">Заказы за окно.</param>
/// <param name="Units">Единицы товара за окно.</param>
/// <param name="UniqueBuyers">Уникальные покупатели.</param>
/// <param name="AverageOrderValue">Средняя стоимость заказа в баллах.</param>
/// <param name="PointsEarned">Начислено баллов за окно.</param>
/// <param name="PointsSpent">Списано баллов за окно.</param>
/// <param name="OutstandingPoints">Остаток на личных балансах.</param>
/// <param name="BurnRate">Отношение списанного к начисленному.</param>
/// <param name="Categories">Спрос по категориям.</param>
public sealed record AiCatalogSnapshot(
    long TotalProducts,
    long Orders,
    long Units,
    long UniqueBuyers,
    double? AverageOrderValue,
    long PointsEarned,
    long PointsSpent,
    long OutstandingPoints,
    double? BurnRate,
    IReadOnlyList<AiCategorySnapshot> Categories);

/// <summary>Готовая сводка каталога для промпта.</summary>
/// <param name="Snapshot">Показатели.</param>
/// <param name="TopDemand">Товары с наибольшим спросом.</param>
/// <param name="StockRisks">Товары с риском по остаткам или без спроса.</param>
/// <param name="Findings">Наблюдения правил.</param>
/// <param name="Limitations">Ограничения интерпретации.</param>
/// <param name="Text">Текст сводки, передаваемый модели.</param>
public sealed record AiProductDigest(
    AiCatalogSnapshot Snapshot,
    IReadOnlyList<AiDemandRow> TopDemand,
    IReadOnlyList<AiDemandRow> StockRisks,
    IReadOnlyList<AiFinding> Findings,
    IReadOnlyList<string> Limitations,
    string Text);

/// <summary>
/// Формирует текст сводки для промпта. В сводку попадают только агрегированные
/// показатели и названия товаров: имена, идентификаторы и контакты сотрудников
/// не передаются модели.
/// </summary>
public static class AiDigestWriter
{
    /// <summary>Формирует сводку вовлечённости.</summary>
    /// <param name="digest">Данные сводки.</param>
    /// <returns>Текст для блока <c>statistics</c>.</returns>
    public static string Engagement(AiEngagementDigest digest)
    {
        var snapshot = digest.Snapshot;
        var builder = new StringBuilder();

        builder.Append("Окно расчёта: ").AppendLine(snapshot.Period);
        builder.Append("Активные сотрудники: ").AppendLine(snapshot.ActiveEmployees.ToString());
        builder.Append("Участники активностей: ").AppendLine(snapshot.ParticipatingEmployees.ToString());
        builder.Append("Доля участия: ").AppendLine(Percent(snapshot.ParticipationRate));
        builder.Append("Средний балл вовлечённости: ").AppendLine(Number(snapshot.AverageEngagementScore));
        builder.Append("Изменение балла к предыдущему периоду: ").AppendLine(Signed(snapshot.ChangePoints));
        builder.Append("Сотрудники без действий в окне: ").AppendLine(Count(snapshot.SilentEmployees));
        builder.Append("Сотрудники со снижением вовлечённости: ").AppendLine(Count(snapshot.DecliningEmployees));

        builder.AppendLine("Форматы активности:");

        if (snapshot.Formats.Count == 0)
        {
            builder.AppendLine("- нет данных");
        }

        foreach (var format in snapshot.Formats)
        {
            builder
                .Append("- ")
                .Append(format.Title)
                .Append(" (").Append(format.Format).Append("): активностей ")
                .Append(format.Activities)
                .Append(", участников ").Append(format.Participants)
                .Append(", охват ").Append(Percent(format.ReachRate))
                .Append(", завершение ").Append(Percent(format.CompletionRate))
                .Append(", награда получена ").Append(Percent(format.RewardClaimRate))
                .Append(", выборка ").AppendLine(format.IsReliable ? "достаточна" : "недостаточна");
        }

        builder.AppendLine("Существующие активности (format:id):");

        if (digest.Activities.Count == 0)
        {
            builder.AppendLine("- нет данных");
        }

        foreach (var activity in digest.Activities)
        {
            builder
                .Append("- ").Append(activity.Format).Append(':').Append(activity.Id)
                .Append(" «").Append(activity.Title).Append("»")
                .Append(": участников ").Append(activity.Participants)
                .Append(", завершений ").Append(activity.Completed)
                .Append(", завершение ").Append(Percent(activity.CompletionRate))
                .Append(", награда ").Append(activity.RewardAmount)
                .Append(", активна: ").AppendLine(activity.IsActive ? "да" : "нет");
        }

        builder.AppendLine("Наблюдения правил аналитики:");

        if (digest.Findings.Count == 0)
        {
            builder.AppendLine("- правил, сработавших на данных, нет");
        }

        foreach (var finding in digest.Findings)
        {
            builder
                .Append("- [").Append(finding.Severity).Append("] ")
                .Append(finding.Code).Append(": ")
                .Append(finding.Title).Append(". ")
                .AppendLine(finding.Description);
        }

        builder.AppendLine("Ограничения интерпретации:");

        if (digest.Limitations.Count == 0)
        {
            builder.AppendLine("- нет");
        }

        foreach (var limitation in digest.Limitations)
        {
            builder.Append("- ").AppendLine(limitation);
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Формирует сводку каталога наград.</summary>
    /// <param name="digest">Данные сводки.</param>
    /// <returns>Текст для блока <c>statistics</c>.</returns>
    public static string Products(AiProductDigest digest)
    {
        var snapshot = digest.Snapshot;
        var builder = new StringBuilder();

        builder.Append("Товаров в каталоге: ").AppendLine(snapshot.TotalProducts.ToString());
        builder.Append("Заказы за окно: ").Append(snapshot.Orders)
            .Append(", единиц: ").Append(snapshot.Units)
            .Append(", покупателей: ").Append(snapshot.UniqueBuyers)
            .Append(", средний чек: ").AppendLine(Points(snapshot.AverageOrderValue));
        builder.Append("Баллы за окно: начислено ").Append(snapshot.PointsEarned)
            .Append(", списано ").Append(snapshot.PointsSpent)
            .Append(", остаток на личных балансах ").Append(snapshot.OutstandingPoints)
            .Append(", доля списания ").AppendLine(Percent(snapshot.BurnRate));

        builder.AppendLine("Категории:");

        if (snapshot.Categories.Count == 0)
        {
            builder.AppendLine("- нет данных");
        }

        foreach (var category in snapshot.Categories)
        {
            builder
                .Append("- ").Append(category.Name)
                .Append(": товаров ").Append(category.Products)
                .Append(", единиц ").Append(category.Units)
                .Append(", покупателей ").Append(category.UniqueBuyers)
                .Append(", выборка ").AppendLine(category.IsReliable ? "достаточна" : "недостаточна");
        }

        builder.AppendLine("Товары с наибольшим спросом:");

        if (digest.TopDemand.Count == 0)
        {
            builder.AppendLine("- спроса в окне нет");
        }

        foreach (var row in digest.TopDemand)
        {
            builder
                .Append("- productId ").Append(row.ProductId?.ToString() ?? "удалён")
                .Append(": ").Append(row.Name)
                .Append(" (").Append(row.Category ?? "без категории").Append(")")
                .Append(": ").Append(row.Price).Append(" баллов")
                .Append(", единиц ").Append(row.Units)
                .Append(", покупателей ").Append(row.UniqueBuyers)
                .Append(", в корзинах ").Append(row.LiveCartUnits)
                .Append(", остаток ").Append(row.Stock?.ToString() ?? "без ограничения")
                .Append(", активен: ").Append(row.IsActive ? "да" : "нет")
                .Append(", цифровой: ").AppendLine(row.IsDigital ? "да" : "нет");
        }

        builder.AppendLine("Товары с риском по остаткам или без спроса:");

        if (digest.StockRisks.Count == 0)
        {
            builder.AppendLine("- нет данных");
        }

        foreach (var row in digest.StockRisks)
        {
            builder
                .Append("- productId ").Append(row.ProductId?.ToString() ?? "удалён")
                .Append(": ").Append(row.Name)
                .Append(", остаток ").Append(row.Stock?.ToString() ?? "без ограничения")
                .Append(", единиц за окно ").Append(row.Units)
                .Append(", в корзинах ").Append(row.LiveCartUnits)
                .Append(", продано ").Append(Percent(row.SellThrough))
                .Append(", активен: ").AppendLine(row.IsActive ? "да" : "нет");
        }

        builder.AppendLine("Наблюдения правил аналитики:");

        if (digest.Findings.Count == 0)
        {
            builder.AppendLine("- правил, сработавших на данных, нет");
        }

        foreach (var finding in digest.Findings)
        {
            builder
                .Append("- [").Append(finding.Severity).Append("] ")
                .Append(finding.Code).Append(": ")
                .Append(finding.Title).Append(". ")
                .AppendLine(finding.Description);
        }

        builder.AppendLine("Ограничения интерпретации:");

        if (digest.Limitations.Count == 0)
        {
            builder.AppendLine("- нет");
        }

        foreach (var limitation in digest.Limitations)
        {
            builder.Append("- ").AppendLine(limitation);
        }

        return builder.ToString().TrimEnd();
    }

    private static string Percent(double? value) =>
        value is double number
            ? Math.Round(number * 100, 1).ToString("0.#", CultureInfo.InvariantCulture) + "%"
            : "нет данных";

    private static string Number(double? value) =>
        value is double number
            ? number.ToString("0.0", CultureInfo.InvariantCulture)
            : "нет данных";

    private static string Signed(double? value) =>
        value is double number
            ? number.ToString("+0.0;-0.0", CultureInfo.InvariantCulture)
            : "нет данных";

    private static string Count(long? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "нет данных";

    private static string Points(double? value) =>
        value is double number
            ? Math.Round(number, 0).ToString("0", CultureInfo.InvariantCulture) + " баллов"
            : "нет данных";
}
