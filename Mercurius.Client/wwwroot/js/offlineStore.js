// This device's offline data, backing Mercurius.Client/Services/OfflineStoreService.cs. IndexedDB:
//   products      — the last-synced catalog snapshot, replaced wholesale on every pull, searched by
//                   the till while offline (and decremented locally as offline sales are made).
//   outbox        — changes made on this device that the server hasn't confirmed yet: every kind
//                   (sales now; refunds, adjustments, receiving, edits as they become offline-capable).
//                   Sent in order by Services/SyncService.cs and removed once the server has them.
//                   Never cleared by signing out — that would lose sales.
//   pendingSales  — the queue before the outbox existed (version 1). Moved into the outbox on start.
const DB_NAME = 'mercurius-offline';
const DB_VERSION = 2;

function openDb() {
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = () => {
            const db = req.result;
            if (!db.objectStoreNames.contains('products')) {
                db.createObjectStore('products', { keyPath: 'id' });
            }
            if (!db.objectStoreNames.contains('pendingSales')) {
                db.createObjectStore('pendingSales', { keyPath: 'syncId' });
            }
            if (!db.objectStoreNames.contains('outbox')) {
                db.createObjectStore('outbox', { keyPath: 'id' });
            }
        };
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });
}

async function withStore(storeName, mode, fn) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const tx = db.transaction(storeName, mode);
        const store = tx.objectStore(storeName);
        const result = fn(store);
        tx.oncomplete = () => resolve(result);
        tx.onerror = () => reject(tx.error);
    });
}

async function getAll(storeName) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const req = db.transaction(storeName, 'readonly').objectStore(storeName).getAll();
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });
}

window.mercuriusOfflineStore = {
    async replaceProducts(products) {
        const db = await openDb();
        await new Promise((resolve, reject) => {
            const tx = db.transaction('products', 'readwrite');
            const store = tx.objectStore('products');
            store.clear();
            for (const p of products) store.put(p);
            tx.oncomplete = resolve;
            tx.onerror = () => reject(tx.error);
        });
    },

    getAllProducts() {
        return getAll('products');
    },

    async decrementProductStock(productId, quantity) {
        return withStore('products', 'readwrite', (store) => {
            const getReq = store.get(productId);
            getReq.onsuccess = () => {
                const product = getReq.result;
                if (product) {
                    product.stock -= quantity;
                    store.put(product);
                }
            };
        });
    },

    // ---- outbox
    putOutboxEntry(entry) {
        return withStore('outbox', 'readwrite', (store) => store.put(entry));
    },

    async getOutbox() {
        const entries = await getAll('outbox');
        return entries.sort((a, b) => (a.createdAtUtc < b.createdAtUtc ? -1 : a.createdAtUtc > b.createdAtUtc ? 1 : 0));
    },

    removeOutboxEntry(id) {
        return withStore('outbox', 'readwrite', (store) => store.delete(id));
    },

    // ---- version 1 queue, read once to move it into the outbox
    getPendingSales() {
        return getAll('pendingSales');
    },

    removePendingSale(syncId) {
        return withStore('pendingSales', 'readwrite', (store) => store.delete(syncId));
    }
};
