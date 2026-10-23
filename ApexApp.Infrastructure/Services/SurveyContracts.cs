using System.Text.Json;
using ApexApp.Domain.Entities;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Поддерживаемые типы вопросов анкеты.
/// </summary>
/// <remarks>
/// Значения хранятся в текстовом столбце <c>survey_questions.question_type</c>.
/// Устаревшие значения <c>single</c>, <c>multiple</c> и <c>scale</c> читаются
/// как <c>single_choice</c>, <c>multiple_choice</c> и <c>number</c> соответственно.
/// </remarks>
public static class SurveyQuestionTypes
{
    public const string Text = "text";

    public const string SingleChoice = "single_choice";

    public const string MultipleChoice = "multiple_choice";

    public const string Number = "number";

    public const string Boolean = "boolean";

    public const string Rating = "rating";

    /// <summary>
    /// Проверяет, поддерживается ли тип вопроса.
    /// </summary>
    /// <param name="questionType">Тип вопроса.</param>
    public static bool IsSupported(string? questionType) =>
        questionType is Text or SingleChoice or MultipleChoice or Number or Boolean or Rating;

    /// <summary>
    /// Проверяет, является ли тип вопроса выбором из вариантов.
    /// </summary>
    /// <param name="questionType">Тип вопроса.</param>
    public static bool IsChoice(string questionType) =>
        questionType is SingleChoice or MultipleChoice;

    /// <summary>
    /// Приводит сохранённое значение к поддерживаемому типу вопроса.
    /// </summary>
    /// <param name="questionType">Значение из базы данных.</param>
    public static string Normalize(string questionType) => questionType switch
    {
        "single" => SingleChoice,
        "multiple" => MultipleChoice,
        "scale" => Number,
        _ => questionType
    };

