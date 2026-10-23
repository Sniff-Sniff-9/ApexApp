using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Сценарии анкет: просмотр, отправка ответов и просмотр собственных ответов,
/// а также администрирование анкет, вопросов и результатов.
/// </summary>
public interface ISurveyService
{
    /// <summary>
    /// Возвращает страницу активных анкет, доступных в текущий момент,
    /// вместе с признаком наличия ответа пользователя.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<PagedResult<SurveySummary>> GetAvailableAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает детали анкеты вместе с вопросами и ответами пользователя.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<SurveyDetailsResult>> GetDetailsAsync(
        long surveyId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправляет ответы на анкету после проверки типов, обязательных вопросов
    /// и правила о повторных ответах.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="command">Ответы на вопросы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<SurveySubmissionResult>> SubmitAsync(
        long surveyId,
        Guid userId,
        SubmitSurveyCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает ответы пользователя на анкету в порядке от новых к старым.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<IReadOnlyList<SurveyResponseView>>> GetMyResponsesAsync(
        long surveyId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает страницу анкет для административного просмотра.
    /// </summary>
    /// <param name="isActive">Фильтр по признаку активности или <c>null</c> для всех анкет.</param>
    /// <param name="page">Номер страницы, начиная с 1.</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<PagedResult<AdminSurveySummary>> GetForAdministrationAsync(
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт анкету.
    /// </summary>
    /// <param name="command">Данные анкеты.</param>
    /// <param name="createdBy">Идентификатор создателя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<Survey>> CreateAsync(
        SurveyCommand command,
        Guid createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет анкету.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="command">Данные анкеты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<Survey>> UpdateAsync(
        long surveyId,
        SurveyCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Деактивирует анкету, сохраняя вопросы и ответы.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<Survey>> DeactivateAsync(
        long surveyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет вопрос в анкету.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="command">Данные вопроса.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<SurveyQuestion>> CreateQuestionAsync(
        long surveyId,
        SurveyQuestionCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет вопрос анкеты. Тип и варианты ответа нельзя изменить,
    /// если на вопрос уже есть ответы.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="questionId">Идентификатор вопроса.</param>
    /// <param name="command">Данные вопроса.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<SurveyQuestion>> UpdateQuestionAsync(
        long surveyId,
        long questionId,
        SurveyQuestionCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет вопрос анкеты, если на него ещё нет ответов.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="questionId">Идентификатор вопроса.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<SurveyQuestion>> DeleteQuestionAsync(
        long surveyId,
        long questionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает агрегированные результаты анкеты.
    /// </summary>
    /// <param name="surveyId">Идентификатор анкеты.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<ServiceResult<SurveyResultsResult>> GetResultsAsync(
        long surveyId,
        CancellationToken cancellationToken = default);
}
