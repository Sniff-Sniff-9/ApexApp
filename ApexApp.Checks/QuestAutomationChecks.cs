using System.Text.Json;
using ApexApp.Domain.Entities;
using ApexApp.Infrastructure.Common;
using ApexApp.Infrastructure.Data;
using ApexApp.Infrastructure.Services;
using ApexApp.Infrastructure.Services.Ai;
using ApexApp.Infrastructure.Services.Quests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexApp.Checks;

/// <summary>
/// Проверки автоматических шагов квестов: типизированная конфигурация, правило
/// подтверждения, защита ИИ от выдуманных идентификаторов и сквозные сценарии
/// на реальной схеме БД (только с флагом <c>--quest-write</c>).
/// </summary>
internal static class QuestAutomationChecks
{
    public static void Run(CheckRunner runner)
    {
        ConfigurationChecks(runner);
        SatisfactionChecks(runner);
        AiChecks(runner);
    }

    private static void ConfigurationChecks(CheckRunner runner)
    {
        runner.Check("quest.action-config.round-trip", () =>
        {
            var configuration = new QuestStepActionConfiguration(
                QuestStepActionCodes.CompleteSurvey,
                TargetId: 5);

            var json = configuration.ToJson();

            CheckRunner.Ensure(
                json.Contains("\"type\":\"complete_survey\"", StringComparison.Ordinal),
                "type must be serialised in camelCase");
            CheckRunner.Ensure(
                !json.Contains("achievementCode", StringComparison.Ordinal),
                "null parameters must be omitted");

            CheckRunner.Ensure(
                QuestStepActionConfiguration.TryParse(json, out var parsed, out var error),
                "serialised configuration must parse: " + error);

            CheckRunner.Ensure(parsed == configuration, "round-trip must preserve values");
        });

        runner.Check("quest.action-config.requires-target", () =>
        {
            var configuration = new QuestStepActionConfiguration(
                QuestStepActionCodes.CompleteSurvey);

            CheckRunner.Ensure(
                !QuestStepActionRegistry.TryValidate(configuration, out var error),
                "survey step without a target must be rejected");

            CheckRunner.Ensure(
                error!.Contains("requires a target", StringComparison.OrdinalIgnoreCase),
                "error must explain the missing target");
        });

        runner.Check("quest.action-config.rejects-irrelevant-parameters", () =>
        {
            var configuration = new QuestStepActionConfiguration(
                QuestStepActionCodes.SendThanks,
                TargetId: 5);

            CheckRunner.Ensure(
                !QuestStepActionRegistry.TryValidate(configuration, out var error),
                "send_thanks must not accept targetId");

            CheckRunner.Ensure(
                error!.Contains("targetId", StringComparison.Ordinal),
                "error must name the irrelevant parameter");
        });

        runner.Check("quest.action-config.unknown-type", () =>
        {
            CheckRunner.Ensure(
                !QuestStepActionConfiguration.TryParse(
                    """{"type":"hack_planet"}""",
                    out _,
                    out var error),
                "unknown action type must be rejected");

            CheckRunner.Ensure(
                error!.Contains("Unknown", StringComparison.OrdinalIgnoreCase),
                "error must mention the unknown type");
        });

        runner.Check("quest.action-config.unavailable-news-is-fail-closed", () =>
        {
            var configuration = new QuestStepActionConfiguration(
                QuestStepActionCodes.ReadCompanyNews,
                TargetId: 1);

            CheckRunner.Ensure(
                !QuestStepActionRegistry.TryValidate(configuration, out var error),
                "read_company_news must be unavailable without read tracking");

            CheckRunner.Ensure(
                error!.Contains("прочт", StringComparison.OrdinalIgnoreCase),
                "error must explain why the step cannot be verified");
        });

        runner.Check("quest.action-config.complete-profile-requires-avatar", () =>
        {
            CheckRunner.Ensure(
                QuestStepActionRegistry.TryValidate(
                    new QuestStepActionConfiguration(QuestStepActionCodes.CompleteProfile),
                    out _),
                "default avatar check must be valid");

            CheckRunner.Ensure(
                QuestStepActionRegistry.TryValidate(
                    new QuestStepActionConfiguration(
                        QuestStepActionCodes.CompleteProfile,
                        RequireAvatar: true),
                    out _),
                "explicit avatar check must be valid");

            CheckRunner.Ensure(
                !QuestStepActionRegistry.TryValidate(
                    new QuestStepActionConfiguration(
                        QuestStepActionCodes.CompleteProfile,
                        RequireAvatar: false),
                    out _),
                "disabling the only supported check must be rejected");
        });
    }

