# System Architecture

```text
[ User UI ] ──(Email/Password)──► [ ASP.NET Core API ]
                                          │
                        ┌─────────────────┴─────────────────┐
                        ▼ (IMAP Protocol - Port 993)        ▼ (SMTP Protocol - Port 587)
             [ Fetch & Browse Mailboxes ]          [ Construct & Send Outbound Mail ]
                        │                                   │
                        ▼                                   ▼
             [ Remote Mail Server ]              [ Remote Mail Server ]
```

## Description
This diagram perfectly outlines the core data flow of the Webmail Enterprise backend.
- **Frontend UI**: Built with vanilla HTML/CSS/JS and an active glassmorphic interface.
- **Minimal API**: Operates as a stateless proxy and translation layer using `MailKit` and `MimeKit`.
- **IMAP**: Handles real-time polling and synchronization for `INBOX`, `Sent`, `Spam`, etc.
- **SMTP**: Utilizes an async `SmtpQueueService` to safely construct and push outbound messages to remote exchanges without blocking the main UI thread.
