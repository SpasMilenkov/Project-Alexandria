/// <reference lib="webworker" />

declare const self: ServiceWorkerGlobalScope;

const CACHE_NAME = "bg-image-v1";
const INTERCEPT_PATH = "/sw/background-image";

self.addEventListener("fetch", (event: FetchEvent) => {
  const url = new URL(event.request.url);
  if (url.pathname !== INTERCEPT_PATH) return;
  event.respondWith(serveFromCache(url));
});

async function serveFromCache(url: URL): Promise<Response> {
  const timestamp = url.searchParams.get("t");
  if (!timestamp) return new Response("Missing timestamp", { status: 400 });

  const cache = await caches.open(CACHE_NAME);
  const cached = await cache.match(buildCacheKey(timestamp));
  if (cached) return cached;

  return new Response("Not cached", { status: 404 });
}

self.addEventListener("message", (event: ExtendableMessageEvent) => {
  const { type } = event.data ?? {};
  if (type === "CHECK_CACHED") handleCheck(event);
  if (type === "FETCH_AND_CACHE") handleFetchAndCache(event);
  if (type === "EVICT") handleEvict(event);
});

async function handleCheck(event: ExtendableMessageEvent) {
  const { timestamp } = event.data;
  const cache = await caches.open(CACHE_NAME);
  const hit = await cache.match(buildCacheKey(timestamp));
  event.ports[0].postMessage({ cached: !!hit });
}

async function handleFetchAndCache(event: ExtendableMessageEvent) {
  const { presignedUrl, timestamp } = event.data;
  try {
    const cache = await caches.open(CACHE_NAME);
    const keys = await cache.keys();
    await Promise.all(keys.map((k) => cache.delete(k)));

    const response = await fetch(presignedUrl);
    if (!response.ok) throw new Error(`S3 fetch failed: ${response.status}`);

    await cache.put(buildCacheKey(timestamp), response.clone());
    event.ports[0].postMessage({ success: true });
  } catch (err) {
    event.ports[0].postMessage({
      success: false,
      error: (err as Error).message,
    });
  }
}

async function handleEvict(event: ExtendableMessageEvent) {
  const cache = await caches.open(CACHE_NAME);
  const keys = await cache.keys();
  await Promise.all(keys.map((k) => cache.delete(k)));
  event.ports[0]?.postMessage({ evicted: true });
}

function buildCacheKey(timestamp: string): string {
  return `${INTERCEPT_PATH}?t=${timestamp}`;
}
