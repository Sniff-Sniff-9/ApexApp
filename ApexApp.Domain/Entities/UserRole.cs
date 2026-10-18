using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class UserRole
{
    public Guid UserId { get; set; }

    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }

    public virtual Role Role { get; set; } = null!;
}
