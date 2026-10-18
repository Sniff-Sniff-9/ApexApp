using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class SurveyResponse
{
    public long Id { get; set; }

    public long SurveyId { get; set; }

    public Guid UserId { get; set; }

    public DateTime SubmittedAt { get; set; }

    public virtual Survey Survey { get; set; } = null!;

    public virtual ICollection<SurveyAnswer> SurveyAnswers { get; set; } = new List<SurveyAnswer>();

    public virtual User User { get; set; } = null!;
}