    private static void SatisfactionChecks(CheckRunner runner)
    {
        runner.Check("quest.satisfaction.manual-and-automatic", () =>
        {
            var manual = new QuestStep
            {
                Id = 1,
                CompletionType = QuestStepCompletionType.Manual
            };

            var automatic = new QuestStep
            {
                Id = 2,
                CompletionType = QuestStepCompletionType.Automatic
            };

            CheckRunner.Ensure(
                !QuestStepSatisfaction.IsSatisfied(manual, []),
                "manual step without a completion must not be satisfied");

            CheckRunner.Ensure(
                QuestStepSatisfaction.IsSatisfied(manual, [new QuestStepCompletion { StepId = 1 }]),
                "manual step with a completion must be satisfied");

            CheckRunner.Ensure(
                !QuestStepSatisfaction.IsSatisfied(automatic, []),
                "automatic step without a server-side completion must not be satisfied");

            CheckRunner.Ensure(
                QuestStepSatisfaction.IsSatisfied(
                    automatic,
                    [new QuestStepCompletion { StepId = 2 }]),
                "automatic step with a completion must be satisfied");
        });

        runner.Check("quest.satisfaction.proof-requires-approval", () =>
        {
            var step = new QuestStep
            {
                Id = 3,
                CompletionType = QuestStepCompletionType.Proof
            };

            var pending = new QuestStepCompletion { StepId = 3 };
            pending.QuestProofs.Add(new QuestProof
            {
                Status = QuestProofStatuses.Pending,
                CreatedAt = DateTime.UtcNow
            });

            CheckRunner.Ensure(
                !QuestStepSatisfaction.IsSatisfied(step, [pending]),
                "pending proof must not satisfy the step");

            var approved = new QuestStepCompletion { StepId = 3 };
            approved.QuestProofs.Add(new QuestProof
            {
                Status = QuestProofStatuses.Approved,
                CreatedAt = DateTime.UtcNow
            });

            CheckRunner.Ensure(
                QuestStepSatisfaction.IsSatisfied(step, [approved]),
                "approved proof must satisfy the step");
        });
    }

