// Cross-origin isolation for hosts that cannot send the headers themselves (static hosting, the .NET dev server).
// CP-SAT runs on WebAssembly threads, which need SharedArrayBuffer, which needs the page to be cross-origin isolated:
//   Cross-Origin-Opener-Policy: same-origin
//   Cross-Origin-Embedder-Policy: require-corp
// This service worker adds both to every same-origin response. index.html registers it only when the page is not
// isolated already, and reloads once so the page is served through it. Sending the headers from the server is better.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));

self.addEventListener('fetch', (event) => {
  const request = event.request;
  if (request.cache === 'only-if-cached' && request.mode !== 'same-origin') return;
  if (new URL(request.url).origin !== self.location.origin) return;
  event.respondWith(
    fetch(request).then((response) => {
      if (response.status === 0) return response;
      const headers = new Headers(response.headers);
      headers.set('Cross-Origin-Opener-Policy', 'same-origin');
      headers.set('Cross-Origin-Embedder-Policy', 'require-corp');
      return new Response(response.body, { status: response.status, statusText: response.statusText, headers });
    }),
  );
});
