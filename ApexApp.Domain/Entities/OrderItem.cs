using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class OrderItem
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public int? ProductId { get; set; }

    public int Quantity { get; set; }

    public long PriceAtPurchase { get; set; }

    public string? DigitalCode { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Product? Product { get; set; }
}
