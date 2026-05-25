using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using WebmailClient.Models;
using Microsoft.Extensions.Logging;

namespace WebmailClient.Services
{
    public interface IImapService
    {
        IAsyncEnumerable<EmailMetadata> GetEmailsAsync(UserAccount account, string folderName = "INBOX", int skip = 0, int take = 50, CancellationToken cancellationToken = default);
        Task<string> GetEmailBodyAsync(UserAccount account, string folderName, string messageId, CancellationToken cancellationToken = default);
    }

    public class ImapService : IImapService
    {
        private readonly IImapConnectionPool _pool;
        private readonly IHtmlSanitizerProvider _sanitizer;
        private readonly ILogger<ImapService> _logger;

        public ImapService(IImapConnectionPool pool, IHtmlSanitizerProvider sanitizer, ILogger<ImapService> logger)
        {
            _pool = pool;
            _sanitizer = sanitizer;
            _logger = logger;
        }

        public async IAsyncEnumerable<EmailMetadata> GetEmailsAsync(
            UserAccount account, 
            string folderName = "INBOX", 
            int skip = 0, 
            int take = 50, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var client = await _pool.GetClientAsync(account.ImapServer, account.ImapPort, account.EmailAddress, account.EncryptedPassword, cancellationToken);
            try
            {
                IMailFolder folder;
                try {
                    if (folderName.Equals("inbox", StringComparison.OrdinalIgnoreCase)) folder = client.Inbox;
                    else if (folderName.Equals("sent", StringComparison.OrdinalIgnoreCase)) folder = client.GetFolder(SpecialFolder.Sent) ?? await client.GetFolderAsync("Sent", cancellationToken);
                    else if (folderName.Equals("drafts", StringComparison.OrdinalIgnoreCase)) folder = client.GetFolder(SpecialFolder.Drafts) ?? await client.GetFolderAsync("Drafts", cancellationToken);
                    else if (folderName.Equals("spam", StringComparison.OrdinalIgnoreCase)) folder = client.GetFolder(SpecialFolder.Junk) ?? await client.GetFolderAsync("Junk", cancellationToken);
                    else if (folderName.Equals("trash", StringComparison.OrdinalIgnoreCase)) folder = client.GetFolder(SpecialFolder.Trash) ?? await client.GetFolderAsync("Trash", cancellationToken);
                    else folder = await client.GetFolderAsync(folderName, cancellationToken);
                } catch (FolderNotFoundException) {
                    yield break;
                }

                await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

                int totalCount = folder.Count;
                int start = Math.Max(0, totalCount - 1 - skip);
                int end = Math.Max(0, start - take + 1);

                if (totalCount > 0)
                {
                    // Fetch headers in chunks/pagination
                    var summaries = await folder.FetchAsync(end, start, MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.UniqueId, cancellationToken);

                    // Iterate in reverse for newest first
                    for (int i = summaries.Count - 1; i >= 0; i--)
                    {
                        var summary = summaries[i];
                        yield return new EmailMetadata
                        {
                            MessageId = summary.Envelope?.MessageId ?? summary.UniqueId.Id.ToString(),
                            AccountId = account.Id,
                            Subject = summary.Envelope?.Subject ?? "(No Subject)",
                            From = summary.Envelope?.From.Count > 0 ? summary.Envelope.From[0].Name ?? summary.Envelope.From[0].ToString() : "Unknown",
                            Date = summary.Envelope?.Date ?? DateTimeOffset.MinValue,
                            IsRead = summary.Flags?.HasFlag(MessageFlags.Seen) ?? false,
                            Folder = folderName
                        };
                    }
                }
            }
            finally
            {
                _pool.ReturnClient(account.EmailAddress, client);
            }
        }

        public async Task<string> GetEmailBodyAsync(UserAccount account, string folderName, string messageId, CancellationToken cancellationToken = default)
        {
            var client = await _pool.GetClientAsync(account.ImapServer, account.ImapPort, account.EmailAddress, account.EncryptedPassword, cancellationToken);
            try
            {
                IMailFolder folder;
                try {
                    if (folderName.Equals("inbox", StringComparison.OrdinalIgnoreCase)) folder = client.Inbox;
                    else if (folderName.Equals("sent", StringComparison.OrdinalIgnoreCase)) folder = client.GetFolder(SpecialFolder.Sent) ?? await client.GetFolderAsync("Sent", cancellationToken);
                    else if (folderName.Equals("drafts", StringComparison.OrdinalIgnoreCase)) folder = client.GetFolder(SpecialFolder.Drafts) ?? await client.GetFolderAsync("Drafts", cancellationToken);
                    else if (folderName.Equals("spam", StringComparison.OrdinalIgnoreCase)) folder = client.GetFolder(SpecialFolder.Junk) ?? await client.GetFolderAsync("Junk", cancellationToken);
                    else if (folderName.Equals("trash", StringComparison.OrdinalIgnoreCase)) folder = client.GetFolder(SpecialFolder.Trash) ?? await client.GetFolderAsync("Trash", cancellationToken);
                    else folder = await client.GetFolderAsync(folderName, cancellationToken);
                } catch (FolderNotFoundException) {
                    return string.Empty;
                }

                await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

                // For simplicity, we search by Message-Id header or UID if implemented that way.
                // Normally you'd parse the uniqueId from the messageId string if that's how it's stored.
                // Assuming messageId is mapped to UniqueId for this example.
                if (UniqueId.TryParse(messageId, out var uid))
                {
                    var message = await folder.GetMessageAsync(uid, cancellationToken);
                    var htmlBody = message.HtmlBody ?? message.TextBody ?? string.Empty;
                    return _sanitizer.Sanitize(htmlBody); // Strict HTML Sanitization
                }
                
                return string.Empty;
            }
            finally
            {
                _pool.ReturnClient(account.EmailAddress, client);
            }
        }
    }
}
