// Dot Defense — 서비스워커
// 업데이트 시: CACHE 이름의 v숫자만 올리면 자동으로 새 캐시 생성 + 기존 캐시 폐기.
// HTML/메인페이지는 network-first 전략 → 인터넷 연결되어 있으면 항상 최신본 반영.
const CACHE = 'dot-defense-v480';
const ASSETS = [
  './',
  './index.html',
  './manifest.json',
  './icons/icon-192.png',
  './icons/icon-512.png',
  './icons/icon-512-maskable.png',
];
// 🚀 큰 가챠 배너 이미지는 별도 배열 — SW 설치 후 백그라운드 캐싱 (블로킹 X)
//    설치 자체는 위 ASSETS만 기다리므로 SW 활성화 지연 안 됨.
//    이거 캐싱 끝나면 다음 로드부터 즉시 표시.
const DEFERRED_ASSETS = [
  './icons/gacha-elemSumi.png',
  './icons/gacha-ariaMisa.png',
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
    ).then(() => {
      // 🚀 큰 배너 이미지는 활성화 후 백그라운드에서 caching (SW 활성화 지연 X)
      caches.open(CACHE).then(cache => {
        // 이미 캐시된 건 skip, 없는 것만 다운로드
        DEFERRED_ASSETS.forEach(url => {
          cache.match(url).then(hit => {
            if (!hit) cache.add(url).catch(() => {});
          });
        });
      });
    })
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
