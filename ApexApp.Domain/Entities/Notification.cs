using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ApexApp.Domain.Entities
{
    public class Notification
    {
        public long Id { get; set; }

        public Guid UserId { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public JsonDocument? Data { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTimeOffset CreatedAt { get; set; }
    }
}
