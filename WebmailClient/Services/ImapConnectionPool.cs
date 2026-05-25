using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Logging;

namespace WebmailClient.Services
{
    public interface IImapConnectionPool
    {
        Task<ImapClient> GetClientAsync(string server, int port, string email, string password, CancellationToken cancellationToken = default);
        void ReturnClient(string email, ImapClient client);
    }

    public class ImapConnectionPool : IImapConnectionPool, IDisposable
    {
        private readonly ConcurrentDictionary<string, ConcurrentQueue<ImapClient>> _pool = new();
        private readonly ILogger<ImapConnectionPool> _logger;

        public ImapConnectionPool(ILogger<ImapConnectionPool> logger)
        {
            _logger = logger;
        }

        public async Task<ImapClient> GetClientAsync(string server, int port, string email, string password, CancellationToken cancellationToken = default)
        {
            var queue = _pool.GetOrAdd(email, _ => new ConcurrentQueue<ImapClient>());

            while (queue.TryDequeue(out var cachedClient))
            {
                if (cachedClient.IsConnected && cachedClient.IsAuthenticated)
                {
                    _logger.LogInformation("Reusing existing IMAP connection for {Email}", email);
                    return cachedClient;
                }
                else
                {
                    cachedClient.Dispose();
                }
            }

            _logger.LogInformation("Creating new IMAP connection for {Email}", email);
            var client = new ImapClient();
            var options = port == 143 ? SecureSocketOptions.None : SecureSocketOptions.Auto;
            await client.ConnectAsync(server, port, options, cancellationToken);
            
            var username = email.Contains("@") ? email.Split('@')[0] : email;
            await client.AuthenticateAsync(username, password, cancellationToken);
            return client;
        }

        public void ReturnClient(string email, ImapClient client)
        {
            if (client.IsConnected && client.IsAuthenticated)
            {
                var queue = _pool.GetOrAdd(email, _ => new ConcurrentQueue<ImapClient>());
                queue.Enqueue(client);
            }
            else
            {
                client.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var queue in _pool.Values)
            {
                while (queue.TryDequeue(out var client))
                {
                    client.Dispose();
                }
            }
        }
    }
}
