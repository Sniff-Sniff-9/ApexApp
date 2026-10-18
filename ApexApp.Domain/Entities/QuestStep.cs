using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class QuestStep
{
    public long Id { get; set; }

    public long QuestId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsRequired { get; set; }

    public string? CompletionData { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Quest Quest { get; set; } = null!;

    public virtual ICollection<QuestStepCompletion> QuestStepCompletions { get; set; } = new List<QuestStepCompletion>();
}
