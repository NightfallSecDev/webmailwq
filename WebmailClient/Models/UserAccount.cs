using System;
using System.Collections.Generic;

namespace WebmailClient.Models
{
    public class UserAccount
    {
        public int Id { get; set; }
        public string EmailAddress { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        
        // Encrypted credentials using AES
        public string EncryptedPassword { get; set; } = string.Empty;
        public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();
        
        // IMAP/SMTP Configurations
        public string ImapServer { get; set; } = string.Empty;
        public int ImapPort { get; set; } = 993;
        public bool ImapUseSsl { get; set; } = true;
        
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 465;
        public bool SmtpUseSsl { get; set; } = true;

        // Enterprise Settings
        public string ThemePreference { get; set; } = "dark";
        public string SignatureHtml { get; set; } = string.Empty;
        
        // Compliance
        public string RetentionPolicy { get; set; } = "Standard";
        public string SovereigntyRegion { get; set; } = "US-East";

        // Auto Responder
        public bool AutoResponderEnabled { get; set; } = false;
        public string AutoResponderMessage { get; set; } = string.Empty;
        public DateTime? AutoResponderStart { get; set; }
        public DateTime? AutoResponderEnd { get; set; }

        // Security
        public bool Require2FA { get; set; } = false;
        public bool RequireSmime { get; set; } = false;
        public bool PopAccessEnabled { get; set; } = false;
        public bool ImapAccessEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<EmailFilterRule> FilterRules { get; set; } = new List<EmailFilterRule>();
        public ICollection<BlockedAddress> BlockedAddresses { get; set; } = new List<BlockedAddress>();
        public ICollection<ActiveSession> ActiveSessions { get; set; } = new List<ActiveSession>();
    }
}
