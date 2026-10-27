using System.Data;
using System.Text.Json;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Caching;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexApp.Infrastructure.Services;

/// <summary>
/// Сценарии анкет: список и детали, отправка ответов, собственные ответы,
/// администрирование анкет и вопросов, а также агрегированные результаты.
/// </summary>
public sealed class SurveyService : ISurveyService
{
    private readonly AppDbContext dbContext;
    private readonly ActivityRewardGranter activityRewardGranter;
    private readonly ICacheService cache;

    private const int TitleMaxLength = 200;
    private const int TextAnswerMaxLength = 4000;

    public SurveyService(
        AppDbContext dbContext,
        ActivityRewardGranter activityRewardGranter,
        ICacheService cache)
    {
        this.dbContext = dbContext;
        this.activityRewardGranter = activityRewardGranter;
        this.cache = cache;
    }

    /// <inheritdoc />
    public async Task<PagedResult<SurveySummary>> GetAvailableAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var now = DateTime.UtcNow;

        var query = dbContext.Surveys
            .AsNoTracking()
            .Where(survey => survey.IsActive
                             && (survey.StartAt == null || survey.StartAt <= now)
                             && (survey.EndAt == null || survey.EndAt >= now));

        var totalCount = await query.CountAsync(cancellationToken);

        var surveys = await query
            .OrderByDescending(survey => survey.CreatedAt)
            .ThenByDescending(survey => survey.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (surveys.Count == 0)
        {
            return new PagedResult<SurveySummary>([], totalCount, page, pageSize);
        }

        var surveyIds = surveys.Select(survey => survey.Id).ToList();

        var questionCounts = await dbContext.SurveyQuestions
            .AsNoTracking()
            .Where(question => surveyIds.Contains(question.SurveyId))
            .GroupBy(question => question.SurveyId)
            .Select(group => new { SurveyId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.SurveyId, row => row.Count, cancellationToken);

        var lastSubmissions = await dbContext.SurveyResponses
            .AsNoTracking()
            .Where(response => response.UserId == userId && surveyIds.Contains(response.SurveyId))
            .GroupBy(response => response.SurveyId)
            .Select(group => new
            {
                SurveyId = group.Key,
                SubmittedAt = (DateTime?)group.Max(response => response.SubmittedAt)
            })
            .ToDictionaryAsync(row => row.SurveyId, row => row.SubmittedAt, cancellationToken);

        var items = surveys.Select(survey =>
        {
            var hasResponded = lastSubmissions.TryGetValue(survey.Id, out var submittedAt);

            return new SurveySummary(
                survey,
                questionCounts.TryGetValue(survey.Id, out var questionsCount) ? questionsCount : 0,
                hasResponded,
                IsOpen(survey, now) && (survey.AllowMultipleResponses || !hasResponded),
                submittedAt);
        }).ToList();

        return new PagedResult<SurveySummary>(items, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<SurveyDetailsResult>> GetDetailsAsync(
        long surveyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0)
        {
            return ServiceResult<SurveyDetailsResult>.Fail(400, "A valid survey ID is required.");
        }

        var survey = await dbContext.Surveys
            .AsNoTracking()
            .Include(item => item.SurveyQuestions)
            .FirstOrDefaultAsync(item => item.Id == surveyId, cancellationToken);

        if (survey is null)
        {
            return ServiceResult<SurveyDetailsResult>.Fail(404, "Survey was not found.");
        }

        var responses = await LoadOwnResponsesAsync(surveyId, userId, cancellationToken);
        var isOpen = IsOpen(survey, DateTime.UtcNow);

        // Как и для остальных активностей, закрытая анкета скрывается от пользователя,
        // который ещё не отвечал, но остаётся доступной для просмотра своих ответов.
        if (responses.Count == 0 && !isOpen)
        {
            return ServiceResult<SurveyDetailsResult>.Fail(
                409,
                "Survey is not active or outside its date range.");
        }

        var questions = survey.SurveyQuestions
            .OrderBy(question => question.SortOrder)
            .ThenBy(question => question.Id)
            .Select(ToView)
            .ToList();

        var canRespond = isOpen && (survey.AllowMultipleResponses || responses.Count == 0);

        return ServiceResult<SurveyDetailsResult>.Ok(
            new SurveyDetailsResult(survey, questions, responses, canRespond));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<IReadOnlyList<SurveyResponseView>>> GetMyResponsesAsync(
        long surveyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0)
        {
            return ServiceResult<IReadOnlyList<SurveyResponseView>>.Fail(
                400,
                "A valid survey ID is required.");
        }

        var surveyExists = await dbContext.Surveys
            .AsNoTracking()
            .AnyAsync(survey => survey.Id == surveyId, cancellationToken);

        if (!surveyExists)
        {
            return ServiceResult<IReadOnlyList<SurveyResponseView>>.Fail(404, "Survey was not found.");
        }

        var responses = await LoadOwnResponsesAsync(surveyId, userId, cancellationToken);

        return ServiceResult<IReadOnlyList<SurveyResponseView>>.Ok(responses);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<SurveySubmissionResult>> SubmitAsync(
        long surveyId,
        Guid userId,
        SubmitSurveyCommand command,
        CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0)
        {
            return ServiceResult<SurveySubmissionResult>.Fail(400, "A valid survey ID is required.");
        }

        if (userId == Guid.Empty)
        {
            return ServiceResult<SurveySubmissionResult>.Fail(400, "A valid user ID is required.");
        }

        if (command?.Answers is null)
        {
            return ServiceResult<SurveySubmissionResult>.Fail(400, "Answers are required.");
        }

        var survey = await dbContext.Surveys
            .AsNoTracking()
            .Include(item => item.SurveyQuestions)
            .FirstOrDefaultAsync(item => item.Id == surveyId, cancellationToken);

        if (survey is null)
        {
            return ServiceResult<SurveySubmissionResult>.Fail(404, "Survey was not found.");
        }

        if (!IsOpen(survey, DateTime.UtcNow))
        {
            return ServiceResult<SurveySubmissionResult>.Fail(
                409,
                "Survey is not active or outside its date range.");
        }

        var questions = survey.SurveyQuestions
            .OrderBy(question => question.SortOrder)
            .ThenBy(question => question.Id)
            .Select(ToView)
            .ToDictionary(view => view.Question.Id);

        var provided = new Dictionary<long, JsonElement>();

        foreach (var answer in command.Answers)
        {
            if (!questions.ContainsKey(answer.QuestionId))
            {
                return ServiceResult<SurveySubmissionResult>.Fail(
                    400,
                    $"Question {answer.QuestionId} does not belong to this survey.");
            }

            if (!provided.TryAdd(answer.QuestionId, answer.Value))
            {
                return ServiceResult<SurveySubmissionResult>.Fail(
                    400,
                    $"Question {answer.QuestionId} has multiple answers.");
            }
        }

        var prepared = new List<(SurveyQuestionView Question, string Json)>();
        var missing = new List<long>();

        foreach (var question in questions.Values)
        {
            var hasAnswer = provided.TryGetValue(question.Question.Id, out var value);

            if (!hasAnswer || IsEmptyAnswer(question.QuestionType, value))
            {
                if (question.Question.IsRequired)
                {
                    missing.Add(question.Question.Id);
                }

                continue;
            }

            var (json, error) = ValidateAnswer(question, value);

            if (error is not null)
            {
                return ServiceResult<SurveySubmissionResult>.Fail(
                    400,
                    $"Question {question.Question.Id}: {error}");
            }

            prepared.Add((question, json!));
        }

        if (missing.Count > 0)
        {
            return ServiceResult<SurveySubmissionResult>.Fail(
                400,
                $"Answers are required for questions: {string.Join(", ", missing)}.");
        }

        // Serializable-транзакция не позволяет двум запросам одновременно создать
        // первый ответ, когда повторные ответы запрещены.
        await using var databaseTransaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        SurveyResponse? response = null;

        try
        {
            var alreadyResponded = await dbContext.SurveyResponses
                .AnyAsync(
                    item => item.SurveyId == surveyId && item.UserId == userId,
                    cancellationToken);

            if (alreadyResponded && !survey.AllowMultipleResponses)
            {
                return ServiceResult<SurveySubmissionResult>.Fail(
                    409,
                    "You have already responded to this survey.");
            }

            var now = DateTime.UtcNow;

            response = new SurveyResponse
            {
                SurveyId = surveyId,
                UserId = userId,
                SubmittedAt = now
            };

            dbContext.SurveyResponses.Add(response);

            foreach (var (question, json) in prepared)
            {
                dbContext.SurveyAnswers.Add(new SurveyAnswer
                {
                    SurveyResponse = response,
                    SurveyId = surveyId,
                    QuestionId = question.Question.Id,
                    Answer = json,
                    CreatedAt = now
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // Начисляем награду после сохранения ответа, чтобы response.Id уже был известен.
            if (survey.RewardAmount > 0)
            {
                var credit = await activityRewardGranter.GrantAsync(
                    userId,
                    survey.RewardAmount,
                    $"Награда за прохождение анкеты \"{survey.Title}\"",
                    ActivityRewardEntityTypes.Survey,
                    response.Id,
                    cancellationToken);

                if (!credit.IsSuccess)
                {
                    await databaseTransaction.RollbackAsync(cancellationToken);

                    return ServiceResult<SurveySubmissionResult>.Fail(
                        credit.StatusCode,
                        credit.Error!);
                }
            }

            await databaseTransaction.CommitAsync(cancellationToken);

            cache.Remove(CacheKeys.SurveyResults(surveyId));
        }
        catch (DbUpdateException exception) when (DatabaseErrors.IsUniqueViolation(exception))
        {
            await databaseTransaction.RollbackAsync(cancellationToken);

            if (response is not null)
            {
                dbContext.Entry(response).State = EntityState.Detached;
            }

            return ServiceResult<SurveySubmissionResult>.Fail(
                409,
                "You have already responded to this survey.");
        }
        catch (Exception exception) when (DatabaseErrors.IsRetryableConflict(exception))
        {
            await databaseTransaction.RollbackAsync(cancellationToken);

            return ServiceResult<SurveySubmissionResult>.Fail(
                409,
                "The survey submission conflicted with another request. Please retry.");
        }

        return ServiceResult<SurveySubmissionResult>.Ok(
            new SurveySubmissionResult(response!, prepared.Count, survey.RewardAmount));
    }

    /// <inheritdoc />
    public async Task<PagedResult<AdminSurveySummary>> GetForAdministrationAsync(
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = dbContext.Surveys.AsNoTracking();

        if (isActive is bool active)
        {
            query = query.Where(survey => survey.IsActive == active);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var surveys = await query
            .OrderByDescending(survey => survey.CreatedAt)
            .ThenByDescending(survey => survey.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (surveys.Count == 0)
        {
            return new PagedResult<AdminSurveySummary>([], totalCount, page, pageSize);
        }

        var surveyIds = surveys.Select(survey => survey.Id).ToList();

        var questionCounts = await dbContext.SurveyQuestions
            .AsNoTracking()
            .Where(question => surveyIds.Contains(question.SurveyId))
            .GroupBy(question => question.SurveyId)
            .Select(group => new { SurveyId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.SurveyId, row => row.Count, cancellationToken);

        var responseCounts = await dbContext.SurveyResponses
            .AsNoTracking()
            .Where(response => surveyIds.Contains(response.SurveyId))
            .GroupBy(response => response.SurveyId)
            .Select(group => new { SurveyId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.SurveyId, row => row.Count, cancellationToken);

        var items = surveys
            .Select(survey => new AdminSurveySummary(
                survey,
                questionCounts.TryGetValue(survey.Id, out var questions) ? questions : 0,
                responseCounts.TryGetValue(survey.Id, out var responses) ? responses : 0))
            .ToList();

        return new PagedResult<AdminSurveySummary>(items, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Survey>> CreateAsync(
        SurveyCommand command,
        Guid createdBy,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
        {
            return ServiceResult<Survey>.Fail(400, "A survey command is required.");
        }

        if (createdBy == Guid.Empty)
        {
            return ServiceResult<Survey>.Fail(400, "A valid creator ID is required.");
        }

        var validationError = ValidateSurveyFields(command);

        if (validationError is not null)
        {
            return ServiceResult<Survey>.Fail(400, validationError);
        }

        var now = DateTime.UtcNow;

        var survey = new Survey
        {
            Title = command.Title!.Trim(),
            Description = NormalizeOptional(command.Description),
            RewardAmount = command.RewardAmount,
            StartAt = ToUtc(command.StartAt),
            EndAt = ToUtc(command.EndAt),
            AllowMultipleResponses = command.AllowMultipleResponses,
            IsActive = command.IsActive,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Surveys.Add(survey);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.IsForeignKeyViolation(exception))
        {
            dbContext.Entry(survey).State = EntityState.Detached;

            return ServiceResult<Survey>.Fail(400, "The survey creator was not found.");
        }

        return ServiceResult<Survey>.Ok(survey);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Survey>> UpdateAsync(
        long surveyId,
        SurveyCommand command,
        CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0)
        {
            return ServiceResult<Survey>.Fail(400, "A valid survey ID is required.");
        }

        if (command is null)
        {
            return ServiceResult<Survey>.Fail(400, "A survey command is required.");
        }

        var survey = await dbContext.Surveys
            .FirstOrDefaultAsync(item => item.Id == surveyId, cancellationToken);

        if (survey is null)
        {
            return ServiceResult<Survey>.Fail(404, "Survey was not found.");
        }

        var validationError = ValidateSurveyFields(command);

        if (validationError is not null)
        {
            return ServiceResult<Survey>.Fail(400, validationError);
        }

        survey.Title = command.Title!.Trim();
        survey.Description = NormalizeOptional(command.Description);
        survey.RewardAmount = command.RewardAmount;
        survey.StartAt = ToUtc(command.StartAt);
        survey.EndAt = ToUtc(command.EndAt);
        survey.AllowMultipleResponses = command.AllowMultipleResponses;
        survey.IsActive = command.IsActive;
        survey.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKeys.SurveyResults(surveyId));

        return ServiceResult<Survey>.Ok(survey);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<Survey>> DeactivateAsync(
        long surveyId,
        CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0)
        {
            return ServiceResult<Survey>.Fail(400, "A valid survey ID is required.");
        }

        var survey = await dbContext.Surveys
            .FirstOrDefaultAsync(item => item.Id == surveyId, cancellationToken);

        if (survey is null)
        {
            return ServiceResult<Survey>.Fail(404, "Survey was not found.");
        }

        if (survey.IsActive)
        {
            // Анкета не удаляется физически: вопросы и ответы остаются частью истории.
            survey.IsActive = false;
            survey.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            cache.Remove(CacheKeys.SurveyResults(surveyId));
        }

        return ServiceResult<Survey>.Ok(survey);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<SurveyQuestion>> CreateQuestionAsync(
        long surveyId,
        SurveyQuestionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0)
        {
            return ServiceResult<SurveyQuestion>.Fail(400, "A valid survey ID is required.");
        }

        if (command is null)
        {
            return ServiceResult<SurveyQuestion>.Fail(400, "A question command is required.");
        }

        var surveyExists = await dbContext.Surveys
            .AsNoTracking()
            .AnyAsync(survey => survey.Id == surveyId, cancellationToken);

        if (!surveyExists)
        {
            return ServiceResult<SurveyQuestion>.Fail(404, "Survey was not found.");
        }

        var validated = ValidateQuestionCommand(command);

        if (validated.Error is not null)
        {
            return ServiceResult<SurveyQuestion>.Fail(400, validated.Error);
        }

        var question = new SurveyQuestion
        {
            SurveyId = surveyId,
            QuestionText = validated.QuestionText!,
            QuestionType = validated.QuestionType!,
            Options = validated.OptionsJson,
            IsRequired = command.IsRequired,
            SortOrder = command.SortOrder,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.SurveyQuestions.Add(question);

        await dbContext.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKeys.SurveyResults(surveyId));

        return ServiceResult<SurveyQuestion>.Ok(question);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<SurveyQuestion>> UpdateQuestionAsync(
        long surveyId,
        long questionId,
        SurveyQuestionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0 || questionId <= 0)
        {
            return ServiceResult<SurveyQuestion>.Fail(
                400,
                "Valid survey and question IDs are required.");
        }

        if (command is null)
        {
            return ServiceResult<SurveyQuestion>.Fail(400, "A question command is required.");
        }

        var surveyExists = await dbContext.Surveys
            .AsNoTracking()
            .AnyAsync(survey => survey.Id == surveyId, cancellationToken);

        if (!surveyExists)
        {
            return ServiceResult<SurveyQuestion>.Fail(404, "Survey was not found.");
        }

        var question = await dbContext.SurveyQuestions
            .FirstOrDefaultAsync(
                item => item.Id == questionId && item.SurveyId == surveyId,
                cancellationToken);

        if (question is null)
        {
            return ServiceResult<SurveyQuestion>.Fail(404, "Survey question was not found.");
        }

        var validated = ValidateQuestionCommand(command);

        if (validated.Error is not null)
        {
            return ServiceResult<SurveyQuestion>.Fail(400, validated.Error);
        }

        var changesTypeOrOptions =
            !string.Equals(
                SurveyQuestionTypes.Normalize(question.QuestionType),
                validated.QuestionType,
                StringComparison.Ordinal)
            || !string.Equals(question.Options, validated.OptionsJson, StringComparison.Ordinal);

        if (changesTypeOrOptions)
        {
            var hasAnswers = await dbContext.SurveyAnswers
                .AsNoTracking()
                .AnyAsync(
                    answer => answer.QuestionId == questionId && answer.SurveyId == surveyId,
                    cancellationToken);

            if (hasAnswers)
            {
                return ServiceResult<SurveyQuestion>.Fail(
                    409,
                    "The question already has answers, so its type and options cannot be changed.");
            }
        }

        question.QuestionText = validated.QuestionText!;
        question.QuestionType = validated.QuestionType!;
        question.Options = validated.OptionsJson;
        question.IsRequired = command.IsRequired;
        question.SortOrder = command.SortOrder;

        await dbContext.SaveChangesAsync(cancellationToken);

        cache.Remove(CacheKeys.SurveyResults(surveyId));

        return ServiceResult<SurveyQuestion>.Ok(question);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<SurveyQuestion>> DeleteQuestionAsync(
        long surveyId,
        long questionId,
        CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0 || questionId <= 0)
        {
            return ServiceResult<SurveyQuestion>.Fail(
                400,
                "Valid survey and question IDs are required.");
        }

        var surveyExists = await dbContext.Surveys
            .AsNoTracking()
            .AnyAsync(survey => survey.Id == surveyId, cancellationToken);

        if (!surveyExists)
        {
            return ServiceResult<SurveyQuestion>.Fail(404, "Survey was not found.");
        }

        var question = await dbContext.SurveyQuestions
            .FirstOrDefaultAsync(
                item => item.Id == questionId && item.SurveyId == surveyId,
                cancellationToken);

        if (question is null)
        {
            return ServiceResult<SurveyQuestion>.Fail(404, "Survey question was not found.");
        }

        var hasAnswers = await dbContext.SurveyAnswers
            .AsNoTracking()
            .AnyAsync(
                answer => answer.QuestionId == questionId && answer.SurveyId == surveyId,
                cancellationToken);

        if (hasAnswers)
        {
            return ServiceResult<SurveyQuestion>.Fail(
                409,
                "The question already has answers and cannot be deleted.");
        }

        dbContext.SurveyQuestions.Remove(question);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseErrors.IsForeignKeyViolation(exception))
        {
            return ServiceResult<SurveyQuestion>.Fail(
                409,
                "The question is referenced by other records and cannot be deleted.");
        }

        cache.Remove(CacheKeys.SurveyResults(surveyId));

        return ServiceResult<SurveyQuestion>.Ok(question);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<SurveyResultsResult>> GetResultsAsync(
        long surveyId,
        CancellationToken cancellationToken = default)
    {
        if (surveyId <= 0)
        {
            return ServiceResult<SurveyResultsResult>.Fail(400, "A valid survey ID is required.");
        }

        var cacheKey = CacheKeys.SurveyResults(surveyId);

        if (cache.TryGet<SurveyResultsResult>(cacheKey, out var cached) &&
            cached is not null)
        {
            return ServiceResult<SurveyResultsResult>.Ok(cached);
        }

        var survey = await dbContext.Surveys
            .AsNoTracking()
            .Include(item => item.SurveyQuestions)
            .FirstOrDefaultAsync(item => item.Id == surveyId, cancellationToken);

        if (survey is null)
        {
            return ServiceResult<SurveyResultsResult>.Fail(404, "Survey was not found.");
        }

        var responseCount = await dbContext.SurveyResponses
            .AsNoTracking()
            .CountAsync(response => response.SurveyId == surveyId, cancellationToken);

        var answers = await dbContext.SurveyAnswers
            .AsNoTracking()
            .Where(answer => answer.SurveyId == surveyId)
            .ToListAsync(cancellationToken);

        var answersByQuestion = answers
            .GroupBy(answer => answer.QuestionId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var questions = new List<SurveyQuestionResult>();

        foreach (var view in survey.SurveyQuestions
                     .OrderBy(question => question.SortOrder)
                     .ThenBy(question => question.Id)
                     .Select(ToView))
        {
            answersByQuestion.TryGetValue(view.Question.Id, out var questionAnswers);
            questionAnswers ??= [];

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var option in view.Options)
            {
                counts[option] = 0;
            }

            if (view.QuestionType == SurveyQuestionTypes.Boolean)
            {
                counts["true"] = 0;
                counts["false"] = 0;
            }

            decimal sum = 0;
            var numericCount = 0;

            foreach (var answer in questionAnswers)
            {
                using var document = ParseAnswer(answer.Answer);

                if (document is null)
                {
                    continue;
                }

                var value = document.RootElement;

                switch (view.QuestionType)
                {
                    case SurveyQuestionTypes.SingleChoice:
                        if (value.ValueKind == JsonValueKind.String)
                        {
                            Increment(counts, value.GetString()!);
                        }

                        break;

                    case SurveyQuestionTypes.MultipleChoice:
                        if (value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in value.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String)
                                {
                                    Increment(counts, item.GetString()!);
                                }
                            }
                        }

                        break;

                    case SurveyQuestionTypes.Boolean:
                        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        {
                            Increment(
                                counts,
                                value.ValueKind == JsonValueKind.True ? "true" : "false");
                        }

                        break;

                    case SurveyQuestionTypes.Number:
                    case SurveyQuestionTypes.Rating:
                        if (value.ValueKind == JsonValueKind.Number
                            && value.TryGetDecimal(out var number))
                        {
                            sum += number;
                            numericCount++;
                        }

                        break;
                }
            }

            questions.Add(new SurveyQuestionResult(
                view.Question.Id,
                view.Question.QuestionText,
                view.QuestionType,
                questionAnswers.Count,
                counts
                    .Select(pair => new SurveyOptionCount(pair.Key, pair.Value))
                    .ToList(),
                numericCount > 0 ? decimal.Round(sum / numericCount, 2) : null));
        }

        var results = new SurveyResultsResult(survey, responseCount, questions);

        cache.Set(cacheKey, results, CacheKeys.SurveyResultsTtl);

        return ServiceResult<SurveyResultsResult>.Ok(results);
    }

    /// <summary>
    /// Загружает ответы пользователя на анкету вместе с ответами на вопросы.
    /// </summary>
    private async Task<IReadOnlyList<SurveyResponseView>> LoadOwnResponsesAsync(
        long surveyId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var responses = await dbContext.SurveyResponses
            .AsNoTracking()
            .Where(response => response.SurveyId == surveyId && response.UserId == userId)
            .Include(response => response.SurveyAnswers)
            .OrderByDescending(response => response.SubmittedAt)
            .ThenByDescending(response => response.Id)
            .ToListAsync(cancellationToken);

        return responses
            .Select(response => new SurveyResponseView(
                response,
                response.SurveyAnswers.OrderBy(answer => answer.QuestionId).ToList()))
            .ToList();
    }

    /// <summary>
    /// Создаёт представление вопроса с нормализованным типом и вариантами ответа.
    /// </summary>
    private static SurveyQuestionView ToView(SurveyQuestion question)
    {
        var questionType = SurveyQuestionTypes.Normalize(question.QuestionType);

        return new SurveyQuestionView(question, questionType, SurveyQuestionTypes.ParseOptions(question.Options));
    }

    /// <summary>
    /// Проверяет значение ответа и возвращает JSON для сохранения либо сообщение об ошибке.
    /// </summary>
    private static (string? Json, string? Error) ValidateAnswer(
        SurveyQuestionView question,
        JsonElement value)
    {
        switch (question.QuestionType)
        {
            case SurveyQuestionTypes.Text:
            {
                if (value.ValueKind != JsonValueKind.String)
                {
                    return (null, "the answer must be a string.");
                }

                var text = value.GetString()!.Trim();

                if (text.Length == 0)
                {
                    return (null, "the answer must not be empty.");
                }

                if (text.Length > TextAnswerMaxLength)
                {
                    return (null, $"the answer must not exceed {TextAnswerMaxLength} characters.");
                }

                return (JsonSerializer.Serialize(text), null);
            }

            case SurveyQuestionTypes.SingleChoice:
            {
                if (value.ValueKind != JsonValueKind.String)
                {
                    return (null, "the answer must be a string.");
                }

                var selected = value.GetString()!;

                if (!question.Options.Contains(selected, StringComparer.Ordinal))
                {
                    return (null, "the answer must be one of the question options.");
                }

                return (JsonSerializer.Serialize(selected), null);
            }

            case SurveyQuestionTypes.MultipleChoice:
            {
                if (value.ValueKind != JsonValueKind.Array)
                {
                    return (null, "the answer must be an array of options.");
                }

                var selected = new List<string>();

                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        return (null, "every selected option must be a string.");
                    }

                    var option = item.GetString()!;

                    if (!question.Options.Contains(option, StringComparer.Ordinal))
                    {
                        return (null, "every selected option must be one of the question options.");
                    }

                    if (selected.Contains(option, StringComparer.Ordinal))
                    {
                        return (null, "selected options must not repeat.");
                    }

                    selected.Add(option);
                }

                if (selected.Count == 0)
                {
                    return (null, "at least one option must be selected.");
                }

                return (JsonSerializer.Serialize(selected), null);
            }

            case SurveyQuestionTypes.Number:
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out _))
                {
                    return (null, "the answer must be a number.");
                }

                return (value.GetRawText(), null);
            }

            case SurveyQuestionTypes.Boolean:
            {
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return (null, "the answer must be a boolean.");
                }

                return (value.ValueKind == JsonValueKind.True ? "true" : "false", null);
            }

            case SurveyQuestionTypes.Rating:
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var rating))
                {
                    return (null, "the answer must be an integer.");
                }

                if (rating < 1)
                {
                    return (null, "the rating must be greater than or equal to 1.");
                }

                return (value.GetRawText(), null);
            }

            default:
                return (null, "the question type is not supported.");
        }
    }

    /// <summary>
    /// Проверяет, считается ли значение пустым ответом.
    /// </summary>
    private static bool IsEmptyAnswer(string questionType, JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        return questionType switch
        {
            SurveyQuestionTypes.Text =>
                value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()),
            SurveyQuestionTypes.MultipleChoice =>
                value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0,
            _ => false
        };
    }

    /// <summary>
    /// Проверяет данные вопроса и возвращает нормализованные значения.
    /// </summary>
    private static (string? Error, string? QuestionText, string? QuestionType, string? OptionsJson)
        ValidateQuestionCommand(SurveyQuestionCommand command)
    {
        var questionText = command.QuestionText?.Trim();

        if (string.IsNullOrEmpty(questionText))
        {
            return ("QuestionText is required.", null, null, null);
        }

        var questionType = command.QuestionType?.Trim().ToLowerInvariant();

        if (!SurveyQuestionTypes.IsSupported(questionType))
        {
            return (
                "QuestionType must be one of: text, single_choice, multiple_choice, number, boolean, rating.",
                null,
                null,
                null);
        }

        string? optionsJson = null;

        if (SurveyQuestionTypes.IsChoice(questionType!))
        {
            var options = (command.Options ?? Array.Empty<string>())
                .Select(option => option?.Trim())
                .Where(option => !string.IsNullOrEmpty(option))
                .Select(option => option!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (options.Count == 0)
            {
                return ("Options are required for choice questions.", null, null, null);
            }

            optionsJson = JsonSerializer.Serialize(options);
        }

        return (null, questionText, questionType, optionsJson);
    }

    /// <summary>
    /// Проверяет поля анкеты.
    /// </summary>
    private static string? ValidateSurveyFields(SurveyCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
        {
            return "Title is required.";
        }

        if (command.Title.Trim().Length > TitleMaxLength)
        {
            return $"Title must not exceed {TitleMaxLength} characters.";
        }

        if (command.StartAt is not null
            && command.EndAt is not null
            && command.EndAt.Value <= command.StartAt.Value)
        {
            return "EndAt must be later than StartAt.";
        }

        return null;
    }

    private static void Increment(Dictionary<string, int> counts, string key) =>
        counts[key] = counts.TryGetValue(key, out var current) ? current + 1 : 1;

    private static JsonDocument? ParseAnswer(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(answer);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Анкета доступна, пока она активна и находится в пределах дат.
    /// </summary>
    private static bool IsOpen(Survey survey, DateTime now) =>
        survey.IsActive
        && (survey.StartAt is null || survey.StartAt <= now)
        && (survey.EndAt is null || survey.EndAt >= now);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Приводит дату к UTC, чтобы значение можно было записать в <c>timestamptz</c>.
    /// </summary>
    private static DateTime? ToUtc(DateTime? value)
    {
        if (value is not DateTime dateTime)
        {
            return null;
        }

        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };
    }
}
