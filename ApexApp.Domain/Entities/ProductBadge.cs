using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class ProductBadge
    {
        public int Id { get; set; }

        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;

        public string Color { get; set; } = null!;

        public string AssignmentType { get; set; } = null!;

        public bool IsActive { get; set; }
        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public virtual ICollection<ProductBadgeAssignment> Assignments { get; set; }
            = new List<ProductBadgeAssignment>();

        public virtual ProductBadgeSetting? Settings { get; set; }
    }
}
