using System;

namespace WebmailClient.Models
{
    public class ActiveSession
    {
        public int Id { get; set; }
        public int UserAccountId { get; set; }
        public UserAccount Account { get; set; } = null!;

        public string DeviceName { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        
        public DateTime LastActive { get; set; } = DateTime.UtcNow;
        public bool IsRevoked { get; set; } = false;
    }
}
