using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Lucene.Net.Search;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WebmailClient.Services
{
    public interface ILuceneSearchService
    {
        void IndexEmail(string messageId, string subject, string body, string from, string folder, int accountId);
        System.Collections.Generic.IEnumerable<WebmailClient.Models.EmailMetadata> SearchEmails(int accountId, string queryText);
    }

    public class BackgroundIndexerService : BackgroundService, ILuceneSearchService
    {
        private readonly ILogger<BackgroundIndexerService> _logger;
        private readonly FSDirectory _dir;
        private readonly IndexWriter _writer;
        
        // This is a simplified simulation of indexing
        // In a real application, you'd use a queue of headers/bodies to index
        
        public BackgroundIndexerService(ILogger<BackgroundIndexerService> logger)
        {
            _logger = logger;
            
            var indexPath = Path.Combine(Environment.CurrentDirectory, "LuceneIndex");
            _dir = FSDirectory.Open(indexPath);
            
            // Note: LuceneVersion.LUCENE_48 is standard for Lucene.Net 4.8.0-beta00016
            var analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
            var indexConfig = new IndexWriterConfig(LuceneVersion.LUCENE_48, analyzer);
            _writer = new IndexWriter(_dir, indexConfig);
        }

        public void IndexEmail(string messageId, string subject, string body, string from, string folder, int accountId)
        {
            var doc = new Document
            {
                new StringField("MessageId", messageId, Field.Store.YES),
                new StringField("AccountId", accountId.ToString(), Field.Store.YES),
                new TextField("Subject", subject, Field.Store.YES),
                new TextField("Body", body, Field.Store.NO),
                new StringField("From", from, Field.Store.YES),
                new StringField("Folder", folder, Field.Store.YES)
            };

            // Using UpdateDocument to replace if it exists, otherwise add. 
            // In a real app we would construct a specific Term like new Term("MessageId", messageId)
            _writer.UpdateDocument(new Term("MessageId", messageId), doc);
            _writer.Commit();
        }
        public System.Collections.Generic.IEnumerable<WebmailClient.Models.EmailMetadata> SearchEmails(int accountId, string queryText)
        {
            var results = new System.Collections.Generic.List<WebmailClient.Models.EmailMetadata>();
            if (string.IsNullOrWhiteSpace(queryText)) return results;

            using var reader = DirectoryReader.Open(_dir);
            var searcher = new IndexSearcher(reader);
            
            // Build boolean query for accountId AND (Subject OR Body OR From)
            var analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
            var parser = new Lucene.Net.QueryParsers.Classic.MultiFieldQueryParser(
                LuceneVersion.LUCENE_48, 
                new[] { "Subject", "Body", "From" }, 
                analyzer);
                
            var textQuery = parser.Parse(queryText);
            
            var accountQuery = new TermQuery(new Term("AccountId", accountId.ToString()));
            
            var boolQuery = new BooleanQuery
            {
                { accountQuery, Occur.MUST },
                { textQuery, Occur.MUST }
            };

            var hits = searcher.Search(boolQuery, 50).ScoreDocs;
            
            foreach (var hit in hits)
            {
                var doc = searcher.Doc(hit.Doc);
                results.Add(new WebmailClient.Models.EmailMetadata
                {
                    MessageId = doc.Get("MessageId"),
                    AccountId = accountId,
                    Subject = doc.Get("Subject"),
                    From = doc.Get("From"),
                    Folder = doc.Get("Folder"),
                    Date = DateTimeOffset.Now // Can't easily retrieve date from this basic index structure unless we index it
                });
            }
            
            return results;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background Indexer Service is starting.");
            
            // Here, we would periodically poll accounts from DB, fetch un-indexed emails via IMAP,
            // and call IndexEmail. For demonstration, we simply yield to the framework.
            while (!stoppingToken.IsCancellationRequested)
            {
                // Delay to simulate a crawling cycle without blocking execution thread
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        public override void Dispose()
        {
            _writer?.Dispose();
            _dir?.Dispose();
            base.Dispose();
        }
    }
}
