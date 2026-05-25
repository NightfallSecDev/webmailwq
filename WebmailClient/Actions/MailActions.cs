using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WebmailClient.Data;
using WebmailClient.Models;
using WebmailClient.Services;
using System;
using System.Linq;

namespace WebmailClient.Actions
{
    public static class MailActions
    {
        public static void MapMailEndpoints(this IEndpointRouteBuilder routes)
        {
            var api = routes.MapGroup("/api");

            api.MapGet("/emails", async (int accountId, string folder, int skip, int take, AppDbContext db, IImapService imap) =>
            {
                var account = await db.UserAccounts.FindAsync(accountId);
                if (account == null) return Results.NotFound("Account not found");

                var metadataList = await imap.GetEmailsAsync(account, folder, skip, take).ToListAsync();
                return Results.Ok(metadataList);
            });

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

            api.MapPost("/emails/send", async (int accountId, SendEmailRequest req, AppDbContext db, ISmtpQueue queue) =>
            {
                var account = await db.UserAccounts.FindAsync(accountId);
                if (account == null) return Results.NotFound("Account not found");

                var mimeMessage = new MimeKit.MimeMessage();
                mimeMessage.From.Add(new MimeKit.MailboxAddress(account.DisplayName ?? account.EmailAddress, account.EmailAddress));
                mimeMessage.To.Add(MimeKit.MailboxAddress.Parse(req.To));
                if (!string.IsNullOrWhiteSpace(req.Cc))
                    foreach (var cc in req.Cc.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        mimeMessage.Cc.Add(MimeKit.MailboxAddress.Parse(cc.Trim()));
                mimeMessage.Subject = req.Subject;
                mimeMessage.Body = new MimeKit.TextPart("html") { Text = req.Body };

                queue.Enqueue(new QueuedEmailMessage { Account = account, Message = mimeMessage });
                return Results.Accepted("Email placed in queue for sending.");
            });

            api.MapDelete("/emails/{messageId}", async (int accountId, string folder, string messageId, AppDbContext db, IImapConnectionPool pool) =>
            {
                var account = await db.UserAccounts.FindAsync(accountId);
                if (account == null) return Results.NotFound();
                try
                {
                    var client = await pool.GetClientAsync(account.ImapServer, account.ImapPort, account.EmailAddress, account.EncryptedPassword);
                    try
                    {
                        var f = folder.Equals("inbox", StringComparison.OrdinalIgnoreCase) ? client.Inbox : await client.GetFolderAsync(folder);
                        await f.OpenAsync(MailKit.FolderAccess.ReadWrite);
                        if (MailKit.UniqueId.TryParse(messageId, out var uid))
                        {
                            var storeReq = new MailKit.StoreFlagsRequest(MailKit.StoreAction.Add, MailKit.MessageFlags.Deleted) { Silent = true };
                            await f.StoreAsync(uid, storeReq);
                            await f.ExpungeAsync();
                        }
                    }
                    finally { pool.ReturnClient(account.EmailAddress, client); }
                    return Results.Ok();
                }
                catch (Exception ex) { return Results.BadRequest(ex.Message); }
            });

            api.MapPost("/emails/{messageId}/mark", async (int accountId, string folder, string messageId, bool read, AppDbContext db, IImapConnectionPool pool) =>
            {
                var account = await db.UserAccounts.FindAsync(accountId);
                if (account == null) return Results.NotFound();
                try
                {
                    var client = await pool.GetClientAsync(account.ImapServer, account.ImapPort, account.EmailAddress, account.EncryptedPassword);
                    try
                    {
                        var f = folder.Equals("inbox", StringComparison.OrdinalIgnoreCase) ? client.Inbox : await client.GetFolderAsync(folder);
                        await f.OpenAsync(MailKit.FolderAccess.ReadWrite);
                        if (MailKit.UniqueId.TryParse(messageId, out var uid))
                        {
                            var action = read ? MailKit.StoreAction.Add : MailKit.StoreAction.Remove;
                            var storeReq = new MailKit.StoreFlagsRequest(action, MailKit.MessageFlags.Seen) { Silent = true };
                            await f.StoreAsync(uid, storeReq);
                        }
                    }
                    finally { pool.ReturnClient(account.EmailAddress, client); }
                    return Results.Ok();
                }
                catch (Exception ex) { return Results.BadRequest(ex.Message); }
            });

            api.MapPost("/emails/{messageId}/move", async (int accountId, string folder, string messageId, string targetFolder, AppDbContext db, IImapConnectionPool pool) =>
            {
                var account = await db.UserAccounts.FindAsync(accountId);
                if (account == null) return Results.NotFound();
                try
                {
                    var client = await pool.GetClientAsync(account.ImapServer, account.ImapPort, account.EmailAddress, account.EncryptedPassword);
                    try
                    {
                        var src = folder.Equals("inbox", StringComparison.OrdinalIgnoreCase) ? client.Inbox : await client.GetFolderAsync(folder);
                        await src.OpenAsync(MailKit.FolderAccess.ReadWrite);
                        if (MailKit.UniqueId.TryParse(messageId, out var uid))
                        {
                            MailKit.IMailFolder? dest = null;
                            try { dest = await client.GetFolderAsync(targetFolder); } catch { }
                            if (dest == null)
                            {
                                var root = client.GetFolder(client.PersonalNamespaces[0]);
                                dest = await root.CreateAsync(targetFolder, true);
                            }
                            await src.MoveToAsync(uid, dest);
                        }
                    }
                    finally { pool.ReturnClient(account.EmailAddress, client); }
                    return Results.Ok();
                }
                catch (Exception ex) { return Results.BadRequest(ex.Message); }
            });

            api.MapPost("/emails/{messageId}/star", async (int accountId, string folder, string messageId, bool star, AppDbContext db, IImapConnectionPool pool) =>
            {
                var account = await db.UserAccounts.FindAsync(accountId);
                if (account == null) return Results.NotFound();
                try
                {
                    var client = await pool.GetClientAsync(account.ImapServer, account.ImapPort, account.EmailAddress, account.EncryptedPassword);
                    try
                    {
                        var f = folder.Equals("inbox", StringComparison.OrdinalIgnoreCase) ? client.Inbox : await client.GetFolderAsync(folder);
                        await f.OpenAsync(MailKit.FolderAccess.ReadWrite);
                        if (MailKit.UniqueId.TryParse(messageId, out var uid))
                        {
                            var action = star ? MailKit.StoreAction.Add : MailKit.StoreAction.Remove;
                            var storeReq = new MailKit.StoreFlagsRequest(action, MailKit.MessageFlags.Flagged) { Silent = true };
                            await f.StoreAsync(uid, storeReq);
                        }
                    }
                    finally { pool.ReturnClient(account.EmailAddress, client); }
                    return Results.Ok();
                }
                catch (Exception ex) { return Results.BadRequest(ex.Message); }
            });

            api.MapPost("/emails/drafts", async (int accountId, SendEmailRequest req, AppDbContext db, IImapConnectionPool pool) =>
            {
                var account = await db.UserAccounts.FindAsync(accountId);
                if (account == null) return Results.NotFound();
                try
                {
                    var mimeMessage = new MimeKit.MimeMessage();
                    mimeMessage.From.Add(new MimeKit.MailboxAddress(account.DisplayName ?? account.EmailAddress, account.EmailAddress));
                    if (!string.IsNullOrWhiteSpace(req.To)) mimeMessage.To.Add(MimeKit.MailboxAddress.Parse(req.To));
                    if (!string.IsNullOrWhiteSpace(req.Cc))
                        foreach (var cc in req.Cc.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            mimeMessage.Cc.Add(MimeKit.MailboxAddress.Parse(cc.Trim()));
                    mimeMessage.Subject = req.Subject;
                    mimeMessage.Body = new MimeKit.TextPart("html") { Text = req.Body };

                    var client = await pool.GetClientAsync(account.ImapServer, account.ImapPort, account.EmailAddress, account.EncryptedPassword);
                    try
                    {
                        var drafts = client.GetFolder(MailKit.SpecialFolder.Drafts) ?? await client.GetFolderAsync("Drafts");
                        var appendReq = new MailKit.AppendRequest(mimeMessage) { Flags = MailKit.MessageFlags.Draft | MailKit.MessageFlags.Seen, InternalDate = DateTimeOffset.Now };
                        await drafts.AppendAsync(appendReq);
                    }
                    finally { pool.ReturnClient(account.EmailAddress, client); }
                    return Results.Ok();
                }
                catch (Exception ex) { return Results.BadRequest(ex.Message); }
            });

            api.MapGet("/folders", async (int accountId, AppDbContext db, IImapConnectionPool pool) =>
            {
                var account = await db.UserAccounts.FindAsync(accountId);
                if (account == null) return Results.NotFound();
                try
                {
                    var client = await pool.GetClientAsync(account.ImapServer, account.ImapPort, account.EmailAddress, account.EncryptedPassword);
                    try
                    {
                        var root = client.GetFolder(client.PersonalNamespaces[0]);
                        var folders = await root.GetSubfoldersAsync(false);
                        return Results.Ok(folders.Select(f => new { f.Name, f.FullName, Attributes = f.Attributes.ToString() }));
                    }
                    finally { pool.ReturnClient(account.EmailAddress, client); }
                }
                catch (Exception ex) { return Results.BadRequest(ex.Message); }
            });
        }
    }
}
