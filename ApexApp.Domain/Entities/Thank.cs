using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class Thank
{
    public long Id { get; set; }

    public Guid FromUserId { get; set; }

    public Guid ToUserId { get; set; }

    public long Amount { get; set; }

    public string? Message { get; set; }

    public bool IsPublic { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User FromUser { get; set; } = null!;

    public virtual User ToUser { get; set; } = null!;
}
