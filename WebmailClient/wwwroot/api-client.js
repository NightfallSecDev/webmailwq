// Centralized API Client
const API = {
    async getEmails(accountId, folder = 'INBOX', skip = 0, take = 50) {
        try {
            const res = await fetch(`/api/emails?accountId=${accountId}&folder=${folder}&skip=${skip}&take=${take}`);
            if (!res.ok) throw new Error('Failed to fetch emails');
            return await res.json();
        } catch (e) {
            console.error(e);
            return [];
        }
    },
    
    async getEmailBody(accountId, folder, messageId) {
        try {
            const res = await fetch(`/api/emails/${encodeURIComponent(messageId)}/body?accountId=${accountId}&folder=${folder}`);
            if (!res.ok) throw new Error('Failed to fetch email body');
            const data = await res.json();
            return data.htmlBody;
        } catch (e) {
            console.error(e);
            return '<div style="padding:20px;color:red;">Error loading email body</div>';
        }
    },
    
    async sendEmail(accountId, to, subject, body) {
        try {
            const res = await fetch(`/api/emails/send?accountId=${accountId}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ to, subject, body })
            });
            if (!res.ok) throw new Error('Failed to send email');
            return true;
        } catch (e) {
            console.error(e);
            return false;
        }
    },

    async saveSettings(accountId, settings) {
        try {
            const res = await fetch(`/api/settings?accountId=${accountId}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(settings)
            });
            return res.ok;
        } catch (e) {
            console.error(e);
            return false;
        }
    },

    async blockAddress(accountId, emailOrDomain) {
        try {
            const res = await fetch(`/api/settings/block?accountId=${accountId}&emailOrDomain=${encodeURIComponent(emailOrDomain)}`, { method: 'POST' });
            return res.ok;
        } catch(e) { console.error(e); return false; }
    },

    async login(email, password, imapServer, imapPort, smtpServer, smtpPort) {
        try {
            const res = await fetch('/api/auth/login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ 
                    email, password, 
                    imapServer: imapServer || '', 
                    imapPort: imapPort || 0,
                    smtpServer: smtpServer || '',
                    smtpPort: smtpPort || 0
                })
            });
            
            if (res.status === 429) {
                alert('Too many login attempts. Please try again in 5 minutes.');
                return null;
            }
            if (!res.ok) {
                try {
                    const errObj = await res.json();
                    if (errObj && errObj.error) {
                        alert(errObj.error);
                        return null;
                    }
                } catch(e) {}
                alert('Invalid credentials or failed to connect to mail server.');
                return null;
            }
            return await res.json();
        } catch(e) { console.error(e); return null; }
    },
    
    async getStorage(accountId) {
        try {
            const res = await fetch(`/api/settings/storage?accountId=${accountId}`);
            if (!res.ok) return null;
            return await res.json();
        } catch(e) { console.error(e); return null; }
    },

    async search(accountId, query) {
        try {
            const res = await fetch(`/api/search?accountId=${accountId}&q=${encodeURIComponent(query)}`);
            if (!res.ok) throw new Error('Failed to search');
            return await res.json();
        } catch(e) { console.error(e); return []; }
    }
};

// Auto-initialize when loaded
document.addEventListener('DOMContentLoaded', async () => {
    // Load storage quota asynchronously without blocking
    const accountId = localStorage.getItem('accountId') || 1; 
    API.getStorage(accountId).then(storageData => {
        if (storageData) {
            const textEl = document.getElementById('storage-text');
            const fillEl = document.getElementById('storage-fill');
            if (textEl && fillEl) {
                textEl.textContent = `${(storageData.usedMb / 1024).toFixed(1)} GB / ${(storageData.totalMb / 1024).toFixed(1)} GB`;
                fillEl.style.width = `${storageData.percentage}%`;
                
                if (storageData.percentage > 90) fillEl.className = 'storage-fill danger';
                else if (storageData.percentage > 75) fillEl.className = 'storage-fill warn';
            }
        }
    }).catch(e => console.error(e));

    // Determine the current page
    const path = window.location.pathname;
    let folder = '';
    let emptyIcon = 'fa-inbox';
    let emptyMsg = 'Your inbox is empty';

    if (path.includes('index.html') || path === '/' || path.endsWith('/')) {
        folder = 'INBOX';
    } else if (path.includes('sent.html')) {
        folder = 'Sent';
        emptyIcon = 'fa-paper-plane';
        emptyMsg = 'No sent messages';
    } else if (path.includes('scheduled.html')) {
        folder = 'Scheduled';
        emptyIcon = 'fa-clock';
        emptyMsg = 'No scheduled messages';
    } else if (path.includes('drafts.html')) {
        folder = 'Drafts';
        emptyIcon = 'fa-file-lines';
        emptyMsg = 'No drafts saved';
    } else if (path.includes('starred.html')) {
        folder = 'Starred';
        emptyIcon = 'fa-star';
        emptyMsg = 'No starred messages';
    } else if (path.includes('spam.html')) {
        folder = 'Spam';
        emptyIcon = 'fa-ban';
        emptyMsg = 'No spam messages! Yay!';
    } else if (path.includes('trash.html')) {
        folder = 'Trash';
        emptyIcon = 'fa-trash';
        emptyMsg = 'Trash is empty';
    }
    
    if (folder) {
        loadEmails(folder, emptyIcon, emptyMsg);
    }
    
    // Bind search bar
    const searchInput = document.querySelector('.search-bar input');
    if (searchInput) {
        let debounceTimer;
        searchInput.addEventListener('input', (e) => {
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(() => {
                const query = e.target.value.trim();
                if (query.length > 0) {
                    performSearch(query);
                } else if (folder) {
                    loadEmails(folder, emptyIcon, emptyMsg);
                }
            }, 500);
        });
    }
});

