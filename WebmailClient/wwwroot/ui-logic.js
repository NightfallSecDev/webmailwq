// ============================================
// LETTERDUCK — GLOBAL UI LOGIC
// ============================================

// --- Auth Check ---
if (!window.location.pathname.endsWith('login.html') && !localStorage.getItem('accountId')) {
    window.location.replace('login.html');
}

// --- Theme Manager ---
const ThemeManager = (() => {
    const key = 'wm-theme';
    const saved = localStorage.getItem(key) || 'dark';
    document.documentElement.setAttribute('data-theme', saved);

    function setTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem(key, theme);
        document.querySelectorAll('.theme-toggle').forEach(btn => {
            btn.innerHTML = theme === 'dark'
                ? '<i class="fa-solid fa-sun"></i>'
                : '<i class="fa-solid fa-moon"></i>';
        });
    }

    document.querySelectorAll('.theme-toggle').forEach(btn => {
        // set correct initial icon
        btn.innerHTML = saved === 'dark'
            ? '<i class="fa-solid fa-sun"></i>'
            : '<i class="fa-solid fa-moon"></i>';
        btn.addEventListener('click', () => {
            const current = document.documentElement.getAttribute('data-theme');
            setTheme(current === 'dark' ? 'light' : 'dark');
        });
    });

    return { setTheme };
})();

// --- Modal Helper ---
function openModal(id)  { const m = document.getElementById(id); if (m) m.classList.add('open'); }
function closeModal(id) { const m = document.getElementById(id); if (m) m.classList.remove('open'); }

// Wire close-btn inside each modal
document.querySelectorAll('.modal-backdrop').forEach(modal => {
    // click on backdrop closes
    modal.addEventListener('click', e => { if (e.target === modal) modal.classList.remove('open'); });
    // close btn
    const closeBtn = modal.querySelector('[id^="close-"]');
    if (closeBtn) closeBtn.addEventListener('click', () => modal.classList.remove('open'));
});

// Escape key closes any open modal
document.addEventListener('keydown', e => {
    if (e.key === 'Escape') {
        document.querySelectorAll('.modal-backdrop.open').forEach(m => m.classList.remove('open'));
    }
});

// --- Global Toast Notification ---
window.showToast = function(message, type = 'success') {
    const banner = document.createElement('div');
    const color = type === 'success' ? '#10B981' : (type === 'error' ? '#EF4444' : 'var(--brand-action)');
    const icon = type === 'success' ? 'fa-circle-check' : (type === 'error' ? 'fa-triangle-exclamation' : 'fa-info-circle');
    banner.style.cssText = `position:fixed;top:16px;left:50%;transform:translateX(-50%);background:${color};color:#fff;padding:10px 22px;border-radius:999px;font-size:.84rem;font-weight:700;z-index:9999;box-shadow:0 4px 20px rgba(0,0,0,0.3);animation:fadeIn .2s ease`;
    banner.innerHTML = `<i class="fa-solid ${icon}"></i> ${message}`;
    document.body.appendChild(banner);
    setTimeout(() => { banner.style.opacity = '0'; banner.style.transition = 'opacity 0.2s'; setTimeout(() => banner.remove(), 200); }, 2500);
};

// --- Compose Button ---
const composeBtn = document.getElementById('compose-btn');
const composeModal = document.getElementById('compose-modal');
const closeCompose = document.getElementById('close-compose');
if (composeBtn && composeModal) {
    composeBtn.addEventListener('click', () => openModal('compose-modal'));
}
if (closeCompose) closeCompose.addEventListener('click', () => closeModal('compose-modal'));

// --- Active nav-item highlight (auto from href) ---
document.querySelectorAll('.nav-item').forEach(item => {
    if (item.href && window.location.pathname.endsWith(item.getAttribute('href'))) {
        item.classList.add('active');
    }
});

// --- Mail item active selection ---
document.querySelectorAll('.mail-item').forEach(item => {
    item.addEventListener('click', () => {
        document.querySelectorAll('.mail-item').forEach(i => i.classList.remove('active'));
        item.classList.add('active');
        // mark as read
        item.classList.remove('unread');
    });
});

// --- Tooltips (title attr) on toolbar buttons ---
document.querySelectorAll('[title]').forEach(el => {
    el.setAttribute('aria-label', el.getAttribute('title'));
});

// ============================================
// LIVE CLOCK
// ============================================
function updateClock() {
    const now = new Date();
    const timeEl = document.getElementById('sidebar-time');
    const dateEl = document.getElementById('sidebar-date');
    if (!timeEl) return;

    // Time — hh:mm:ss AM/PM
    timeEl.textContent = now.toLocaleTimeString('en-US', {
        hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: true
    });

    // Date — Saturday, May 24
    if (dateEl) {
        dateEl.textContent = now.toLocaleDateString('en-US', {
            weekday: 'long', month: 'short', day: 'numeric'
        });
    }
}
updateClock();
setInterval(updateClock, 1000);

