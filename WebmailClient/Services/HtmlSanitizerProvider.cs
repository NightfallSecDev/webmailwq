using Ganss.Xss;

namespace WebmailClient.Services
{
    public interface IHtmlSanitizerProvider
    {
        string Sanitize(string html);
    }

    public class HtmlSanitizerProvider : IHtmlSanitizerProvider
    {
        private readonly HtmlSanitizer _sanitizer;

        public HtmlSanitizerProvider()
        {
            _sanitizer = new HtmlSanitizer();
            // Additional custom allowed tags/attributes can be configured here
        }

        public string Sanitize(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            return _sanitizer.Sanitize(html);
        }
    }
}
