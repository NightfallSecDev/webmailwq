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
        private readonly SemaphoreSlim _signal = new(0);

        public SmtpQueueService(ILogger<SmtpQueueService> logger)
        {
            _logger = logger;
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
