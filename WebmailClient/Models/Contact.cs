namespace WebmailClient.Models
{
    public class Contact
    {
        public int Id { get; set; }
        public int UserAccountId { get; set; }
        public UserAccount? UserAccount { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Company { get; set; }
    }
}
