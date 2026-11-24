// Backs Mercurius.Client/Services/PwaService.cs and SyncService.cs. Things that only exist in JS:
//   1. Requesting persistent (non-evictable) storage — without this, browsers can silently clear
//      IndexedDB, including unsent offline changes, after a period of inactivity. Not a guarantee,
//      but it's the only lever available (an installed app usually gets it automatically).
//   2. Capturing the browser's install prompt so a button can trigger it — the prompt is only
//      available on demand if we intercept and stash the event beforehand.
//   3. The browser's online/offline signal, passed to SyncService so it syncs the moment the
//      connection is back.
//   4. Asking the service worker to save what this device needs to open offline.
window.mercuriusPwa = {
    deferredInstallPrompt: null,

    async requestPersistentStorage() {
        if (!(navigator.storage && navigator.storage.persist)) return false;
        try {
            const already = await navigator.storage.persisted();
            if (already) return true;
            return await navigator.storage.persist();
        } catch {
            return false;
        }
    },

    isInstalled() {
        return window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
    },

    canPromptInstall() {
        return !!window.mercuriusPwa.deferredInstallPrompt;
    },

    async promptInstall() {
        const promptEvent = window.mercuriusPwa.deferredInstallPrompt;
        if (!promptEvent) return 'unavailable';
        promptEvent.prompt();
        const choice = await promptEvent.userChoice;
        window.mercuriusPwa.deferredInstallPrompt = null;
        return choice.outcome; // 'accepted' | 'dismissed'
    },

    isOnline() {
        return navigator.onLine;
    },

    watchConnectivity(dotNetRef) {
        window.addEventListener('online', () => dotNetRef.invokeMethodAsync('OnBrowserConnectivityChanged', true));
        window.addEventListener('offline', () => dotNetRef.invokeMethodAsync('OnBrowserConnectivityChanged', false));
    },

    // Saves the given pages and every app file this page has loaded, so the app can open offline.
    async warmOfflineCache(pages) {
        if (!('serviceWorker' in navigator)) return false;
        try {
            const registration = await navigator.serviceWorker.ready;
            const worker = navigator.serviceWorker.controller || registration.active;
            if (!worker) return false;
            const assets = performance.getEntriesByType('resource')
                .map(entry => entry.name)
                .filter(name => name.startsWith(location.origin) || /fonts\.(googleapis|gstatic)\.com|cdn\.jsdelivr\.net/.test(name));
            worker.postMessage({ type: 'warm', pages, assets });
            return true;
        } catch {
            return false;
        }
    }
};

window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    window.mercuriusPwa.deferredInstallPrompt = e;
});
