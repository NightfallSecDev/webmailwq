using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using WebmailClient.Models;

namespace WebmailClient.Services
{
    public class QueuedEmailMessage
    {
        public UserAccount Account { get; set; } = new();
        public MimeMessage Message { get; set; } = new();
    }

    public interface ISmtpQueue
    {
        void Enqueue(QueuedEmailMessage message);
    }

    public class SmtpQueueService : BackgroundService, ISmtpQueue
    {
        private readonly ConcurrentQueue<QueuedEmailMessage> _queue = new();
        private readonly ILogger<SmtpQueueService> _logger;
        private readonly IImapConnectionPool _imapPool;
        private readonly SemaphoreSlim _signal = new(0);

        public SmtpQueueService(ILogger<SmtpQueueService> logger, IImapConnectionPool imapPool)
        {
            _logger = logger;
            _imapPool = imapPool;
        }

        public void Enqueue(QueuedEmailMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            _queue.Enqueue(message);
            _signal.Release();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _signal.WaitAsync(stoppingToken);

                if (_queue.TryDequeue(out var item))
                {
                    try
                    {
                        // Use async I/O
                        using var client = new SmtpClient();
                        var options = item.Account.SmtpPort == 25 ? SecureSocketOptions.None : SecureSocketOptions.Auto;
                        await client.ConnectAsync(item.Account.SmtpServer, item.Account.SmtpPort, options, stoppingToken);
                        
                        // Postfix on localhost allows relaying from 127.0.0.1 without auth.
                        // We cannot call AuthenticateAsync here because item.Account.EncryptedPassword is a BCrypt hash!
                        
                        await client.SendAsync(item.Message, stoppingToken);
                        await client.DisconnectAsync(true, stoppingToken);
                        
                        _logger.LogInformation("Successfully sent queued email for {Email}", item.Account.EmailAddress);

                        // Append to IMAP Sent folder
                        try 
                        {
                            var imapClient = await _imapPool.GetClientAsync(item.Account.ImapServer, item.Account.ImapPort, item.Account.EmailAddress, item.Account.EncryptedPassword, stoppingToken);
                            try 
                            {
                                MailKit.IMailFolder? sentFolder = null;
                                try {
                                    sentFolder = imapClient.GetFolder(MailKit.SpecialFolder.Sent) ?? await imapClient.GetFolderAsync("Sent", stoppingToken);
                                } catch (MailKit.FolderNotFoundException) {
                                    var root = imapClient.GetFolder(imapClient.PersonalNamespaces[0]);
                                    sentFolder = await root.CreateAsync("Sent", true, stoppingToken);
                                }

                                if (sentFolder != null) 
                                {
                                    await sentFolder.AppendAsync(item.Message, MailKit.MessageFlags.Seen, stoppingToken);
                                    _logger.LogInformation("Successfully appended email to Sent folder for {Email}", item.Account.EmailAddress);
                                }
                            }
                            finally
                            {
                                _imapPool.ReturnClient(item.Account.EmailAddress, imapClient);
                            }
                        }
                        catch (Exception imapEx)
                        {
                            _logger.LogWarning(imapEx, "Could not append sent message to IMAP Sent folder for {Email}", item.Account.EmailAddress);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send queued email for {Email}", item.Account.EmailAddress);
                    }
                }
            }
        }
    }
}
