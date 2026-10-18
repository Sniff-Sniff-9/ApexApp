using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class EventParticipant
{
    public long Id { get; set; }

    public long EventId { get; set; }

    public Guid UserId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime RegisteredAt { get; set; }

    public DateTime? AttendedAt { get; set; }

    public bool RewardClaimed { get; set; }

    public virtual Event Event { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
