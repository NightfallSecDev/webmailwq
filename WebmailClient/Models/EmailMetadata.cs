using System;

namespace WebmailClient.Models
{
    public class EmailMetadata
    {
        public string MessageId { get; set; } = string.Empty;
        public int AccountId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public DateTimeOffset Date { get; set; }
        public bool IsRead { get; set; }
        public string Folder { get; set; } = string.Empty;
    }
}
