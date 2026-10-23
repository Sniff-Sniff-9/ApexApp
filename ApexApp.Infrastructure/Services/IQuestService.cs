using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Пользовательские сценарии квестов: просмотр, участие, выполнение шагов и завершение квеста.
/// </summary>
public interface IQuestService
{
    /// <summary>
    /// Возвращает страницу активных квестов, доступных в текущий момент,
    /// вместе со статусом участия пользователя.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<PagedResult<QuestSummary>> GetAvailableAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает детали квеста вместе с шагами и участием пользователя.
    /// </summary>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<QuestDetailsResult>> GetDetailsAsync(
        long questId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает страницу участий пользователя в квестах с прогрессом выполнения.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<PagedResult<MyQuestSummary>> GetMyAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Присоединяет пользователя к квесту.
    /// Повторный запрос возвращает существующее участие без создания дубликата.
    /// </summary>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<QuestJoinResult>> JoinAsync(
        long questId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Завершает шаг квеста.
    /// Повторный запрос для выполненного шага не создаёт новое завершение.
    /// </summary>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="stepId">Идентификатор шага квеста.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="command">Данные шага: путь к доказательству и комментарий.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<QuestStepResult>> CompleteStepAsync(
        long questId,
        long stepId,
        Guid userId,
        CompleteQuestStepCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Завершает квест и атомарно начисляет награду, если все обязательные шаги выполнены,
    /// а необходимые доказательства одобрены.
    /// Повторный запрос не начисляет награду повторно; если квест уже завершён, но награда
    /// не была зафиксирована, повторный запрос безопасно довыдаёт её.
    /// </summary>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<QuestCompletionResult>> CompleteAsync(
        long questId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает страницу квестов для административного просмотра.
    /// </summary>
    /// <param name="isActive">Фильтр по признаку активности или <c>null</c> для всех квестов.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<PagedResult<AdminQuestSummary>> GetForAdministrationAsync(
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт квест.
    /// </summary>
    /// <param name="command">Данные квеста.</param>
    /// <param name="createdBy">Идентификатор создателя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<Quest>> CreateAsync(
        QuestCommand command,
        Guid createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет квест.
    /// </summary>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="command">Данные квеста.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<Quest>> UpdateAsync(
        long questId,
        QuestCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Деактивирует квест, сохраняя шаги, участия и доказательства.
    /// </summary>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<Quest>> DeactivateAsync(
        long questId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет шаг в квест.
    /// </summary>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="command">Данные шага.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<QuestStep>> CreateStepAsync(
        long questId,
        QuestStepCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет шаг квеста. Тип шага нельзя изменить, если у участников уже есть
    /// завершения этого шага.
    /// </summary>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="stepId">Идентификатор шага.</param>
    /// <param name="command">Данные шага.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<QuestStep>> UpdateStepAsync(
        long questId,
        long stepId,
        QuestStepCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет шаг квеста, если у участников ещё нет завершений этого шага.
    /// </summary>
    /// <param name="questId">Идентификатор квеста.</param>
    /// <param name="stepId">Идентификатор шага.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<QuestStep>> DeleteStepAsync(
        long questId,
        long stepId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает страницу доказательств для модерации: сначала ожидающие проверки.
    /// </summary>
    /// <param name="status">Фильтр по статусу или <c>null</c> для всех доказательств.</param>
    /// <param name="questId">Фильтр по квесту или <c>null</c> для всех квестов.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<PagedResult<QuestProofView>>> GetProofsAsync(
        string? status,
        long? questId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет доказательство, устанавливая статус <c>approved</c> или <c>rejected</c>.
    /// Повторная проверка с тем же статусом идемпотентна, с другим статусом — конфликт.
    /// </summary>
    /// <param name="proofId">Идентификатор доказательства.</param>
    /// <param name="command">Новый статус доказательства.</param>
    /// <param name="reviewedBy">Идентификатор проверяющего.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<QuestProofReviewResult>> ReviewProofAsync(
        long proofId,
        ReviewQuestProofCommand command,
        Guid reviewedBy,
        CancellationToken cancellationToken = default);
}
