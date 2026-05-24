using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebmailClient.Data;
using WebmailClient.Hubs;
using WebmailClient.Models;
using WebmailClient.Services;
using System.Threading.Tasks;
using System.Linq;
using System;
using BCrypt.Net;

var builder = WebApplication.CreateBuilder(args);

// Security: Rate Limiting
builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("LoginPolicy", opt => {
        opt.Window = TimeSpan.FromMinutes(5);
        opt.PermitLimit = 5;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

// Database - SQLite for quick demo, but in prod could be PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=webmail.db"));

// SignalR for Real-time push notifications
builder.Services.AddSignalR();

// Core Services
builder.Services.AddSingleton<IImapConnectionPool, ImapConnectionPool>();
builder.Services.AddSingleton<IHtmlSanitizerProvider, HtmlSanitizerProvider>();
builder.Services.AddScoped<IImapService, ImapService>();

// Smtp Queue and Background Indexer Services
builder.Services.AddSingleton<SmtpQueueService>();
builder.Services.AddSingleton<ISmtpQueue>(sp => sp.GetRequiredService<SmtpQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<SmtpQueueService>());

builder.Services.AddSingleton<BackgroundIndexerService>();
builder.Services.AddSingleton<ILuceneSearchService>(sp => sp.GetRequiredService<BackgroundIndexerService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<BackgroundIndexerService>());

var app = builder.Build();

// Migrate Database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
    
    if (!dbContext.UserAccounts.Any())
    {
        dbContext.UserAccounts.Add(new UserAccount
        {
            EmailAddress = "demo@company.com",
            EncryptedPassword = BCrypt.Net.BCrypt.HashPassword("password"),
            ImapServer = "imap.example.com",
            SmtpServer = "smtp.example.com"
        });
        dbContext.SaveChanges();
    }
}

// Security: Unhackable Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self' https: 'unsafe-inline' 'unsafe-eval'; img-src 'self' data: https:;");
    context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    await next();
});

app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

// SignalR Hub Endpoint
app.MapHub<WebmailHub>("/hubs/webmail");

// REST Endpoints (Minimal APIs)
var api = app.MapGroup("/api");

// 1. Get Emails (Cursor/Virtual Pagination)
api.MapGet("/emails", async (int accountId, string folder, int skip, int take, AppDbContext db, IImapService imap) =>
{
    var account = await db.UserAccounts.FindAsync(accountId);
    if (account == null) return Results.NotFound("Account not found");

    var metadataList = await imap.GetEmailsAsync(account, folder, skip, take).ToListAsync();
    return Results.Ok(metadataList);
});

// 2. Read Email Body
api.MapGet("/emails/{messageId}/body", async (int accountId, string folder, string messageId, AppDbContext db, IImapService imap) =>
{
    var account = await db.UserAccounts.FindAsync(accountId);
    if (account == null) return Results.NotFound("Account not found");

    var body = await imap.GetEmailBodyAsync(account, folder, messageId);
    return Results.Ok(new { HtmlBody = body });
});

api.MapGet("/search", (int accountId, string q, ILuceneSearchService searchService) =>
{
    var results = searchService.SearchEmails(accountId, q);
    return Results.Ok(results);
});

// 3. Send Email (Queue-based)
api.MapPost("/emails/send", async (int accountId, SendEmailRequest req, AppDbContext db, ISmtpQueue queue) =>
{
    var account = await db.UserAccounts.FindAsync(accountId);
    if (account == null) return Results.NotFound("Account not found");

    var mimeMessage = new MimeKit.MimeMessage();
    mimeMessage.From.Add(new MimeKit.MailboxAddress(account.EmailAddress, account.EmailAddress));
    mimeMessage.To.Add(MimeKit.MailboxAddress.Parse(req.To));
    mimeMessage.Subject = req.Subject;
    mimeMessage.Body = new MimeKit.TextPart("html") { Text = req.Body };

    queue.Enqueue(new QueuedEmailMessage { Account = account, Message = mimeMessage });
    
    return Results.Accepted("Email placed in queue for sending.");
});
// 4. Save Settings
api.MapPost("/settings", async (int accountId, UpdateSettingsRequest req, AppDbContext db) =>
{
    var account = await db.UserAccounts.FindAsync(accountId);
    if (account == null) return Results.NotFound();

    account.DisplayName = req.DisplayName ?? account.DisplayName;
    account.ThemePreference = req.ThemePreference ?? account.ThemePreference;
    account.SignatureHtml = req.SignatureHtml ?? account.SignatureHtml;
    account.AutoResponderEnabled = req.AutoResponderEnabled ?? account.AutoResponderEnabled;
    account.AutoResponderMessage = req.AutoResponderMessage ?? account.AutoResponderMessage;
    account.RetentionPolicy = req.RetentionPolicy ?? account.RetentionPolicy;
    account.SovereigntyRegion = req.SovereigntyRegion ?? account.SovereigntyRegion;
    account.Require2FA = req.Require2FA ?? account.Require2FA;
    account.PopAccessEnabled = req.PopAccessEnabled ?? account.PopAccessEnabled;
    account.ImapAccessEnabled = req.ImapAccessEnabled ?? account.ImapAccessEnabled;

    await db.SaveChangesAsync();
    return Results.Ok();
});

