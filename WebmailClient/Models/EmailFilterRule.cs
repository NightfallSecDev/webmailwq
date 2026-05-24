using System;

namespace WebmailClient.Models
{
    public class EmailFilterRule
    {
        public int Id { get; set; }
        public int UserAccountId { get; set; }
        public UserAccount Account { get; set; } = null!;

        public string RuleName { get; set; } = string.Empty;
        
        // Match conditions (comma separated or JSON)
        public string MatchFrom { get; set; } = string.Empty;
        public string MatchSubject { get; set; } = string.Empty;
        
        // Actions
        public string ActionType { get; set; } = "MoveToFolder"; // MoveToFolder, MarkAsRead, Delete
        public string TargetFolder { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
