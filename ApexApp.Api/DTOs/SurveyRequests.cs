using System.Text.Json;

namespace ApexApp.Api.DTOs
{
    /// <summary>
    /// Ответ на вопрос анкеты.
    /// </summary>
    /// <param name="QuestionId">Идентификатор вопроса.</param>
    /// <param name="Value">
    /// Значение ответа: строка для <c>text</c>, <c>single_choice</c>; число для <c>number</c>, <c>rating</c>;
    /// логическое значение для <c>boolean</c>; массив строк для <c>multiple_choice</c>.
    /// </param>
    public sealed record SubmitSurveyAnswerRequest(long QuestionId, JsonElement? Value);

    /// <summary>
    /// Запрос на отправку ответов на анкету.
    /// </summary>
    /// <param name="Answers">Ответы на вопросы.</param>
    public sealed record SubmitSurveyRequest(IReadOnlyList<SubmitSurveyAnswerRequest>? Answers);

    /// <summary>
    /// Запрос на создание анкеты.
    /// </summary>
    /// <param name="Title">Название анкеты (обязательно, до 200 символов).</param>
    /// <param name="Description">Описание анкеты.</param>
    /// <param name="StartAt">Дата и время начала.</param>
    /// <param name="EndAt">Дата и время окончания (позже начала).</param>
    /// <param name="AllowMultipleResponses">Разрешить несколько ответов от одного пользователя.</param>
    /// <param name="IsActive">Признак активности анкеты.</param>
    public sealed record CreateSurveyRequest(
        string? Title,
        string? Description,
        DateTime? StartAt,
        DateTime? EndAt,
        bool AllowMultipleResponses,
        bool IsActive);

    /// <summary>
    /// Запрос на обновление анкеты.
    /// </summary>
    /// <param name="Title">Название анкеты (обязательно, до 200 символов).</param>
    /// <param name="Description">Описание анкеты.</param>
    /// <param name="StartAt">Дата и время начала.</param>
    /// <param name="EndAt">Дата и время окончания (позже начала).</param>
    /// <param name="AllowMultipleResponses">Разрешить несколько ответов от одного пользователя.</param>
    /// <param name="IsActive">Признак активности анкеты.</param>
    public sealed record UpdateSurveyRequest(
        string? Title,
        string? Description,
        DateTime? StartAt,
        DateTime? EndAt,
        bool AllowMultipleResponses,
        bool IsActive);

    /// <summary>
    /// Запрос на создание вопроса анкеты.
    /// </summary>
    /// <param name="QuestionText">Текст вопроса.</param>
    /// <param name="QuestionType">Тип вопроса: <c>text</c>, <c>single_choice</c>, <c>multiple_choice</c>, <c>number</c>, <c>boolean</c> или <c>rating</c>.</param>
    /// <param name="Options">Варианты ответа; обязательны для <c>single_choice</c> и <c>multiple_choice</c>.</param>
    /// <param name="IsRequired">Признак обязательного вопроса; по умолчанию <c>true</c>.</param>
    /// <param name="SortOrder">Порядок отображения; по умолчанию 0.</param>
    public sealed record CreateSurveyQuestionRequest(
        string? QuestionText,
        string? QuestionType,
        IReadOnlyList<string>? Options,
        bool? IsRequired,
        int? SortOrder);

    /// <summary>
    /// Запрос на обновление вопроса анкеты.
    /// </summary>
    /// <param name="QuestionText">Текст вопроса.</param>
    /// <param name="QuestionType">Тип вопроса: <c>text</c>, <c>single_choice</c>, <c>multiple_choice</c>, <c>number</c>, <c>boolean</c> или <c>rating</c>.</param>
    /// <param name="Options">Варианты ответа; обязательны для <c>single_choice</c> и <c>multiple_choice</c>.</param>
    /// <param name="IsRequired">Признак обязательного вопроса; по умолчанию <c>true</c>.</param>
    /// <param name="SortOrder">Порядок отображения; по умолчанию 0.</param>
    public sealed record UpdateSurveyQuestionRequest(
        string? QuestionText,
        string? QuestionType,
        IReadOnlyList<string>? Options,
        bool? IsRequired,
        int? SortOrder);
}
