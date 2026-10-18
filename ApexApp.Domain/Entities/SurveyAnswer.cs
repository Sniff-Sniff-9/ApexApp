using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class SurveyAnswer
{
    public long Id { get; set; }

    public long ResponseId { get; set; }

    public long SurveyId { get; set; }

    public long QuestionId { get; set; }

    public string Answer { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual SurveyQuestion SurveyQuestion { get; set; } = null!;

    public virtual SurveyResponse SurveyResponse { get; set; } = null!;
}
