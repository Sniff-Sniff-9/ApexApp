using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class SupportTicket
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public TicketStatus Status { get; set; }

    public string Subject { get; set; } = null!;

    public Guid? AssignedTo { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? AssignedToNavigation { get; set; }

    public virtual ICollection<SupportMessage> SupportMessages { get; set; } = new List<SupportMessage>();

    public virtual User User { get; set; } = null!;
}
