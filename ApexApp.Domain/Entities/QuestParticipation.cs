using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class QuestParticipation
{
    public long Id { get; set; }

    public long QuestId { get; set; }

    public Guid UserId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public bool RewardClaimed { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Quest Quest { get; set; } = null!;

    public virtual ICollection<QuestStepCompletion> QuestStepCompletions { get; set; } = new List<QuestStepCompletion>();

    public virtual User User { get; set; } = null!;
}
