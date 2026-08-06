// Service worker mínimo — só existe para tornar o site instalável como app (PWA).
// Sem cache agressivo: cada navegação busca a rede primeiro, sem guardar nada offline.
self.addEventListener('install', function (event) {
  self.skipWaiting();
});

self.addEventListener('activate', function (event) {
  event.waitUntil(self.clients.claim());
});

self.addEventListener('fetch', function (event) {
  event.respondWith(fetch(event.request));
});
