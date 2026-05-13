// Phase I follow-up — kill switch.
//
// The PWA service worker introduced in Phase E was caching JS/CSS
// aggressively and pinned users to a buggy `offline-indicator.js`
// that fired the offline banner against a healthy connection. Even
// after I switched the cache strategy to network-first, browsers
// already controlled by the old SW never updated cleanly.
//
// Easiest fix: replace this file with a self-uninstalling stub.
// On the next page load, the browser fetches this SW, sees it's
// different from the old one, installs it, activates it, and the
// activate handler:
//   1. clears every cache the previous SW created;
//   2. unregisters this SW from the page so future visits run
//      with no SW at all (no caching, no install prompt).
//
// We can re-introduce a properly-scoped SW later if + when we
// genuinely need offline assets. For now the install prompt is
// not worth the cache-invalidation pain.
self.addEventListener('install', (event) => {
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys.map((k) => caches.delete(k)));
    await self.registration.unregister();
    const clients = await self.clients.matchAll({ type: 'window' });
    for (const c of clients) {
      // Ask each open tab to reload itself so the un-controlled
      // page picks up fresh assets straight from the network.
      c.navigate(c.url);
    }
  })());
});

self.addEventListener('fetch', (event) => {
  // Pass through everything to the network — never cache, never
  // intercept. Belt-and-braces in case activate hasn't completed
  // before a request fires.
  event.respondWith(fetch(event.request));
});
