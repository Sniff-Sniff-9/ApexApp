using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class Transaction
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public long Amount { get; set; }

    public long BalanceAfter { get; set; }

    public string? Description { get; set; }

    public Guid? RelatedUserId { get; set; }

    public string? RelatedEntityType { get; set; }

    public long? RelatedEntityId { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual User? RelatedUser { get; set; }

    public virtual User User { get; set; } = null!;
}
