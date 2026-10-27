namespace ApexApp.Api.DTOs
{
    /// <summary>
    /// Результат отправки ответов на анкету.
    /// </summary>
    /// <param name="SurveyId">Идентификатор анкеты.</param>
    /// <param name="ResponseId">Идентификатор созданного ответа.</param>
    /// <param name="SubmittedAt">Дата и время отправки.</param>
    /// <param name="AnswersCount">Количество сохранённых ответов на вопросы.</param>
    /// <param name="RewardAmount">Сумма награды.</param>
    public sealed record SubmitSurveyResultDto(
        long SurveyId,
        long ResponseId,
        DateTime SubmittedAt,
        int AnswersCount,
        long RewardAmount);

    /// <summary>
    /// Количество ответов на вариант.
    /// </summary>
    /// <param name="Option">Вариант ответа.</param>
    /// <param name="Count">Количество выборов.</param>
    public sealed record SurveyOptionCountDto(string Option, int Count);

    /// <summary>
    /// Результат по вопросу анкеты.
    /// </summary>
    /// <param name="QuestionId">Идентификатор вопроса.</param>
    /// <param name="QuestionText">Текст вопроса.</param>
    /// <param name="QuestionType">Тип вопроса.</param>
    /// <param name="AnswerCount">Количество ответов на вопрос.</param>
    /// <param name="OptionCounts">Количество выборов по вариантам.</param>
    /// <param name="AverageValue">Среднее значение для числовых вопросов.</param>
    public sealed record SurveyQuestionResultDto(
        long QuestionId,
        string QuestionText,
        string QuestionType,
        int AnswerCount,
        IReadOnlyList<SurveyOptionCountDto> OptionCounts,
        decimal? AverageValue);

    /// <summary>
    /// Результаты анкеты.
    /// </summary>
    /// <param name="SurveyId">Идентификатор анкеты.</param>
    /// <param name="Title">Название анкеты.</param>
    /// <param name="ResponseCount">Количество ответов на анкету.</param>
    /// <param name="Questions">Результаты по вопросам.</param>
    public sealed record SurveyResultsDto(
        long SurveyId,
        string Title,
        int ResponseCount,
        IReadOnlyList<SurveyQuestionResultDto> Questions);
}
