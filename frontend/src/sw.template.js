// Service Worker for EPL Predictions PWA.
//
// Built, not copied: __BUILD_ID__ is replaced at build time by the plugin in vite.config.ts.
// That matters twice over. The cache names change every deploy, so an old build's caches are
// dropped rather than kept forever; and because the file's bytes differ, the browser actually
// notices there is a new worker. A static sw.js is byte-identical on every deploy, so `install`
// only ever ran on a user's very first visit and the caches written that day were frozen there.
const BUILD_ID = '__BUILD_ID__';
const CACHE_NAME = `epl-predictions-${BUILD_ID}`;
const RUNTIME_CACHE = `epl-predictions-runtime-${BUILD_ID}`;

// The shell, and only things that certainly exist. This list used to name /pl-logo.png and
// /pl-logo-alt.png, neither of which is in public/. cache.addAll rejects wholesale if any one
// request fails, which would have aborted the install and disabled the worker entirely — it
// survived only because vercel.json rewrites everything to /index.html, so the misses came back
// as 200 text/html and got stored under two image keys.
const PRECACHE_ASSETS = ['/', '/index.html', '/manifest.json'];

// Hashed build output. The filename changes whenever the contents do, so these are the only
// things safe to serve from cache without asking the network first.
const IMMUTABLE_PATH = '/assets/';

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(CACHE_NAME)
      .then((cache) => cache.addAll(PRECACHE_ASSETS))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((names) =>
        Promise.all(
          names
            .filter((name) => name !== CACHE_NAME && name !== RUNTIME_CACHE)
            .map((name) => caches.delete(name))
        )
      )
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const { request } = event;
  const url = new URL(request.url);

  if (request.method !== 'GET') {
    return;
  }

  // Navigations: network first, always.
  //
  // index.html carries the <script src="/assets/index-[hash].js"> for the build it was published
  // with, so it is the one file that must never be served stale — a cached copy points at an
  // older build's assets, which are then served from cache too, and the whole app is pinned to
  // whatever version the user first loaded. This used to answer from the cache unconditionally.
  //
  // The cached copy is kept only as an offline fallback. Losing the offline shell is the price,
  // and it is worth paying: a live-scores app has nothing to show without a network anyway.
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request)
        .then((response) => {
          if (response && response.status === 200) {
            const copy = response.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put('/index.html', copy));
          }
          return response;
        })
        .catch(() =>
          caches.match('/index.html').then(
            (cached) =>
              cached ||
              new Response('<h1>Offline</h1>', {
                status: 503,
                headers: { 'Content-Type': 'text/html' },
              })
          )
        )
    );
    return;
  }

  // API calls and the SignalR hub are never touched. Returning without calling respondWith
  // hands the request back to the browser untouched.
  //
  // These responses are per-player and behind a login: standings, another player's picks, the
  // admin user list. Writing them to Cache Storage put them somewhere that outlives the
  // session, survives logout, and is readable by any script on the origin — and the old handler
  // then served them from that cache whenever the network failed, so a second user on a shared
  // device could be shown the first one's data. It cached whatever came back, 401s included.
  //
  // Nothing is lost by dropping it. React Query already caches reads in memory for the life of
  // the session, which is the right lifetime for data that belongs to one logged-in player.
  if (
    url.origin.includes('api.eplpredict.com') ||
    url.pathname.startsWith('/api/') ||
    url.pathname.startsWith('/hubs/')
  ) {
    return;
  }

  // Anything on another origin is somebody else's to cache.
  if (url.origin !== self.location.origin) {
    return;
  }

  // Hashed build assets - cache first. A new build means new filenames, so a hit here can never
  // be stale, and the old names are dropped with the old cache on activate.
  if (url.pathname.startsWith(IMMUTABLE_PATH)) {
    event.respondWith(
      caches.match(request).then((cached) => {
        if (cached) {
          return cached;
        }
        return fetch(request).then((response) => {
          if (!response || response.status !== 200 || response.type === 'error') {
            return response;
          }
          const responseClone = response.clone();
          caches.open(RUNTIME_CACHE).then((cache) => cache.put(request, responseClone));
          return response;
        });
      })
    );
    return;
  }

  // Everything left is same-origin, public and unauthenticated - icons, the manifest, splash
  // images. Revalidate, but stay usable offline. These keep their names between builds, so
  // cache-first would serve a replaced image indefinitely.
  event.respondWith(
    fetch(request)
      .then((response) => {
        if (response && response.status === 200 && response.type !== 'error') {
          const responseClone = response.clone();
          caches.open(RUNTIME_CACHE).then((cache) => cache.put(request, responseClone));
        }
        return response;
      })
      .catch(() => caches.match(request))
  );
});

// Background sync for offline picks (if supported)
self.addEventListener('sync', (event) => {
  if (event.tag === 'sync-picks') {
    event.waitUntil(syncPicks());
  }
});

async function syncPicks() {
  // This would sync any picks made while offline
  // Implementation depends on your IndexedDB or localStorage strategy
  console.log('Syncing offline picks...');
}

// Push notifications (future feature)
self.addEventListener('push', (event) => {
  const data = event.data ? event.data.json() : {};
  const title = data.title || 'EPL Predictions';
  const options = {
    body: data.body || 'New update available',
    icon: '/pwa-icon-192.png',
    badge: '/pwa-icon-192.png',
    vibrate: [200, 100, 200],
    data: data.url || '/',
    actions: [
      { action: 'open', title: 'Open App' },
      { action: 'close', title: 'Close' },
    ],
  };

  event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();

  if (event.action === 'open' || !event.action) {
    event.waitUntil(clients.openWindow(event.notification.data || '/'));
  }
});