// ============================================
// PROFILE DROPDOWN TOGGLE
// ============================================
const profileTrigger = document.getElementById('profile-trigger');
const profileDropdown = document.getElementById('profile-dropdown');

if (profileTrigger && profileDropdown) {
    profileTrigger.addEventListener('click', (e) => {
        e.stopPropagation();
        profileDropdown.classList.toggle('open');
    });
    document.addEventListener('click', () => {
        profileDropdown.classList.remove('open');
    });
}

// ============================================
// ACCOUNT SWITCHER
// ============================================
const AccountManager = (() => {
    const STORAGE_KEY = 'wm-accounts';
    const ACTIVE_KEY  = 'wm-active-account';

    const defaultAccounts = [
        { id: 1, name: 'User', email: 'user@localhost', initials: 'U', color: '#6366F1' }
    ];

    function getAccounts() {
        const stored = localStorage.getItem(STORAGE_KEY);
        return stored ? JSON.parse(stored) : defaultAccounts;
    }

    function saveAccounts(accs) {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(accs));
    }

    function getActiveId() {
        return parseInt(localStorage.getItem(ACTIVE_KEY) || '1');
    }

    function setActiveId(id) {
        localStorage.setItem(ACTIVE_KEY, id);
    }

    function getActive() {
        const accs = getAccounts();
        return accs.find(a => a.id === getActiveId()) || accs[0];
    }

    function updateSidebarProfile() {
        const acc = getActive();
        const nameEl  = document.querySelector('.profile-name');
        const emailEl = document.querySelector('.profile-email');
        const avatarEl = document.querySelector('.profile-avatar');
        if (nameEl)  nameEl.textContent  = acc.name;
        if (emailEl) {
            emailEl.textContent = acc.email;
            emailEl.title = "Click to copy email address";
            emailEl.style.cursor = "pointer";
            emailEl.onclick = (e) => {
                e.stopPropagation();
                navigator.clipboard.writeText(acc.email).then(() => {
                    const originalText = acc.email;
                    emailEl.innerHTML = '<i class="fa-solid fa-check"></i> Copied!';
                    emailEl.style.color = 'var(--brand-success)';
                    setTimeout(() => {
                        emailEl.textContent = originalText;
                        emailEl.style.color = '';
                    }, 1500);
                });
            };
        }
        if (avatarEl) {
            avatarEl.textContent = acc.initials;
            avatarEl.style.background = `linear-gradient(135deg, ${acc.color}, ${acc.color}99)`;
        }
    }

    function renderPanel() {
        const list = document.getElementById('account-list');
        if (!list) return;
        const accs = getAccounts();
        const activeId = getActiveId();
        list.innerHTML = accs.map(acc => `
            <div class="account-item ${acc.id === activeId ? 'active-account' : ''}"
                 data-id="${acc.id}" onclick="AccountManager.switchTo(${acc.id})">
                <div class="account-item-avatar" style="background:${acc.color}">${acc.initials}</div>
                <div class="account-item-info">
                    <div class="account-item-name">${acc.name}</div>
                    <div class="account-item-email">${acc.email}</div>
                </div>
                ${acc.id === activeId ? '<i class="fa-solid fa-circle-check account-item-check"></i>' : ''}
            </div>
        `).join('');
    }

    function switchTo(id) {
        setActiveId(id);
        updateSidebarProfile();
        renderPanel();
        closeAccountModal();
        // brief flash to confirm switch
        window.showToast('Account switched', 'info');
    }

    function addAccount() {
        closeAccountModal();
        // Show add account form modal
        const modal = document.getElementById('add-account-modal');
        if (modal) modal.classList.add('open');
    }

    function saveNewAccount() {
        const name    = document.getElementById('new-acc-name')?.value.trim();
        const email   = document.getElementById('new-acc-email')?.value.trim();
        const server  = document.getElementById('new-acc-server')?.value.trim();
        if (!name || !email) { alert('Name and email are required.'); return; }
        const accs = getAccounts();
        const colors = ['#6366F1','#10B981','#F59E0B','#EF4444','#EC4899','#14B8A6'];
        const newAcc = {
            id: Date.now(),
            name, email,
            initials: name.split(' ').map(w=>w[0]).join('').slice(0,2).toUpperCase(),
            color: colors[accs.length % colors.length]
        };
        accs.push(newAcc);
        saveAccounts(accs);
        setActiveId(newAcc.id);
        updateSidebarProfile();
        const modal = document.getElementById('add-account-modal');
        if (modal) modal.classList.remove('open');
        // reset
        ['new-acc-name','new-acc-email','new-acc-server'].forEach(id => {
            const el = document.getElementById(id); if(el) el.value='';
        });
    }

    function openAccountModal() {
        const m = document.getElementById('account-switcher-modal');
        if (m) { renderPanel(); m.classList.add('open'); }
    }
    function closeAccountModal() {
        const m = document.getElementById('account-switcher-modal');
        if (m) m.classList.remove('open');
    }

    // Init
    updateSidebarProfile();

    // Wire "Switch Account" item in dropdown
    document.addEventListener('click', e => {
        if (e.target.closest('#switch-account-btn')) {
            e.stopPropagation();
            document.getElementById('profile-dropdown')?.classList.remove('open');
            openAccountModal();
        }
        if (e.target.closest('#add-account-btn')) {
            e.stopPropagation();
            document.getElementById('profile-dropdown')?.classList.remove('open');
            addAccount();
        }
    });

    // backdrop close
    const switcher = document.getElementById('account-switcher-modal');
    if (switcher) {
        const bd = switcher.querySelector('.account-modal-backdrop');
        if (bd) bd.addEventListener('click', closeAccountModal);
    }

    return { switchTo, addAccount, saveNewAccount, openAccountModal, closeAccountModal };
})();

