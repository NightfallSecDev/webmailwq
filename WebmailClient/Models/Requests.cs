namespace WebmailClient.Models
{
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ImapServer { get; set; } = string.Empty;
        public int ImapPort { get; set; }
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
    }

    public class UpdateSettingsRequest
    {
        public string? DisplayName { get; set; }
        public string? ThemePreference { get; set; }
        public string? SignatureHtml { get; set; }
        public bool? AutoResponderEnabled { get; set; }
        public string? AutoResponderMessage { get; set; }
        public string? RetentionPolicy { get; set; }
        public string? SovereigntyRegion { get; set; }
        public bool? Require2FA { get; set; }
        public bool? PopAccessEnabled { get; set; }
        public bool? ImapAccessEnabled { get; set; }
    }

    public class SendEmailRequest
    {
        public string To { get; set; } = string.Empty;
        public string Cc { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}
