using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class Quest
{
    public long Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public long RewardAmount { get; set; }

    public bool IsActive { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<QuestParticipation> QuestParticipations { get; set; } = new List<QuestParticipation>();

    public virtual ICollection<QuestStep> QuestSteps { get; set; } = new List<QuestStep>();
}