// ============================================
// SIDEBAR COLLAPSE TOGGLE
// ============================================
const SidebarManager = (() => {
    const key = 'wm-sidebar-collapsed';
    const saved = localStorage.getItem(key) === 'true';
    const sidebar = document.querySelector('.sidebar');
    
    if (sidebar && saved) {
        sidebar.classList.add('collapsed');
    }

    function toggle() {
        if (!sidebar) return;
        const isCollapsed = sidebar.classList.toggle('collapsed');
        localStorage.setItem(key, isCollapsed);
    }

    // Attach to any element with id="sidebar-toggle"
    document.addEventListener('click', e => {
        if (e.target.closest('#sidebar-toggle')) {
            toggle();
        }
    });

    return { toggle };
})();

// ============================================
// MOBILE RESPONSIVE LOGIC
// ============================================
const MobileManager = (() => {
    function init() {
        // 1. Inject Backdrop
        if (!document.querySelector('.mobile-sidebar-backdrop')) {
            const backdrop = document.createElement('div');
            backdrop.className = 'mobile-sidebar-backdrop';
            
            // Append to .app-container to stay in the same stacking context as .sidebar
            const container = document.querySelector('.app-container') || document.body;
            container.appendChild(backdrop);
            
            backdrop.addEventListener('click', () => {
                document.querySelector('.sidebar')?.classList.remove('mobile-open');
                backdrop.classList.remove('show');
            });
        }

        // 2. Inject Hamburger Menu to Mail List Header & Settings Header
        const listHeaderH2 = document.querySelector('.list-header h2');
        if (listHeaderH2 && !document.querySelector('.mobile-menu-btn')) {
            const btn = document.createElement('button');
            btn.className = 'mobile-menu-btn';
            btn.innerHTML = '<i class="fa-solid fa-bars"></i>';
            btn.onclick = openSidebar;
            listHeaderH2.prepend(btn);
        }

        const settingsH1 = document.querySelector('.settings-header-top h1');
        if (settingsH1 && !document.querySelector('.settings-menu-btn')) {
            const btn = document.createElement('button');
            btn.className = 'mobile-menu-btn settings-menu-btn';
            btn.innerHTML = '<i class="fa-solid fa-bars"></i>';
            btn.onclick = openSidebar;
            settingsH1.prepend(btn);
        }

        // 3. Inject Mobile Back Button to Mail View Header
        const viewHeaderH1 = document.querySelector('.view-header-top .view-subject');
        if (viewHeaderH1 && !document.querySelector('.mobile-back-btn')) {
            const btn = document.createElement('button');
            btn.className = 'mobile-back-btn';
            btn.innerHTML = '<i class="fa-solid fa-arrow-left"></i>';
            btn.onclick = closeMailView;
            viewHeaderH1.parentElement.prepend(btn);
            
            // Adjust title to not overlap
            viewHeaderH1.style.flex = '1';
        }

        // 4. Wire up Mail Item clicks for mobile
        document.querySelectorAll('.mail-item').forEach(item => {
            item.addEventListener('click', openMailView);
        });

        // Add auto-close sidebar on nav item click (mobile only)
        document.querySelectorAll('.nav-item').forEach(item => {
            item.addEventListener('click', () => {
                if (window.innerWidth <= 768) {
                    document.querySelector('.sidebar')?.classList.remove('mobile-open');
                    document.querySelector('.mobile-sidebar-backdrop')?.classList.remove('show');
                }
            });
        });
    }

    function openSidebar() {
        document.querySelector('.sidebar')?.classList.add('mobile-open');
        document.querySelector('.mobile-sidebar-backdrop')?.classList.add('show');
    }

    function openMailView() {
        if (window.innerWidth <= 768) {
            document.querySelector('.app-container')?.classList.add('viewing-mail');
        }
    }

    function closeMailView() {
        document.querySelector('.app-container')?.classList.remove('viewing-mail');
    }

    // Run on load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    return { openSidebar, openMailView, closeMailView };
})();