async function performSearch(query) {
    const accountId = localStorage.getItem('accountId') || 1; 
    const listContainer = document.querySelector('.mail-list');
    if (!listContainer) return;
    
    document.querySelector('.list-header h2').textContent = `Search: ${query}`;
    listContainer.innerHTML = '<div style="padding: 20px; text-align: center; color: var(--text-muted);"><i class="fa-solid fa-spinner fa-spin"></i> Searching...</div>';
    
    const emails = await API.search(accountId, query);
    renderEmailList(emails, listContainer, 'fa-magnifying-glass', 'No results found', accountId);
}

function renderEmailList(emails, listContainer, emptyIcon, emptyMsg, accountId) {
    if (emails.length === 0) {
        listContainer.innerHTML = `
            <div style="display:flex; flex-direction:column; align-items:center; justify-content:center; height:100%; color: var(--text-muted); padding: 40px; text-align:center;">
                <i class="fa-solid ${emptyIcon}" style="font-size: 3rem; margin-bottom: 16px; opacity: 0.2;"></i>
                <div style="font-size: 1.1rem; font-weight: 500;">${emptyMsg}</div>
                <div style="font-size: .85rem; margin-top: 8px;">Try a different term...</div>
            </div>`;
            
        const bodyEl = document.querySelector('.mail-body');
        const subjectEl = document.querySelector('.view-subject');
        const viewMeta = document.querySelector('.view-meta');
        
        if (bodyEl) bodyEl.innerHTML = `<div style="padding: 60px; text-align: center; color: var(--text-muted);"><i class="fa-regular fa-envelope-open fa-3x" style="opacity:0.2; margin-bottom: 16px;"></i><br>Select an item to read</div>`;
        if (subjectEl) subjectEl.textContent = 'No Message Selected';
        if (viewMeta) viewMeta.style.display = 'none';
        
        return;
    }

    listContainer.innerHTML = '';
    emails.forEach((email, index) => {
        const item = document.createElement('div');
        item.className = `mail-item ${email.isRead ? '' : 'unread'}`;
        
        const d = new Date(email.date);
        const dateStr = d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
        
        item.innerHTML = `
            <div class="mail-header-info">
                <span class="sender">${escapeHtml(email.from)}</span>
                <span class="date">${dateStr}</span>
            </div>
            <div class="subject">${escapeHtml(email.subject)}</div>
            <div class="preview">${escapeHtml(email.subject)}...</div>
        `;
        
        item.addEventListener('click', async () => {
            document.querySelectorAll('.mail-item').forEach(m => m.classList.remove('active'));
            item.classList.add('active');
            item.classList.remove('unread');
            
            document.querySelector('.app-container')?.classList.add('viewing-mail');
            
            await renderEmailView(accountId, email);
        });
        
        listContainer.appendChild(item);
        
        if (index === 0 && window.innerWidth > 768) {
            item.click();
        }
    });
}

async function loadEmails(folder, emptyIcon, emptyMsg) {
    const accountId = localStorage.getItem('accountId') || 1; 
    const listContainer = document.querySelector('.mail-list');
    if (!listContainer) return;
    
    document.querySelector('.list-header h2').textContent = folder;

    listContainer.innerHTML = '<div style="padding: 20px; text-align: center; color: var(--text-muted);"><i class="fa-solid fa-spinner fa-spin"></i> Loading...</div>';

    const emails = await API.getEmails(accountId, folder, 0, 50);
    renderEmailList(emails, listContainer, emptyIcon, emptyMsg, accountId);
}

async function renderEmailView(accountId, email) {
    const subjectEl = document.querySelector('.view-subject');
    const senderNameEl = document.querySelector('.sender-details .name');
    const senderEmailEl = document.querySelector('.sender-details .email');
    const avatarEl = document.querySelector('.sender-profile .avatar');
    const bodyEl = document.querySelector('.mail-body');
    const viewMeta = document.querySelector('.view-meta');
    
    if (viewMeta) viewMeta.style.display = ''; // Restore flex display
    if (!subjectEl || !bodyEl) return;
    
    subjectEl.textContent = email.subject;
    
    // Extract name and email from "Name <email>" format if possible
    let name = email.from;
    let address = email.from;
    const match = email.from.match(/(.*)<(.*)>/);
    if (match) {
        name = match[1].trim();
        address = match[2].trim();
    }
    
    if (senderNameEl) senderNameEl.textContent = name;
    if (senderEmailEl) senderEmailEl.textContent = address;
    if (avatarEl) avatarEl.textContent = name.charAt(0).toUpperCase();
    
    bodyEl.innerHTML = '<div style="padding: 40px; text-align: center; color: var(--brand-action);"><i class="fa-solid fa-circle-notch fa-spin fa-2x"></i></div>';
    
    const htmlBody = await API.getEmailBody(accountId, email.folder, email.messageId);
    
    bodyEl.innerHTML = htmlBody || '<div style="padding: 20px; color: var(--text-muted);">This message has no content.</div>';
}

function escapeHtml(unsafe) {
    return (unsafe || '').replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#039;");
}

// ── SignalR Real-Time Push ──────────────────────────
if (window.signalR) {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/webmail")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveEmailNotification", (subject, from) => {
        // Log or show toast notification
        console.log("New Email Notification:", subject, "from", from);
        
        // Auto-refresh the list if we are currently looking at the INBOX
        const folderEl = document.querySelector('.list-header h2');
        if (folderEl && folderEl.textContent === 'INBOX') {
            loadEmails('INBOX', 'fa-inbox', 'Your inbox is empty');
        }
    });

    connection.start().then(() => {
        console.log("SignalR Connected for Real-Time Push!");
    }).catch(err => console.error("SignalR Connection Error: ", err));
}
