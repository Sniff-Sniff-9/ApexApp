using ApexApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ApexApp.Infrastructure.Caching;

/// <summary>
/// Сбрасывает кеш вовлечённости сразу после записи действий, которые участвуют в расчёте,
/// чтобы пользователь не получал устаревший статус в пределах TTL.
/// </summary>
public sealed class EngagementCacheInvalidationInterceptor(ICacheService cache) : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Invalidate(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Invalidate(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Invalidate(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var changed = context.ChangeTracker
            .Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (changed.Count == 0)
        {
            return;
        }

        var userIds = new HashSet<Guid>();

        foreach (var entry in changed)
        {
            switch (entry.Entity)
            {
                case EventParticipant participant:
                    userIds.Add(participant.UserId);
                    break;
                case QuestParticipation participation:
                    userIds.Add(participation.UserId);
                    break;
                case SurveyResponse response:
                    userIds.Add(response.UserId);
                    break;
                case Thank thank:
                    userIds.Add(thank.FromUserId);
                    userIds.Add(thank.ToUserId);
                    break;
                case QuestStepCompletion:
                    // Шаг не хранит user_id: сбрасываем всю группу ключей вовлечённости.
                    cache.RemoveByPrefix(CacheKeys.EngagementPrefix);
                    break;
            }
        }

        foreach (var userId in userIds)
        {
            cache.Remove(CacheKeys.Engagement(userId));
        }
    }
}
