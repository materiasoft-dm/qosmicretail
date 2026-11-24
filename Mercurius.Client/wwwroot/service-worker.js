// Mercurius service worker: lets the installed app open and keep working without a connection.
//
// The app is server-rendered first (each page's HTML comes from the server, then WebAssembly takes
// over), so there is no static index.html to fall back to. Instead:
//   - Pages (navigations): network first; offline, the last copy of that page this browser saw, else
//     any saved page (WebAssembly routes by the URL, so any of them boots the right screen), else
//     offline.html. Sign-in pages are never saved.
//   - App files (_framework, _content, css, js, images, fonts): cache first. They are fingerprinted
//     (a new build means a new file name), and the cache is per build, so nothing stale is served.
//   - API reads (GET /api/...): network first; offline, the last response for that exact URL, marked
//     with an X-Mercurius-Offline header (when it was saved) so the app can say it's showing saved
//     data. Admin, security and download endpoints are never saved.
//   - Changes (POST/PUT/DELETE) pass straight through: the app itself queues them when offline (see
//     Services/SyncService.cs). Signing out clears the saved pages and data (never unsent changes,
//     which live in IndexedDB), so the next person on a shared PC doesn't see them.
//
// The ?v= on the registration (App.razor) is the build: a new build installs a new worker, which
// starts a new app-file cache and drops the old one.
const VERSION = new URL(self.location).searchParams.get('v') || 'dev';
const SHELL = `mercurius-shell-${VERSION}`;
const PAGES = 'mercurius-pages';
const DATA = 'mercurius-data';
const OFFLINE_URL = '/offline.html';
const PAGE_TIMEOUT_MS = 8000;

const STATIC_PREFIXES = ['/_framework/', '/_content/', '/css/', '/js/', '/images/', '/ProductImages/'];
const STATIC_EXTENSIONS = ['.styles.css', '.png', '.ico', '.svg', '.webmanifest', '.woff', '.woff2'];
const UNSAVED_API_PREFIXES = ['/api/admin/', '/api/platform/', '/api/logs', '/api/audit-trail', '/api/downloads',
    '/api/products/import', '/api/auth/', '/api/sync/', '/api/roles', '/api/users'];

self.addEventListener('install', event => {
    event.waitUntil(caches.open(SHELL).then(cache => cache.addAll([OFFLINE_URL, '/icon-192.png', '/favicon.png'])));
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        const keys = await caches.keys();
        await Promise.all(keys.filter(k => k.startsWith('mercurius-shell-') && k !== SHELL).map(k => caches.delete(k)));
        await self.clients.claim();
    })());
});

self.addEventListener('message', event => {
    const message = event.data || {};
    if (message.type === 'warm') event.waitUntil(warm(message.pages || [], message.assets || []));
    if (message.type === 'clear-user-data') event.waitUntil(clearUserData());
});

self.addEventListener('fetch', event => {
    const request = event.request;
    const url = new URL(request.url);
    const sameOrigin = url.origin === self.location.origin;

    if (request.method !== 'GET') {
        if (sameOrigin && url.pathname.toLowerCase() === '/account/logout') event.waitUntil(clearUserData());
        return;
    }
    if (!sameOrigin) {
        // Web fonts and the icon font (Google Fonts, jsDelivr).
        if (/fonts\.(googleapis|gstatic)\.com$|cdn\.jsdelivr\.net$/.test(url.hostname)) event.respondWith(cacheFirst(request));
        return;
    }
    const path = url.pathname.toLowerCase();
    // File downloads (app installers, backups, logs) are left entirely to the browser.
    if (path.startsWith('/downloads/')) return;
    if (path.startsWith('/api/')) {
        if (request.mode !== 'navigate' && !UNSAVED_API_PREFIXES.some(p => path.startsWith(p))) event.respondWith(data(request));
        return;
    }
    if (request.mode === 'navigate') {
        event.respondWith(page(request, url));
        return;
    }
    if (isStatic(url)) event.respondWith(cacheFirst(request));
});

