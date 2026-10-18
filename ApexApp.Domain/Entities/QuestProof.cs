using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class QuestProof
{
    public long Id { get; set; }

    public long CompletionId { get; set; }

    public string StoragePath { get; set; } = null!;

    public string? Comment { get; set; }

    public string Status { get; set; } = null!;

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual QuestStepCompletion Completion { get; set; } = null!;

    public virtual User? ReviewedByNavigation { get; set; }
}
