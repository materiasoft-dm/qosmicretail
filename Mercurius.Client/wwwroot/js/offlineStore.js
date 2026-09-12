// Local product cache + pending-sale queue for offline selling, backing
// Mercurius.Client/Services/OfflineStoreService.cs. Two IndexedDB object stores:
//   products      — the last-synced catalog snapshot, replaced wholesale on every pull.
//   pendingSales  — sales rung up while offline, removed once successfully pushed to the server.
const DB_NAME = 'mercurius-offline';
const DB_VERSION = 1;

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

window.mercuriusOfflineStore = {
    async replaceProducts(products) {
        const db = await openDb();
        await new Promise((resolve, reject) => {
            const tx = db.transaction('products', 'readwrite');
            tx.objectStore('products').clear();
            const store = tx.objectStore('products');
            for (const p of products) store.put(p);
            tx.oncomplete = resolve;
            tx.onerror = () => reject(tx.error);
        });
    },

    async getAllProducts() {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('products', 'readonly');
            const req = tx.objectStore('products').getAll();
            req.onsuccess = () => resolve(req.result);
            req.onerror = () => reject(req.error);
        });
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

    async queueSale(sale) {
        return withStore('pendingSales', 'readwrite', (store) => store.put(sale));
    },

    async getPendingSales() {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx = db.transaction('pendingSales', 'readonly');
            const req = tx.objectStore('pendingSales').getAll();
            req.onsuccess = () => resolve(req.result);
            req.onerror = () => reject(req.error);
        });
    },

    async removePendingSale(syncId) {
        return withStore('pendingSales', 'readwrite', (store) => store.delete(syncId));
    }
};
