using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class QuestStepCompletion
{
    public long Id { get; set; }

    public long ParticipationId { get; set; }

    public long QuestId { get; set; }

    public long StepId { get; set; }

    public DateTime CompletedAt { get; set; }

    public string? Proof { get; set; }

    public virtual QuestParticipation QuestParticipation { get; set; } = null!;

    public virtual ICollection<QuestProof> QuestProofs { get; set; } = new List<QuestProof>();

    public virtual QuestStep QuestStep { get; set; } = null!;
}
