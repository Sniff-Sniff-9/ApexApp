using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class CartItem
{
    public long Id { get; set; }

    public Guid UserId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public DateTime AddedAt { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
