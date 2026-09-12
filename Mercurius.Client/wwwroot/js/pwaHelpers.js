// Backs Mercurius.Client/Services/PwaService.cs. Two concerns:
//   1. Requesting persistent (non-evictable) storage — without this, browsers (Safari on iOS
//      especially) can silently clear IndexedDB, including any unsynced offline sales, after a
//      period of inactivity. Not a guarantee, but it's the only lever available.
//   2. Capturing the browser's install prompt so a UI button can trigger it — the prompt is only
//      available on demand if we intercept and stash the event beforehand.
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
    }
};

window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    window.mercuriusPwa.deferredInstallPrompt = e;
});
