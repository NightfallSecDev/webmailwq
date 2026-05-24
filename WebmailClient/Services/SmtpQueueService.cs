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
                        await client.ConnectAsync(item.Account.SmtpServer, item.Account.SmtpPort, SecureSocketOptions.Auto, stoppingToken);
                        await client.AuthenticateAsync(item.Account.EmailAddress, item.Account.EncryptedPassword, stoppingToken);
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
