using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
