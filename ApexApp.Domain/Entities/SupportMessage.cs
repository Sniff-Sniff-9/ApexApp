using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class SupportMessage
{
    public long Id { get; set; }

    public long TicketId { get; set; }

    public Guid UserId { get; set; }

    public string Message { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual SupportTicket Ticket { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