    private static void AiChecks(CheckRunner runner)
    {
        var candidates = new QuestStepActionCandidateSets(
            [new QuestStepActionOption("1", "Онбординг", "до 2026-12-01", true)],
            [new QuestStepActionOption("5", "Пульс-опрос", "один ответ", true)],
            [new QuestStepActionOption("9", "Хакатон", "2026-12-10 — 2026-12-11", true)],
            [new QuestStepActionOption("FIRST_ORDER", "Первый заказ", "FIRST_ORDER", true)],
            [new QuestStepActionOption("34", "Футболка", "500 баллов", true)]);

        runner.Check("quest.ai.accepts-listed-target", () =>
        {
            var steps = AiContentValidator.QuestSteps(
                [
                    new QuestStepPayload
                    {
                        Title = "Пройти пульс-опрос",
                        Description = "Ответьте на вопросы анкеты.",
                        CompletionType = "automatic",
                        ActionType = "complete_survey",
                        TargetId = 5,
                        IsRequired = true
                    }
                ],
                8,
                candidates);

            CheckRunner.Ensure(steps.Count == 1, "step must be kept");
            CheckRunner.Ensure(
                steps[0].CompletionType == "automatic",
                "known action must stay automatic");
            CheckRunner.Ensure(steps[0].Action?.Type == "complete_survey", "action must be kept");
            CheckRunner.Ensure(steps[0].Action?.TargetId == 5, "listed target must be kept");
            CheckRunner.Ensure(!steps[0].NeedsConfiguration, "listed target needs no manual setup");
        });

        runner.Check("quest.ai.rejects-fabricated-target", () =>
        {
            var steps = AiContentValidator.QuestSteps(
                [
                    new QuestStepPayload
                    {
                        Title = "Пройти придуманную анкету",
                        Description = "Такой анкеты нет в списке Backend-а.",
                        CompletionType = "automatic",
                        ActionType = "complete_survey",
                        TargetId = 999,
                        IsRequired = true
                    }
                ],
                8,
                candidates);

            CheckRunner.Ensure(
                steps[0].Action?.TargetId is null,
                "fabricated identifier must be discarded");
            CheckRunner.Ensure(
                steps[0].NeedsConfiguration,
                "step must be flagged for manual configuration");
        });

        runner.Check("quest.ai.unknown-action-falls-back-to-manual", () =>
        {
            var steps = AiContentValidator.QuestSteps(
                [
                    new QuestStepPayload
                    {
                        Title = "Неизвестное действие",
                        Description = "Модель предложила неподдерживаемый сценарий.",
                        CompletionType = "automatic",
                        ActionType = "hack_planet",
                        IsRequired = true
                    }
                ],
                8,
                candidates);

            CheckRunner.Ensure(
                steps[0].CompletionType == "manual",
                "unsupported action must become a manual step");
            CheckRunner.Ensure(
                steps[0].Action is null,
                "unsupported action code must be dropped");
        });

        runner.Check("quest.ai.disabled-automatic-falls-back-to-manual", () =>
        {
            var steps = AiContentValidator.QuestSteps(
                [
                    new QuestStepPayload
                    {
                        Title = "Автоматический шаг",
                        Description = "Автоматические шаги запрещены запросом.",
                        CompletionType = "automatic",
                        ActionType = "send_thanks",
                        IsRequired = true
                    }
                ],
                8,
                candidates,
                allowAutomatic: false);

            CheckRunner.Ensure(
                steps[0].CompletionType == "manual",
                "automatic must be disabled by the request flag");
        });

        runner.Check("quest.ai.make-purchase-parameters", () =>
        {
            var steps = AiContentValidator.QuestSteps(
                [
                    new QuestStepPayload
                    {
                        Title = "Купить футболку",
                        Description = "Оформите заказ на футболку.",
                        CompletionType = "automatic",
                        ActionType = "make_purchase",
                        ProductId = 34,
                        MinAmount = 500,
                        IsRequired = true
                    }
                ],
                8,
                candidates);

            CheckRunner.Ensure(steps.Count == 1, "step must be kept");
            CheckRunner.Ensure(steps[0].Action?.Type == "make_purchase", "action must be kept");
            CheckRunner.Ensure(steps[0].Action?.ProductId == 34, "product must be kept");
            CheckRunner.Ensure(steps[0].Action?.MinAmount == 500, "amount must be kept");
            CheckRunner.Ensure(!steps[0].NeedsConfiguration, "known parameters need no setup");
        });

        runner.Check("quest.ai.thanks-count-and-avatar", () =>
        {
            var steps = AiContentValidator.QuestSteps(
                [
                    new QuestStepPayload
                    {
                        Title = "Поблагодарить коллег",
                        Description = "Отправьте пять благодарностей.",
                        CompletionType = "automatic",
                        ActionType = "send_thanks",
                        Count = 5,
                        IsRequired = true
                    },
                    new QuestStepPayload
                    {
                        Title = "Заполнить профиль",
                        Description = "Установите аватар.",
                        CompletionType = "automatic",
                        ActionType = "complete_profile",
                        RequireAvatar = true,
                        IsRequired = false
                    }
                ],
                8,
                candidates);

            CheckRunner.Ensure(steps.Count == 2, "both steps must be kept");
            CheckRunner.Ensure(steps[0].Action?.Count == 5, "count must be kept");
            CheckRunner.Ensure(
                steps[1].Action?.RequireAvatar == true,
                "avatar requirement must be kept");
        });

        runner.Check("quest.ai.rejects-fabricated-product", () =>
        {
            var steps = AiContentValidator.QuestSteps(
                [
                    new QuestStepPayload
                    {
                        Title = "Купить несуществующий товар",
                        Description = "Такого товара нет в списке.",
                        CompletionType = "automatic",
                        ActionType = "make_purchase",
                        ProductId = 999,
                        IsRequired = true
                    }
                ],
                8,
                candidates);

            CheckRunner.Ensure(steps[0].Action?.ProductId is null, "product must be discarded");
            CheckRunner.Ensure(
                steps[0].NeedsConfiguration,
                "step must be flagged for manual configuration");
        });

        runner.Check("quest.ai.prompt-lists-only-backend-objects", () =>
        {
            var prompt = AiPromptBuilder.QuestSteps(
                new AiQuestStepRequest("Квест", null, 4, null, true),
                candidates);

            CheckRunner.Ensure(
                prompt.Contains("complete_survey", StringComparison.Ordinal),
                "prompt must list supported action codes");
            CheckRunner.Ensure(
                prompt.Contains("5: Пульс-опрос", StringComparison.Ordinal),
                "prompt must carry real identifiers from the backend");
            CheckRunner.Ensure(
                prompt.Contains("никогда не придумывай идентификаторы", StringComparison.OrdinalIgnoreCase),
                "prompt must forbid invented identifiers");
        });
    }

