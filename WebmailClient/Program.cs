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
using WebmailClient.Actions;

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

// Database - Support for SQLite, MySQL, and PostgreSQL
string dbProvider = builder.Configuration["DatabaseProvider"] ?? Environment.GetEnvironmentVariable("DB_PROVIDER") ?? "sqlite";
string dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") 
    ?? "Data Source=webmail.db";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (dbProvider.Equals("mysql", StringComparison.OrdinalIgnoreCase))
    {
        var serverVersion = Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(dbConnectionString);
        options.UseMySql(dbConnectionString, serverVersion);
    }
    else if (dbProvider.Equals("postgresql", StringComparison.OrdinalIgnoreCase) || dbProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(dbConnectionString);
    }
    else
    {
        options.UseSqlite(dbConnectionString);
    }
});

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
            EmailAddress = "alice@myserver.local",
            EncryptedPassword = "password",
            ImapServer = "localhost",
            ImapPort = 143,
            SmtpServer = "localhost",
            SmtpPort = 25,
            DisplayName = "Alice"
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

// REST Endpoints (Actions)
app.MapAuthEndpoints();
app.MapMailEndpoints();
app.MapSettingsEndpoints();
app.MapContactsEndpoints();

app.Run();
