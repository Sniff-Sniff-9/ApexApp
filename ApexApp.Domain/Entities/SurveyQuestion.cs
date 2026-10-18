using System;
using System.Collections.Generic;

namespace ApexApp.Domain.Entities;

public partial class SurveyQuestion
{
    public long Id { get; set; }

    public long SurveyId { get; set; }

    public string QuestionText { get; set; } = null!;

    public string QuestionType { get; set; } = null!;

    public string? Options { get; set; }

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Survey Survey { get; set; } = null!;

    public virtual ICollection<SurveyAnswer> SurveyAnswers { get; set; } = new List<SurveyAnswer>();
}