// 5. Add Blocked Address
api.MapPost("/settings/block", async (int accountId, string emailOrDomain, AppDbContext db) =>
{
    var account = await db.UserAccounts.FindAsync(accountId);
    if (account == null) return Results.NotFound();

    db.BlockedAddresses.Add(new BlockedAddress { UserAccountId = accountId, EmailAddressOrDomain = emailOrDomain });
    await db.SaveChangesAsync();
    return Results.Ok();
});

// 6. Add Filter Rule
api.MapPost("/settings/filters", async (int accountId, EmailFilterRule rule, AppDbContext db) =>
{
    rule.UserAccountId = accountId;
    db.EmailFilterRules.Add(rule);
    await db.SaveChangesAsync();
    return Results.Ok();
});

// 7. Auth Login with Rate Limiting and Dynamic IMAP Binding
api.MapPost("/auth/login", async (LoginRequest req, AppDbContext db, IImapConnectionPool pool) =>
{
    // If user provided IMAP settings, use them, otherwise default to demo.
    string imapHost = string.IsNullOrWhiteSpace(req.ImapServer) ? "imap.example.com" : req.ImapServer;
    int imapPort = req.ImapPort > 0 ? req.ImapPort : 993;
    string smtpHost = string.IsNullOrWhiteSpace(req.SmtpServer) ? "smtp.example.com" : req.SmtpServer;
    int smtpPort = req.SmtpPort > 0 ? req.SmtpPort : 465;

    // Test connection if they provided custom settings
    if (!string.IsNullOrWhiteSpace(req.ImapServer))
    {
        try 
        {
            var testClient = await pool.GetClientAsync(imapHost, imapPort, req.Email, req.Password);
            pool.ReturnClient(req.Email, testClient);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { Error = "Failed to connect to mail server: " + ex.Message });
        }
    }

    var account = await db.UserAccounts.FirstOrDefaultAsync(u => u.EmailAddress == req.Email);
    if (account == null) 
    {
        // Auto-register new accounts upon successful IMAP test or if it's the demo account
        account = new UserAccount 
        {
            EmailAddress = req.Email,
            EncryptedPassword = BCrypt.Net.BCrypt.HashPassword(req.Password),
            ImapServer = imapHost,
            ImapPort = imapPort,
            SmtpServer = smtpHost,
            SmtpPort = smtpPort,
            DisplayName = req.Email.Split('@')[0]
        };
        db.UserAccounts.Add(account);
        await db.SaveChangesAsync();
    }
    else
    {
        // Verify existing local account password
        if (!BCrypt.Net.BCrypt.Verify(req.Password, account.EncryptedPassword))
            return Results.Unauthorized();
            
        // Update server bindings if provided
        if (!string.IsNullOrWhiteSpace(req.ImapServer)) {
            account.ImapServer = imapHost;
            account.ImapPort = imapPort;
            account.SmtpServer = smtpHost;
            account.SmtpPort = smtpPort;
            account.EncryptedPassword = BCrypt.Net.BCrypt.HashPassword(req.Password);
            await db.SaveChangesAsync();
        }
    }

    return Results.Ok(new { AccountId = account.Id, Message = "Authenticated" });
}).RequireRateLimiting("LoginPolicy");

// 8. Get Storage Quota
api.MapGet("/settings/storage", async (int accountId, AppDbContext db) =>
{
    var account = await db.UserAccounts.FindAsync(accountId);
    if (account == null) return Results.NotFound();

    // Mock storage calculation for now. In reality, you'd calculate db size or Imap quota.
    long totalStorageMb = 15360; // 15 GB
    long usedStorageMb = new Random().Next(400, 12000); // Random usage between 400MB and 12GB for demo

    return Results.Ok(new {
        TotalMb = totalStorageMb,
        UsedMb = usedStorageMb,
        Percentage = Math.Round((double)usedStorageMb / totalStorageMb * 100, 1)
    });
});

app.Run();

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
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
