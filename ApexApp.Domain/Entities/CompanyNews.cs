using System;
using System.Collections.Generic;
using System.Text;

namespace ApexApp.Domain.Entities
{
    public class CompanyNews
    {
        public long Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Summary { get; set; }

        public string Content { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        public Guid AuthorId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public virtual User? Author { get; set; }
    }
}
