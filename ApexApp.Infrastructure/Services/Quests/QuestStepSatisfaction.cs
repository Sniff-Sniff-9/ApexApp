using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services.Quests;

/// <summary>
/// Единое правило подтверждённого выполнения шага. Используется и сервисом квестов,
/// и контроллером, чтобы представление прогресса не расходилось с проверкой завершения.
/// </summary>
public static class QuestStepSatisfaction
{
    /// <summary>
    /// Проверяет, подтверждено ли выполнение шага по сохранённым завершениям.
    /// </summary>
    /// <param name="step">Шаг квеста.</param>
    /// <param name="completions">Завершения участия.</param>
    /// <returns><c>true</c>, если шаг выполнен.</returns>
    public static bool IsSatisfied(
        QuestStep step,
        IEnumerable<QuestStepCompletion> completions) =>
        step.CompletionType switch
        {
            // Ручной шаг подтверждается отметкой пользователя.
            QuestStepCompletionType.Manual => completions
                .Any(completion => completion.StepId == step.Id),

            // Шаг с доказательством подтверждается только одобренным доказательством.
            QuestStepCompletionType.Proof => completions
                .Where(completion => completion.StepId == step.Id)
                .Select(LatestProof)
                .Any(proof => proof?.Status == QuestProofStatuses.Approved),

            // Автоматический шаг подтверждается только серверной проверкой,
            // которая создаёт завершение. Фронтенд не может его создать.
            QuestStepCompletionType.Automatic => completions
                .Any(completion => completion.StepId == step.Id),

            _ => false
        };

    /// <summary>
    /// Возвращает последнее доказательство завершения шага.
    /// </summary>
    /// <param name="completion">Завершение шага.</param>
    /// <returns>Последнее доказательство или <c>null</c>.</returns>
    public static QuestProof? LatestProof(QuestStepCompletion completion) =>
        completion.QuestProofs
            .OrderByDescending(proof => proof.CreatedAt)
            .ThenByDescending(proof => proof.Id)
            .FirstOrDefault();
}
