// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development harder (changes wouldn't be reflected
// on the first load after each change). See service-worker.published.js for production.
self.addEventListener('fetch', () => { });