    /// <summary>
    /// Сквозные проверки на реальной схеме. Используют временных пользователей,
    /// квесты и записи источников внутри транзакции и откатывают её в конце,
    /// поэтому данные не остаются в базе.
    /// </summary>
    public static async Task RunAsync(CheckRunner runner, ServiceProvider provider)
    {
        await runner.CheckAsync("quest.automation.real-records-and-duplicate-reward-guard", async () =>
        {
            await using var scope = provider.CreateAsyncScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var progress = scope.ServiceProvider.GetRequiredService<IQuestProgressService>();
            var rewards = scope.ServiceProvider.GetRequiredService<IActivityRewardService>();
            var questService = scope.ServiceProvider.GetRequiredService<IQuestService>();

            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            try
            {
                var now = DateTime.UtcNow;

                // public.users.id ссылается на auth.users.id, поэтому временного
                // пользователя создать нельзя. Берём существующих сотрудников.
                var users = await dbContext.Users
                    .AsNoTracking()
                    .Where(item => item.IsActive)
                    .OrderBy(item => item.CreatedAt)
                    .Take(2)
                    .ToListAsync();

                CheckRunner.Ensure(
                    users.Count == 2,
                    "two active users are required for the quest automation check");

                var user = users[0];
                var peer = users[1];

                var startingBalance = await dbContext.Balances
                    .AsNoTracking()
                    .Where(balance =>
                        balance.UserId == user.Id && balance.Type == BalanceType.Personal)
                    .Select(balance => balance.Amount)
                    .FirstOrDefaultAsync();

                var quest = NewQuest(now);
                quest.QuestSteps.Add(NewAutomaticStep(
                    QuestStepActionCodes.SendThanks,
                    isRequired: true,
                    sortOrder: 0));
                quest.QuestSteps.Add(NewAutomaticStep(
                    QuestStepActionCodes.MakePurchase,
                    isRequired: false,
                    sortOrder: 1));
                dbContext.Quests.Add(quest);

                await dbContext.SaveChangesAsync();

                var participation = new QuestParticipation
                {
                    QuestId = quest.Id,
                    UserId = user.Id,
                    Status = QuestParticipationStatuses.InProgress,
                    StartedAt = now,
                    CreatedAt = now
                };

                dbContext.QuestParticipations.Add(participation);
                await dbContext.SaveChangesAsync();

                // 1. Пустая сверка ничего не создаёт.
                var empty = await progress.SyncAsync(user.Id, [quest.Id]);
                CheckRunner.Ensure(
                    empty.CreatedCompletions == 0,
                    "nothing must be completed before the source action exists");

                // 2. Благодарность записана напрямую, как это сделал бы любой сценарий.
                dbContext.Thanks.Add(new Thank
                {
                    FromUserId = user.Id,
                    ToUserId = peer.Id,
                    Amount = 1,
                    CreatedAt = now.AddSeconds(1)
                });
                await dbContext.SaveChangesAsync();

                var afterThanks = await progress.SyncAsync(user.Id, [quest.Id]);
                CheckRunner.Ensure(
                    afterThanks.CreatedCompletions == 1,
                    "send_thanks must be satisfied by the recorded thank");

                // 3. Идемпотентность: повторная сверка не создаёт дубликат.
                var repeated = await progress.SyncAsync(user.Id, [quest.Id]);
                CheckRunner.Ensure(
                    repeated.CreatedCompletions == 0,
                    "repeated sync must not create a duplicate completion");

                CheckRunner.Ensure(
                    await dbContext.QuestStepCompletions.CountAsync(
                        completion => completion.ParticipationId == participation.Id) == 1,
                    "exactly one completion must exist");

                // 4. Отменённый заказ не засчитывается.
                dbContext.Orders.Add(new Order
                {
                    UserId = user.Id,
                    Status = OrderStatus.Cancelled,
                    TotalAmount = 500,
                    CreatedAt = now.AddSeconds(2),
                    UpdatedAt = now
                });
                await dbContext.SaveChangesAsync();

                var afterCancelledOrder = await progress.SyncAsync(user.Id, [quest.Id]);
                CheckRunner.Ensure(
                    afterCancelledOrder.CreatedCompletions == 0,
                    "cancelled order must not complete make_purchase");

                // 5. Оплаченный заказ засчитывается.
                dbContext.Orders.Add(new Order
                {
                    UserId = user.Id,
                    Status = OrderStatus.Paid,
                    TotalAmount = 500,
                    CreatedAt = now.AddSeconds(3),
                    UpdatedAt = now
                });
                await dbContext.SaveChangesAsync();

                var afterPaidOrder = await progress.SyncAsync(user.Id, [quest.Id]);
                CheckRunner.Ensure(
                    afterPaidOrder.CreatedCompletions == 1,
                    "paid order must complete make_purchase");

                var completions = await dbContext.QuestStepCompletions
                    .AsNoTracking()
                    .Where(completion => completion.ParticipationId == participation.Id)
                    .ToListAsync();

                CheckRunner.Ensure(completions.Count == 2, "two steps must be completed");

                var purchaseStep = quest.QuestSteps.Single(step =>
                    step.Title == "Automatic: " + QuestStepActionCodes.MakePurchase);

                var purchaseCompletion = completions.Single(completion =>
                    completion.StepId == purchaseStep.Id);

                CheckRunner.Ensure(
                    purchaseCompletion.Proof is not null,
                    "completion must store the evidence snapshot");

                using var evidence = JsonDocument.Parse(purchaseCompletion.Proof!);
                CheckRunner.Ensure(
                    evidence.RootElement.GetProperty("action").GetString() == "make_purchase",
                    "evidence must identify the action");

                // 6. Награда выдаётся один раз, повторный вызов идемпотентен.
                participation.Status = QuestParticipationStatuses.Completed;
                participation.CompletedAt = now.AddSeconds(4);
                await dbContext.SaveChangesAsync();

                var firstReward = await rewards.GrantQuestRewardAsync(participation.Id);
                CheckRunner.Ensure(firstReward.IsSuccess, "first reward claim must succeed");
                CheckRunner.Ensure(
                    !firstReward.Value!.AlreadyGranted,
                    "first reward claim must grant points");
                CheckRunner.Ensure(
                    firstReward.Value.Amount == quest.RewardAmount,
                    "reward amount must match the quest");

                var secondReward = await rewards.GrantQuestRewardAsync(participation.Id);
                CheckRunner.Ensure(secondReward.IsSuccess, "second reward claim must succeed");
                CheckRunner.Ensure(
                    secondReward.Value!.AlreadyGranted,
                    "second reward claim must be idempotent");

                var balance = await dbContext.Balances
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item =>
                        item.UserId == user.Id && item.Type == BalanceType.Personal);

                CheckRunner.Ensure(balance is not null, "balance must exist");
                CheckRunner.Ensure(
                    balance!.Amount == startingBalance + quest.RewardAmount,
                    "reward must be credited exactly once");

                // 7. Посещение мероприятия — устойчивый факт, подтверждённый администратором.
                var eventItem = new Event
                {
                    Title = "Quest automation event " + Guid.NewGuid().ToString("N"),
                    StartAt = now.AddHours(-1),
                    EndAt = now.AddHours(1),
                    RewardAmount = 0,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                dbContext.Events.Add(eventItem);
                await dbContext.SaveChangesAsync();

                var eventQuest = NewQuest(now);
                eventQuest.QuestSteps.Add(NewAutomaticStep(
                    QuestStepActionCodes.AttendEvent,
                    isRequired: true,
                    sortOrder: 0,
                    targetId: eventItem.Id));
                dbContext.Quests.Add(eventQuest);
                dbContext.EventParticipants.Add(new EventParticipant
                {
                    EventId = eventItem.Id,
                    UserId = user.Id,
                    Status = EventParticipantStatuses.Attended,
                    RegisteredAt = now,
                    AttendedAt = now
                });
                await dbContext.SaveChangesAsync();

                var eventParticipation = new QuestParticipation
                {
                    QuestId = eventQuest.Id,
                    UserId = user.Id,
                    Status = QuestParticipationStatuses.InProgress,
                    StartedAt = now,
                    CreatedAt = now
                };

                dbContext.QuestParticipations.Add(eventParticipation);
                await dbContext.SaveChangesAsync();

                var afterEvent = await progress.SyncAsync(user.Id, [eventQuest.Id]);
                CheckRunner.Ensure(
                    afterEvent.CreatedCompletions == 1,
                    "confirmed attendance must complete attend_event regardless of when it was confirmed");

                // 8. Ручные и proof-шаги по-прежнему создаются без типизированной конфигурации.
                var manualStep = await questService.CreateStepAsync(
                    quest.Id,
                    new QuestStepCommand(
                        "Manual check step",
                        "Routine step",
                        "manual",
                        2,
                        false,
                        null),
                    default);

                CheckRunner.Ensure(
                    manualStep.IsSuccess,
                    "manual step creation must keep working: " + manualStep.Error);

                var proofStep = await questService.CreateStepAsync(
                    quest.Id,
                    new QuestStepCommand(
                        "Proof check step",
                        "Proof step",
                        "proof",
                        3,
                        false,
                        null),
                    default);

                CheckRunner.Ensure(
                    proofStep.IsSuccess,
                    "proof step creation must keep working: " + proofStep.Error);

                // 9. Автоматический шаг создаётся и публикуется только с корректной конфигурацией.
                var publishable = await questService.CreateAsync(
                    new QuestCommand(
                        "Quest publish check " + Guid.NewGuid().ToString("N"),
                        null,
                        5,
                        now.AddDays(-1),
                        now.AddDays(1),
                        false),
                    [
                        new QuestStepCommand(
                            "Send thanks",
                            null,
                            "automatic",
                            0,
                            true,
                            null,
                            new QuestStepActionCommand(QuestStepActionCodes.SendThanks))
                    ],
                    user.Id,
                    default);

                CheckRunner.Ensure(
                    publishable.IsSuccess,
                    "automatic quest creation must succeed: " + publishable.Error);

                var published = await questService.PublishAsync(publishable.Value!.Id, default);

                CheckRunner.Ensure(
                    published.IsSuccess,
                    "publishing a valid automatic step must succeed: " + published.Error);

                var invalidConfiguration = await questService.CreateAsync(
                    new QuestCommand(
                        "Quest invalid check " + Guid.NewGuid().ToString("N"),
                        null,
                        5,
                        now.AddDays(-1),
                        now.AddDays(1),
                        false),
                    [
                        new QuestStepCommand(
                            "Broken survey step",
                            null,
                            "automatic",
                            0,
                            true,
                            null,
                            new QuestStepActionCommand(QuestStepActionCodes.CompleteSurvey))
                    ],
                    user.Id,
                    default);

                CheckRunner.Ensure(
                    !invalidConfiguration.IsSuccess
                    && invalidConfiguration.StatusCode == 400,
                    "automatic step without a target must be rejected with 400");
            }
            catch (Exception exception)
            {
                // Harness reports only Message; keep the full chain for the inner
                // PostgreSQL error while diagnosing the scenario.
                throw new InvalidOperationException(exception.ToString());
            }
            finally
            {
                await transaction.RollbackAsync();
            }
        });
    }

    private static Quest NewQuest(DateTime now) =>
        new()
        {
            Title = "Quest automation check " + Guid.NewGuid().ToString("N"),
            RewardAmount = 10,
            IsActive = true,
            StartAt = now.AddDays(-1),
            EndAt = now.AddDays(1),
            CreatedAt = now,
            UpdatedAt = now
        };

    private static QuestStep NewAutomaticStep(
        string actionType,
        bool isRequired,
        int sortOrder,
        long? targetId = null,
        string? achievementCode = null) =>
        new()
        {
            Title = "Automatic: " + actionType,
            Description = "Check step",
            CompletionType = QuestStepCompletionType.Automatic,
            SortOrder = sortOrder,
            IsRequired = isRequired,
            CompletionData = new QuestStepActionConfiguration(
                actionType,
                targetId,
                achievementCode).ToJson(),
            CreatedAt = DateTime.UtcNow
        };
}
