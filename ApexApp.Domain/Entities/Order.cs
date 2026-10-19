using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class Order
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public OrderStatus Status { get; set; }

    public long TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? ProcessedBy { get; set; }

    public string? Comment { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual User? ProcessedByNavigation { get; set; }

    public virtual User User { get; set; } = null!;
}
