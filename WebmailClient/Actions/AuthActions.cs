using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WebmailClient.Data;
using WebmailClient.Models;
using WebmailClient.Services;
using System;
using System.Threading.Tasks;

namespace WebmailClient.Actions
{
    public static class AuthActions
    {
        public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
        {
            var api = routes.MapGroup("/api/auth");
            
            // 7. Auth Login — Test IMAP credentials FIRST (like Roundcube), then upsert DB record
            api.MapPost("/login", async (LoginRequest req, AppDbContext db, IImapConnectionPool pool) =>
            {
                if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                    return Results.BadRequest(new { Error = "Email and password are required." });

                // Default to localhost if no custom server provided
                string imapHost = string.IsNullOrWhiteSpace(req.ImapServer) ? "localhost" : req.ImapServer;
                int imapPort   = req.ImapPort > 0 ? req.ImapPort : 143;
                string smtpHost = string.IsNullOrWhiteSpace(req.SmtpServer) ? "localhost" : req.SmtpServer;
                int smtpPort   = req.SmtpPort > 0 ? req.SmtpPort : 25;

                // ALWAYS test IMAP first — the real credential check (like Roundcube)
                try 
                {
                    var testClient = await pool.GetClientAsync(imapHost, imapPort, req.Email, req.Password);
                    pool.ReturnClient(req.Email, testClient);
                }
                catch (Exception)
                {
                    return Results.Unauthorized();
                }

                // If IMAP succeeded, upsert the local DB record
                var account = await db.UserAccounts.FirstOrDefaultAsync(u => u.EmailAddress == req.Email);
                if (account == null) 
                {
                    account = new UserAccount 
                    {
                        EmailAddress  = req.Email,
                        EncryptedPassword = req.Password,   // Plaintext stored; only used for IMAP re-auth
                        ImapServer    = imapHost,
                        ImapPort      = imapPort,
                        SmtpServer    = smtpHost,
                        SmtpPort      = smtpPort,
                        DisplayName   = req.Email.Split('@')[0]
                    };
                    db.UserAccounts.Add(account);
                }
                else
                {
                    // Update server bindings and refresh stored password
                    account.ImapServer = imapHost;
                    account.ImapPort   = imapPort;
                    account.SmtpServer = smtpHost;
                    account.SmtpPort   = smtpPort;
                    account.EncryptedPassword = req.Password;
                }
                await db.SaveChangesAsync();

                return Results.Ok(new { AccountId = account.Id, Email = account.EmailAddress, DisplayName = account.DisplayName, Message = "Authenticated" });
            }).RequireRateLimiting("LoginPolicy");
        }
    }
}
