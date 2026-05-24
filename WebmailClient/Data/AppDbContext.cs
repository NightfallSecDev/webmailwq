using Microsoft.EntityFrameworkCore;
using WebmailClient.Models;

namespace WebmailClient.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
        public DbSet<EmailFilterRule> EmailFilterRules => Set<EmailFilterRule>();
        public DbSet<BlockedAddress> BlockedAddresses => Set<BlockedAddress>();
        public DbSet<ActiveSession> ActiveSessions => Set<ActiveSession>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserAccount>().HasIndex(u => u.EmailAddress).IsUnique();
        }
    }
}
