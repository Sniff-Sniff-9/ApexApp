using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class Balance
{
    public Guid UserId { get; set; }

    public BalanceType Type { get; set; }

    public long Amount { get; set; }

    public int Version { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
