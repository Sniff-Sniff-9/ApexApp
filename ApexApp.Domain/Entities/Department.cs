using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class Department
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public int? ParentDepartmentId { get; set; }

        public Department? ParentDepartment { get; set; }

        public ICollection<Department> ChildDepartments { get; set; }
            = new List<Department>();

        public ICollection<User> Users { get; set; }
            = new List<User>();

        public bool IsActive { get; set; } = true;
    }
}