    /// <summary>
    /// Разбирает <c>jsonb</c>-массив вариантов ответа.
    /// </summary>
    /// <param name="options">JSON-значение из базы данных.</param>
    public static IReadOnlyList<string> ParseOptions(string? options)
    {
        if (string.IsNullOrWhiteSpace(options))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(options);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var values = new List<string>();

            foreach (var item in document.RootElement.EnumerateArray())
            {
                values.Add(item.ValueKind == JsonValueKind.String
                    ? item.GetString()!
                    : item.GetRawText());
            }

            return values;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>
/// Вопрос анкеты вместе с разобранными вариантами ответа.
/// </summary>
/// <param name="Question">Вопрос.</param>
/// <param name="QuestionType">Поддерживаемый тип вопроса.</param>
/// <param name="Options">Варианты ответа для типов-выборов.</param>
public sealed record SurveyQuestionView(
    SurveyQuestion Question,
    string QuestionType,
    IReadOnlyList<string> Options);

/// <summary>
/// Анкета для списка.
/// </summary>
/// <param name="Survey">Анкета.</param>
/// <param name="QuestionsCount">Количество вопросов.</param>
/// <param name="HasResponded">Признак того, что пользователь уже отвечал.</param>
/// <param name="CanRespond">Признак того, что пользователь может отправить ответ.</param>
/// <param name="LastSubmittedAt">Дата и время последнего ответа пользователя.</param>
public sealed record SurveySummary(
    Survey Survey,
    int QuestionsCount,
    bool HasResponded,
    bool CanRespond,
    DateTime? LastSubmittedAt);

/// <summary>
/// Анкета в административном списке.
/// </summary>
/// <param name="Survey">Анкета.</param>
/// <param name="QuestionsCount">Количество вопросов.</param>
/// <param name="ResponsesCount">Количество ответов пользователей.</param>
public sealed record AdminSurveySummary(Survey Survey, int QuestionsCount, int ResponsesCount);

/// <summary>
/// Ответ пользователя вместе с сохранёнными значениями.
/// </summary>
/// <param name="Response">Запись ответа.</param>
/// <param name="Answers">Ответы на вопросы.</param>
public sealed record SurveyResponseView(
    SurveyResponse Response,
    IReadOnlyList<SurveyAnswer> Answers);

/// <summary>
/// Детали анкеты вместе с вопросами и ответами пользователя.
/// </summary>
/// <param name="Survey">Анкета.</param>
/// <param name="Questions">Вопросы в порядке отображения.</param>
/// <param name="Responses">Ответы текущего пользователя (сначала новые).</param>
/// <param name="CanRespond">Признак того, что пользователь может отправить ответ.</param>
public sealed record SurveyDetailsResult(
    Survey Survey,
    IReadOnlyList<SurveyQuestionView> Questions,
    IReadOnlyList<SurveyResponseView> Responses,
    bool CanRespond);

/// <summary>
/// Ответ на конкретный вопрос в запросе отправки анкеты.
/// </summary>
/// <param name="QuestionId">Идентификатор вопроса.</param>
/// <param name="Value">Значение ответа в формате JSON.</param>
public sealed record SurveyAnswerCommand(long QuestionId, JsonElement Value);

/// <summary>
/// Команда отправки ответов на анкету.
/// </summary>
/// <param name="Answers">Ответы на вопросы.</param>
public sealed record SubmitSurveyCommand(IReadOnlyList<SurveyAnswerCommand> Answers);

/// <summary>
/// Результат отправки ответов на анкету.
/// </summary>
/// <param name="Response">Созданный ответ.</param>
/// <param name="AnswersCount">Количество сохранённых ответов на вопросы.</param>
public sealed record SurveySubmissionResult(SurveyResponse Response, int AnswersCount);

/// <summary>
/// Количество ответов на вариант.
/// </summary>
/// <param name="Option">Вариант ответа.</param>
/// <param name="Count">Количество выборов.</param>
public sealed record SurveyOptionCount(string Option, int Count);

/// <summary>
/// Агрегированный результат по вопросу.
/// </summary>
/// <param name="QuestionId">Идентификатор вопроса.</param>
/// <param name="QuestionText">Текст вопроса.</param>
/// <param name="QuestionType">Тип вопроса.</param>
/// <param name="AnswerCount">Количество ответов на вопрос.</param>
/// <param name="OptionCounts">Количество выборов по вариантам.</param>
/// <param name="AverageValue">Среднее значение для числовых вопросов.</param>
public sealed record SurveyQuestionResult(
    long QuestionId,
    string QuestionText,
    string QuestionType,
    int AnswerCount,
    IReadOnlyList<SurveyOptionCount> OptionCounts,
    decimal? AverageValue);

/// <summary>
/// Результаты анкеты.
/// </summary>
/// <param name="Survey">Анкета.</param>
/// <param name="ResponseCount">Количество ответов на анкету.</param>
/// <param name="Questions">Результаты по вопросам.</param>
public sealed record SurveyResultsResult(
    Survey Survey,
    int ResponseCount,
    IReadOnlyList<SurveyQuestionResult> Questions);

/// <summary>
/// Данные для создания или обновления анкеты.
/// </summary>
/// <param name="Title">Название анкеты.</param>
/// <param name="Description">Описание анкеты.</param>
/// <param name="StartAt">Дата и время начала.</param>
/// <param name="EndAt">Дата и время окончания.</param>
/// <param name="AllowMultipleResponses">Разрешить несколько ответов от одного пользователя.</param>
/// <param name="IsActive">Признак активности анкеты.</param>
public sealed record SurveyCommand(
    string? Title,
    string? Description,
    DateTime? StartAt,
    DateTime? EndAt,
    bool AllowMultipleResponses,
    bool IsActive);

/// <summary>
/// Данные для создания или обновления вопроса анкеты.
/// </summary>
/// <param name="QuestionText">Текст вопроса.</param>
/// <param name="QuestionType">Тип вопроса.</param>
/// <param name="Options">Варианты ответа для типов-выборов.</param>
/// <param name="IsRequired">Признак обязательного вопроса.</param>
/// <param name="SortOrder">Порядок отображения.</param>
public sealed record SurveyQuestionCommand(
    string? QuestionText,
    string? QuestionType,
    IReadOnlyList<string>? Options,
    bool IsRequired,
    int SortOrder);
