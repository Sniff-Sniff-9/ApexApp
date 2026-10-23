using System.Text.Json;

namespace ApexApp.Api.DTOs
{
    /// <summary>
    /// Анкета для списка.
    /// </summary>
    /// <param name="Id">Идентификатор анкеты.</param>
    /// <param name="Title">Название анкеты.</param>
    /// <param name="Description">Описание анкеты.</param>
    /// <param name="StartAt">Дата и время начала.</param>
    /// <param name="EndAt">Дата и время окончания.</param>
    /// <param name="AllowMultipleResponses">Разрешено ли несколько ответов от одного пользователя.</param>
    /// <param name="QuestionsCount">Количество вопросов.</param>
    /// <param name="HasResponded">Признак того, что пользователь уже отвечал.</param>
    /// <param name="CanRespond">Признак того, что пользователь может отправить ответ.</param>
    /// <param name="LastSubmittedAt">Дата и время последнего ответа пользователя.</param>
    public sealed record SurveyListItemDto(
        long Id,
        string Title,
        string? Description,
        DateTime? StartAt,
        DateTime? EndAt,
        bool AllowMultipleResponses,
        int QuestionsCount,
        bool HasResponded,
        bool CanRespond,
        DateTime? LastSubmittedAt);

    /// <summary>
    /// Вопрос анкеты.
    /// </summary>
    /// <param name="Id">Идентификатор вопроса.</param>
    /// <param name="QuestionText">Текст вопроса.</param>
    /// <param name="QuestionType">Тип вопроса.</param>
    /// <param name="Options">Варианты ответа для типов-выборов.</param>
    /// <param name="IsRequired">Признак обязательного вопроса.</param>
    /// <param name="SortOrder">Порядок отображения.</param>
    public sealed record SurveyQuestionDto(
        long Id,
        string QuestionText,
        string QuestionType,
        IReadOnlyList<string> Options,
        bool IsRequired,
        int SortOrder);

    /// <summary>
    /// Сохранённый ответ на вопрос.
    /// </summary>
    /// <param name="QuestionId">Идентификатор вопроса.</param>
    /// <param name="Value">Значение ответа в формате JSON.</param>
    public sealed record SurveyAnswerDto(long QuestionId, JsonElement? Value);

    /// <summary>
    /// Ответ пользователя на анкету.
    /// </summary>
    /// <param name="Id">Идентификатор ответа.</param>
    /// <param name="SurveyId">Идентификатор анкеты.</param>
    /// <param name="UserId">Идентификатор пользователя.</param>
    /// <param name="SubmittedAt">Дата и время отправки.</param>
    /// <param name="Answers">Ответы на вопросы.</param>
    public sealed record SurveyResponseDto(
        long Id,
        long SurveyId,
        Guid UserId,
        DateTime SubmittedAt,
        IReadOnlyList<SurveyAnswerDto> Answers);

    /// <summary>
    /// Детали анкеты вместе с вопросами и ответами пользователя.
    /// </summary>
    /// <param name="Id">Идентификатор анкеты.</param>
    /// <param name="Title">Название анкеты.</param>
    /// <param name="Description">Описание анкеты.</param>
    /// <param name="IsActive">Признак активности анкеты.</param>
    /// <param name="StartAt">Дата и время начала.</param>
    /// <param name="EndAt">Дата и время окончания.</param>
    /// <param name="AllowMultipleResponses">Разрешено ли несколько ответов от одного пользователя.</param>
    /// <param name="CanRespond">Признак того, что пользователь может отправить ответ.</param>
    /// <param name="Questions">Вопросы в порядке отображения.</param>
    /// <param name="Responses">Ответы текущего пользователя.</param>
    public sealed record SurveyDetailsDto(
        long Id,
        string Title,
        string? Description,
        bool IsActive,
        DateTime? StartAt,
        DateTime? EndAt,
        bool AllowMultipleResponses,
        bool CanRespond,
        IReadOnlyList<SurveyQuestionDto> Questions,
        IReadOnlyList<SurveyResponseDto> Responses);

    /// <summary>
    /// Анкета в административном списке.
    /// </summary>
    /// <param name="Id">Идентификатор анкеты.</param>
    /// <param name="Title">Название анкеты.</param>
    /// <param name="Description">Описание анкеты.</param>
    /// <param name="IsActive">Признак активности анкеты.</param>
    /// <param name="StartAt">Дата и время начала.</param>
    /// <param name="EndAt">Дата и время окончания.</param>
    /// <param name="AllowMultipleResponses">Разрешено ли несколько ответов от одного пользователя.</param>
    /// <param name="QuestionsCount">Количество вопросов.</param>
    /// <param name="ResponsesCount">Количество ответов пользователей.</param>
    /// <param name="CreatedBy">Идентификатор создателя.</param>
    /// <param name="CreatedAt">Дата и время создания.</param>
    /// <param name="UpdatedAt">Дата и время последнего изменения.</param>
    public sealed record AdminSurveyDto(
        long Id,
        string Title,
        string? Description,
        bool IsActive,
        DateTime? StartAt,
        DateTime? EndAt,
        bool AllowMultipleResponses,
        int QuestionsCount,
        int ResponsesCount,
        Guid? CreatedBy,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
