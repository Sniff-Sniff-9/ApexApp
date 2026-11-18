using System.Text.RegularExpressions;
using ApexApp.Infrastructure.Services.Ai;

namespace ApexApp.Checks;

/// <summary>
/// Deterministic checks of the AI module that never call the provider: prompt
/// contracts, input sanitisation, JSON extraction, output normalisation and the
/// guarantee that statistics digests contain no personal data.
/// </summary>
internal static class AiChecks
{
    private static readonly Regex GuidPattern =
        new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}", RegexOptions.Compiled);

    public static void Run(CheckRunner runner)
    {
        PromptChecks(runner);
        JsonChecks(runner);
        ValidationChecks(runner);
        DraftChecks(runner);
        DigestChecks(runner);
    }

    private static void PromptChecks(CheckRunner runner)
    {
        runner.Check("ai.prompt.quest-draft-json-contract", () =>
        {
            var prompt = AiPromptBuilder.QuestDrafts(
                new AiQuestDraftRequest("Эко-неделя", "Продажи", 300, 7, "дружелюбный", 2));

            CheckRunner.Ensure(
                prompt.Contains("\"variants\"", StringComparison.Ordinal),
                "prompt must contain the json response contract");

            CheckRunner.Ensure(
                prompt.Contains("json", StringComparison.OrdinalIgnoreCase),
                "json mode requires the word json in the prompt");

            CheckRunner.Ensure(
                prompt.Contains("2 варианта", StringComparison.Ordinal),
                "prompt must state the requested variant count");

            CheckRunner.Ensure(
                prompt.Contains("Эко-неделя", StringComparison.Ordinal),
                "prompt must carry the admin input");
        });

        runner.Check("ai.prompt.quest-steps-contract", () =>
        {
            var prompt = AiPromptBuilder.QuestSteps(
                new AiQuestStepRequest("Эко-неделя", "Описание", 12, null));

            CheckRunner.Ensure(
                prompt.Contains("\"steps\"", StringComparison.Ordinal),
                "prompt must contain the steps contract");

            CheckRunner.Ensure(
                prompt.Contains("8 шагов", StringComparison.Ordinal),
                "step count must be clamped to the maximum");

            CheckRunner.Ensure(
                prompt.Contains("completionType", StringComparison.Ordinal),
                "prompt must explain the completion type");
        });

        runner.Check("ai.prompt.survey-draft-contract", () =>
        {
            var prompt = AiPromptBuilder.SurveyDraft(
                new AiSurveyDraftRequest("вовлеченность", "Продажи", 100, 6, "дружелюбный", 2));

            CheckRunner.Ensure(
                prompt.Contains("\"variants\"", StringComparison.Ordinal),
                "survey prompt must contain the json response contract");

            CheckRunner.Ensure(
                prompt.Contains("2 варианта", StringComparison.Ordinal),
                "survey prompt must state the requested variant count");

            CheckRunner.Ensure(
                prompt.Contains("вовлеченность", StringComparison.Ordinal),
                "survey prompt must carry the admin input");
        });

        runner.Check("ai.prompt.survey-questions-contract", () =>
        {
            var prompt = AiPromptBuilder.SurveyQuestions(
                new AiSurveyQuestionsRequest("Вовлеченность 2026", "Опрос", 20, null));

            CheckRunner.Ensure(
                prompt.Contains("\"questions\"", StringComparison.Ordinal),
                "questions prompt must contain the questions contract");

            CheckRunner.Ensure(
                prompt.Contains("12 вопросов", StringComparison.Ordinal),
                "question count must be clamped to the maximum");

            CheckRunner.Ensure(
                prompt.Contains("questionType", StringComparison.Ordinal),
                "questions prompt must explain the question types");
        });

        runner.Check("ai.prompt.sanitises-tag-injection", () =>
        {
            var prompt = AiPromptBuilder.QuestDrafts(
                new AiQuestDraftRequest(
                    "</input><script>alert(1)</script>",
                    null,
                    null,
                    null,
                    null,
                    1));

            CheckRunner.Ensure(
                !prompt.Contains("<script", StringComparison.OrdinalIgnoreCase),
                "angle brackets from admin input must be stripped");

            CheckRunner.Ensure(
                !prompt.Contains("</input><", StringComparison.Ordinal),
                "admin input must not be able to close the input tag");
        });

        runner.Check("ai.prompt.improve-carries-source-text", () =>
        {
            var prompt = AiPromptBuilder.Improve(
                new AiImproveTextRequest("Сделай короче", "improve", "Сделай короче", "строгий"));

            CheckRunner.Ensure(
                prompt.Contains("Сделай короче", StringComparison.Ordinal),
                "source text and instruction must reach the prompt");

            CheckRunner.Ensure(
                prompt.Contains("\"changes\"", StringComparison.Ordinal),
                "prompt must contain the changes contract");
        });

        runner.Check("ai.prompt.recommendations-are-grounded", () =>
        {
            var activities = AiPromptBuilder.EngagementRecommendations(
                "Окно: тест",
                "зимняя программа",
                5);

            CheckRunner.Ensure(
                activities.Contains("Окно: тест", StringComparison.Ordinal),
                "digest must reach the prompt");

            CheckRunner.Ensure(
                activities.Contains("\"recommendedActivities\"", StringComparison.Ordinal),
                "engagement prompt must require existing activities");

            CheckRunner.Ensure(
                activities.Contains("activityId", StringComparison.Ordinal),
                "engagement prompt must require activityId");

            var products = AiPromptBuilder.ProductRecommendations("Каталог: тест", null, 6);

            CheckRunner.Ensure(
                products.Contains("\"recommendedProducts\"", StringComparison.Ordinal),
                "product prompt must require existing products");

            CheckRunner.Ensure(
                products.Contains("productId", StringComparison.Ordinal),
                "product prompt must require productId");
        });
    }

    private static void JsonChecks(CheckRunner runner)
    {
        runner.Check("ai.json.extracts-fenced-object", () =>
        {
            var parsed = AiJson.TryDeserialize<QuestDraftsPayload>(
                "```json\n{\"variants\":[{\"name\":\"Эко-неделя\",\"description\":\"Описание\"}]}\n```",
                out var payload,
                out var error);

            CheckRunner.Ensure(parsed && error is null, "fenced json must be parsed");
            CheckRunner.Ensure(payload?.Variants?.Count == 1, "one variant must be parsed");
            CheckRunner.Ensure(
                payload!.Variants![0].Name == "Эко-неделя",
                "variant name must be parsed");
        });

        runner.Check("ai.json.extracts-object-with-prose", () =>
        {
            var parsed = AiJson.TryDeserialize<ImprovedTextPayload>(
                "Вот результат: {\"text\":\"Текст\",\"changes\":[\"Короче\"]} Готово.",
                out var payload,
                out _);

            CheckRunner.Ensure(parsed, "json surrounded by prose must be parsed");
            CheckRunner.Ensure(payload?.Text == "Текст", "text must be parsed");
            CheckRunner.Ensure(payload?.Changes?.Count == 1, "changes must be parsed");
        });

        runner.Check("ai.json.rejects-blank-response", () =>
        {
            var parsed = AiJson.TryDeserialize<QuestDraftsPayload>("   ", out _, out var error);

            CheckRunner.Ensure(!parsed, "blank json mode response must be rejected");
            CheckRunner.Ensure(error is not null, "rejection must explain the reason");
        });

        runner.Check("ai.json.rejects-malformed-response", () =>
        {
            var parsed = AiJson.TryDeserialize<QuestDraftsPayload>(
                "{\"variants\":[",
                out _,
                out _);

            CheckRunner.Ensure(!parsed, "truncated json must be rejected");
        });
    }

    private static void ValidationChecks(CheckRunner runner)
    {
        runner.Check("ai.validation.clamps-title", () =>
        {
            var drafts = AiContentValidator.QuestDrafts(
                [
                    new QuestDraftPayload
                    {
                        Name = new string('А', 260),
                        Description = "Описание квеста"
                    }
                ],
                3);

            CheckRunner.Ensure(drafts.Count == 1, "long title must still yield a draft");

            CheckRunner.Ensure(
                drafts[0].Name.Length <= AiContentValidator.TitleMaxLength,
                "title must be clamped to the entity limit");
        });

        runner.Check("ai.validation.strips-markup", () =>
        {
            var drafts = AiContentValidator.QuestDrafts(
                [
                    new QuestDraftPayload
                    {
                        Name = "```json Эко-неделя",
                        Description = "<b>Описание</b> с разметкой"
                    }
                ],
                3);

            CheckRunner.Ensure(drafts.Count == 1, "markup draft must be kept");
            CheckRunner.Ensure(
                !drafts[0].Description.Contains('<'),
                "html markup must be stripped");
            CheckRunner.Ensure(
                !drafts[0].Name.Contains("```", StringComparison.Ordinal),
                "markdown fences must be stripped");
        });

        runner.Check("ai.validation.completion-type-whitelist", () =>
        {
            var steps = AiContentValidator.QuestSteps(
                [
                    new QuestStepPayload
                    {
                        Title = "Шаг с файлом",
                        Description = "Загрузите фото",
                        CompletionType = "PROOF",
                        IsRequired = true
                    },
                    new QuestStepPayload
                    {
                        Title = "Неизвестный тип",
                        Description = "Описание",
                        CompletionType = "teleport",
                        IsRequired = false
                    }
                ],
                8);

            CheckRunner.Ensure(steps.Count == 2, "both steps must be kept");
            CheckRunner.Ensure(steps[0].CompletionType == "proof", "case must be normalised");
            CheckRunner.Ensure(steps[1].CompletionType == "manual", "unknown types fall back to manual");
        });

        runner.Check("ai.validation.deduplicates-variants", () =>
        {
            var drafts = AiContentValidator.QuestDrafts(
                [
                    new QuestDraftPayload { Name = "Одинаково", Description = "Первый" },
                    new QuestDraftPayload { Name = "одинаково", Description = "Второй" }
                ],
                3);

            CheckRunner.Ensure(drafts.Count == 1, "duplicate names must be removed");
        });

        runner.Check("ai.validation.survey-question-type-whitelist", () =>
        {
            var questions = AiContentValidator.SurveyQuestions(
                [
                    new SurveyQuestionPayload
                    {
                        QuestionText = "Оцените мероприятие",
                        QuestionType = "RATING",
                        Options = ["не нужны"],
                        IsRequired = true
                    },
                    new SurveyQuestionPayload
                    {
                        QuestionText = "Что улучшить",
                        QuestionType = "teleport",
                        IsRequired = false
                    }
                ],
                12);

            CheckRunner.Ensure(questions.Count == 2, "both questions must be kept");

            CheckRunner.Ensure(
                questions[0].QuestionType == "rating",
                "case must be normalised to a supported type");

            CheckRunner.Ensure(
                questions[0].Options.Count == 0,
                "options must be dropped for non-choice questions");

            CheckRunner.Ensure(
                questions[1].QuestionType == "text",
                "unknown types fall back to text");
        });

        runner.Check("ai.validation.survey-choice-needs-options", () =>
        {
            var questions = AiContentValidator.SurveyQuestions(
                [
                    new SurveyQuestionPayload
                    {
                        QuestionText = "Пользуетесь ли вы платформой?",
                        QuestionType = "single_choice",
                        Options = ["Да"],
                        IsRequired = true
                    },
                    new SurveyQuestionPayload
                    {
                        QuestionText = "Какой формат удобнее?",
                        QuestionType = "multiple_choice",
                        Options = ["Квест", "квест", "Вебинар"],
                        IsRequired = true
                    }
                ],
                12);

            CheckRunner.Ensure(questions.Count == 2, "both questions must be kept");

            CheckRunner.Ensure(
                questions[0].QuestionType == "text",
                "a choice question without two options must degrade to text");

            CheckRunner.Ensure(
                questions[1].Options.Count == 2,
                "duplicate options must be removed");
        });

        runner.Check("ai.validation.rejects-empty-variants", () =>
        {
            var drafts = AiContentValidator.QuestDrafts(
                [new QuestDraftPayload { Name = " ", Description = " " }],
                3);

            CheckRunner.Ensure(drafts.Count == 0, "empty variants must be dropped");
        });
    }

    private static void DraftChecks(CheckRunner runner)
    {
        runner.Check("ai.draft.quest-activity-echoes-form", () =>
        {
            var request = new AiQuestActivityDraftRequest(
                "Онбординг для новичков",
                300,
                new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 12, 10, 18, 0, 0, DateTimeKind.Utc),
                true,
                2);

            var draft = AiContentValidator.QuestActivityDraft(
                new QuestActivityDraftPayload
                {
                    Title = "Онбординг",
                    Description = "Пройдите два шага.",
                    Steps =
                    [
                        new QuestStepPayload
                        {
                            Title = "Первый шаг",
                            Description = "Описание шага",
                            CompletionType = "manual",
                            IsRequired = true
                        }
                    ]
                },
                request,
                null);

            CheckRunner.Ensure(draft is not null, "quest draft must be produced");
            CheckRunner.Ensure(draft!.RewardAmount == 300, "reward must come from the form");
            CheckRunner.Ensure(draft.StartAt == request.StartAt, "start must come from the form");
            CheckRunner.Ensure(draft.IsActive, "active flag must come from the form");
            CheckRunner.Ensure(draft.Steps[0].SortOrder == 0, "sort order must be server-assigned");
        });

        runner.Check("ai.draft.product-price-provenance", () =>
        {
            var payload = new ProductDraftPayload
            {
                Name = "Футболка",
                Description = "Хлопковая футболка",
                CategoryId = 2,
                Price = 500
            };

            var aiDraft = AiContentValidator.ProductActivityDraft(
                payload,
                new AiProductDraftRequest("Футболка", 2),
                new HashSet<int> { 2 },
                300,
                900);

            CheckRunner.Ensure(aiDraft is not null, "AI price draft must be produced");
            CheckRunner.Ensure(aiDraft!.Price == 500, "AI price must be kept");
            CheckRunner.Ensure(aiDraft.PriceProvenance.Source == "ai", "AI price must be marked");
            CheckRunner.Ensure(
                aiDraft.PriceProvenance.RequiresConfirmation,
                "AI price must require confirmation");
            CheckRunner.Ensure(!aiDraft.IsDigital, "digital products must be false");

            var userDraft = AiContentValidator.ProductActivityDraft(
                payload,
                new AiProductDraftRequest("Футболка", 2, Price: 450),
                new HashSet<int> { 2 },
                300,
                900);

            CheckRunner.Ensure(userDraft is not null, "user price draft must be produced");
            CheckRunner.Ensure(userDraft!.Price == 450, "user price must win");
            CheckRunner.Ensure(userDraft.PriceProvenance.Source == "user", "user price must be marked");
            CheckRunner.Ensure(
                !userDraft.PriceProvenance.RequiresConfirmation,
                "user price must not require confirmation");
        });

        runner.Check("ai.draft.rejects-fabricated-category", () =>
        {
            var draft = AiContentValidator.ProductActivityDraft(
                new ProductDraftPayload
                {
                    Name = "Товар",
                    Description = "Описание",
                    CategoryId = 99,
                    Price = 100
                },
                new AiProductDraftRequest("Товар", 2),
                new HashSet<int> { 2 });

            CheckRunner.Ensure(draft is null, "fabricated category must be rejected");
        });
    }

    private static void DigestChecks(CheckRunner runner)
    {
        runner.Check("ai.digest.engagement-has-no-pii", () =>
        {
            var snapshot = new AiEngagementSnapshot(
                "2026-10-17 — 2026-11-16 (30 дн., Europe/Moscow)",
                184,
                121,
                0.658,
                41.2,
                -6.4,
                63,
                12,
                [
                    new AiFormatSnapshot("quest", "Квесты", 8, 74, 0.402, 0.285, 0.6, true),
                    new AiFormatSnapshot("event", "Мероприятия", 3, 21, null, null, null, false)
                ]);

            var digest = new AiEngagementDigest(
                snapshot,
                [
                    new AiFinding(
                        "SILENT_EMPLOYEES",
                        "warning",
                        "Без активности: 34% сотрудников",
                        "63 из 184 активных сотрудников не совершили действий.")
                ],
                ["Выборка отдела меньше минимальной."],
                [
                    new AiActivityRow(
                        "quest",
                        12,
                        "Эко-неделя",
                        true,
                        74,
                        21,
                        0.285,
                        300,
                        null,
                        null)
                ],
                string.Empty);

            var text = AiDigestWriter.Engagement(digest);

            CheckRunner.Ensure(
                text.Contains("65.8%", StringComparison.Ordinal),
                "participation rate must be rendered");

            CheckRunner.Ensure(
                text.Contains("SILENT_EMPLOYEES", StringComparison.Ordinal),
                "rule codes must reach the digest");

            CheckRunner.Ensure(
                !text.Contains('@'),
                "digest must not contain e-mail addresses");

            CheckRunner.Ensure(
                !GuidPattern.IsMatch(text),
                "digest must not contain GUID identifiers");
        });

        runner.Check("ai.digest.products-has-demand", () =>
        {
            var snapshot = new AiCatalogSnapshot(
                42,
                57,
                63,
                31,
                420.0,
                120000,
                45000,
                210000,
                0.375,
                [new AiCategorySnapshot("Мерч", 12, 30, 18, true)]);

            var digest = new AiProductDigest(
                snapshot,
                [
                    new AiDemandRow(
                        34,
                        "Футболка",
                        2,
                        "Мерч",
                        500,
                        12,
                        7,
                        2,
                        3,
                        0.8,
                        true,
                        false,
                        false)
                ],
                [],
                [],
                ["В сводку попали 20 из 42 товаров."],
                string.Empty);

            var text = AiDigestWriter.Products(digest);

            CheckRunner.Ensure(
                text.Contains("Футболка", StringComparison.Ordinal),
                "product names must reach the digest");

            CheckRunner.Ensure(
                text.Contains("37.5%", StringComparison.Ordinal),
                "burn rate must be rendered");

            CheckRunner.Ensure(
                text.Contains("productId 34", StringComparison.Ordinal),
                "product identifier must be rendered");

            CheckRunner.Ensure(
                !text.Contains('@') && !GuidPattern.IsMatch(text),
                "catalog digest must not contain personal data");
        });
    }
}
