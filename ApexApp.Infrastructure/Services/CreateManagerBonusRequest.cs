using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Infrastructure.Services
{
    public sealed record CreateManagerBonusRequest(
    Guid ToUserId,
    long Amount,
    string? Message);
}
