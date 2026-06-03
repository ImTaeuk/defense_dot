// Dot Defense — 서비스워커
// 업데이트 시: CACHE 이름의 v숫자만 올리면 자동으로 새 캐시 생성 + 기존 캐시 폐기.
// HTML/메인페이지는 network-first 전략 → 인터넷 연결되어 있으면 항상 최신본 반영.
const CACHE = 'dot-defense-v169';
const ASSETS = [
  './',
  './index.html',
  './manifest.json',
  './icons/icon-192.png',
  './icons/icon-512.png',
  './icons/icon-512-maskable.png',
];

self.addEventListener('install', e => {
  e.waitUntil(
    caches.open(CACHE).then(cache => cache.addAll(ASSETS).catch(() => {}))
  );
  self.skipWaiting();   // 새 SW가 즉시 활성화 대기 안 함
});

self.addEventListener('activate', e => {
  e.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k)))
    )
  );
  self.clients.claim(); // 현재 열려있는 페이지 즉시 새 SW가 제어
});

self.addEventListener('fetch', e => {
  if (e.request.method !== 'GET') return;
  const url = new URL(e.request.url);
  // 메인 페이지(HTML)는 network-first — 항상 새 코드 받기
  const isHTML =
    e.request.mode === 'navigate' ||
    url.pathname.endsWith('.html') ||
    url.pathname.endsWith('/');
  if (isHTML) {
    e.respondWith(
      fetch(e.request).then(net => {
        const copy = net.clone();
        caches.open(CACHE).then(c => c.put(e.request, copy)).catch(() => {});
        return net;
      }).catch(() =>
        caches.match(e.request).then(r => r || caches.match('./index.html'))
      )
    );
  } else {
    // 정적 에셋 (아이콘 등)은 cache-first — 빠른 로드
    e.respondWith(
      caches.match(e.request).then(res =>
        res || fetch(e.request).then(net => {
          const copy = net.clone();
          caches.open(CACHE).then(c => c.put(e.request, copy)).catch(() => {});
          return net;
        })
      )
    );
  }
});