function isStatic(url) {
    if (url.pathname === '/service-worker.js') return false;
    return url.pathname === OFFLINE_URL
        || STATIC_PREFIXES.some(p => url.pathname.startsWith(p))
        || STATIC_EXTENSIONS.some(e => url.pathname.endsWith(e));
}

async function cacheFirst(request) {
    const cache = await caches.open(SHELL);
    const hit = await cache.match(request, { ignoreVary: true });
    if (hit) return hit;
    const response = await fetch(request);
    if (response.ok || response.type === 'opaque') cache.put(request, response.clone());
    return response;
}

function savePageKey(url) {
    return url.pathname.toLowerCase().startsWith('/account/') ? null : url.pathname;
}

async function page(request, url) {
    try {
        const response = await withTimeout(fetch(request), PAGE_TIMEOUT_MS);
        const key = savePageKey(url);
        const isHtml = (response.headers.get('content-type') || '').includes('text/html');
        if (key && isHtml && response.ok && response.type === 'basic' && !response.redirected) {
            (await caches.open(PAGES)).put(key, response.clone());
        }
        return response;
    } catch {
        const pages = await caches.open(PAGES);
        const saved = await pages.match(url.pathname) || await pages.match('/') || await anySaved(pages);
        return saved || (await caches.match(OFFLINE_URL)) || Response.error();
    }
}

async function anySaved(cache) {
    const keys = await cache.keys();
    return keys.length ? cache.match(keys[keys.length - 1]) : undefined;
}

async function data(request) {
    // A caller that has its own offline data (the till's product search reads the device's catalog)
    // asks not to be given a saved response.
    const wantsFallback = request.headers.get('X-Mercurius-Offline-Fallback') !== 'none';
    try {
        const response = await fetch(request);
        const type = response.headers.get('content-type') || '';
        if (response.ok && type.includes('json')) {
            const saved = new Response(await response.clone().blob(), { status: 200, headers: response.headers });
            saved.headers.set('X-Mercurius-Saved-At', new Date().toISOString());
            (await caches.open(DATA)).put(request.url, saved);
        }
        return response;
    } catch (error) {
        if (!wantsFallback) throw error;
        const hit = await (await caches.open(DATA)).match(request.url);
        if (!hit) throw error;
        const headers = new Headers(hit.headers);
        headers.set('X-Mercurius-Offline', hit.headers.get('X-Mercurius-Saved-At') || '');
        return new Response(await hit.blob(), { status: 200, headers });
    }
}

// Called by the app once it's running: save the pages a cashier may need to open offline, and every
// app file this page loaded (the first visit happens before this worker controls the page, so those
// loads weren't seen).
async function warm(pages, assets) {
    const shell = await caches.open(SHELL);
    for (const asset of assets) {
        try {
            const url = new URL(asset, self.location.origin);
            if (url.origin !== self.location.origin || !isStatic(url)) continue;
            if (!(await shell.match(url.href, { ignoreVary: true }))) {
                const response = await fetch(url.href);
                if (response.ok) await shell.put(url.href, response);
            }
        } catch { /* best effort */ }
    }
    const saved = await caches.open(PAGES);
    for (const path of pages) {
        try {
            const response = await fetch(path, { credentials: 'same-origin' });
            const isHtml = (response.headers.get('content-type') || '').includes('text/html');
            if (response.ok && isHtml && !response.redirected) await saved.put(path, response);
        } catch { /* offline: keep what's there */ }
    }
}

async function clearUserData() {
    await caches.delete(PAGES);
    await caches.delete(DATA);
}

function withTimeout(promise, ms) {
    return new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error('timeout')), ms);
        promise.then(r => { clearTimeout(timer); resolve(r); }, e => { clearTimeout(timer); reject(e); });
    });
}
