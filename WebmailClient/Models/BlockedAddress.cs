using System;

namespace WebmailClient.Models
{
    public class BlockedAddress
    {
        public int Id { get; set; }
        public int UserAccountId { get; set; }
        public UserAccount Account { get; set; } = null!;

        public string EmailAddressOrDomain { get; set; } = string.Empty;
        public DateTime BlockedAt { get; set; } = DateTime.UtcNow;
    }
}
