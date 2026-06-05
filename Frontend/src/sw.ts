/// <reference lib="webworker" />
import {
  CacheFirst,
  ExpirationPlugin,
  NetworkFirst,
  NetworkOnly,
  type PrecacheEntry,
  Serwist,
  type SerwistGlobalConfig,
} from "serwist";

declare const self: ServiceWorkerGlobalScope;

declare global {
  interface WorkerGlobalScope extends SerwistGlobalConfig {
    __SW_MANIFEST: (PrecacheEntry | string)[] | undefined;
  }
}
const CACHE_NAME = "bg-image-v1";
const INTERCEPT_PATH = "/sw/background-image";

self.addEventListener("fetch", (event: FetchEvent) => {
  const url = new URL(event.request.url);
  if (url.pathname !== INTERCEPT_PATH) {
    return;
  }
  event.respondWith(serveFromCache(url));
});

const serveFromCache = async (url: URL): Promise<Response> => {
  const timestamp = url.searchParams.get("t");
  if (!timestamp) {
    return new Response("Missing timestamp", { status: 400 });
  }

  const cache = await caches.open(CACHE_NAME);
  const cached = await cache.match(buildCacheKey(timestamp));
  if (cached) {
    return cached;
  }

  return new Response("Not cached", { status: 404 });
};

self.addEventListener("message", (event: ExtendableMessageEvent) => {
  const { type } = event.data ?? {};
  if (type === "CHECK_CACHED") {
    handleCheck(event);
  }
  if (type === "FETCH_AND_CACHE") {
    handleFetchAndCache(event);
  }
  if (type === "EVICT") {
    handleEvict(event);
  }
});

const handleCheck = async (event: ExtendableMessageEvent) => {
  const { timestamp } = event.data;
  const cache = await caches.open(CACHE_NAME);
  const hit = await cache.match(buildCacheKey(timestamp));
  event.ports[0].postMessage({ cached: Boolean(hit) });
};

const handleFetchAndCache = async (event: ExtendableMessageEvent) => {
  const { presignedUrl, timestamp } = event.data;
  try {
    const cache = await caches.open(CACHE_NAME);
    const keys = await cache.keys();
    await Promise.all(keys.map((k) => cache.delete(k)));

    const response = await fetch(presignedUrl);
    if (!response.ok) {
      throw new Error(`S3 fetch failed: ${response.status}`);
    }

    await cache.put(buildCacheKey(timestamp), response.clone());
    event.ports[0].postMessage({ success: true });
  } catch (err) {
    event.ports[0].postMessage({
      error: (err as Error).message,
      success: false,
    });
  }
};

const handleEvict = async (event: ExtendableMessageEvent) => {
  const cache = await caches.open(CACHE_NAME);
  const keys = await cache.keys();
  await Promise.all(keys.map((k) => cache.delete(k)));
  event.ports[0]?.postMessage({ evicted: true });
};

const buildCacheKey = (timestamp: string): string => `${INTERCEPT_PATH}?t=${timestamp}`;

const serwist = new Serwist({
  clientsClaim: true,
  navigationPreload: true,
  precacheEntries: self.__SW_MANIFEST,
  runtimeCaching: [
    {
      handler: new NetworkFirst({ networkTimeoutSeconds: 3 }),
      matcher: ({ request }) => request.mode === "navigate",
    },
    {
      matcher: ({ url }) => url.pathname.startsWith("/stream"),
      handler: new NetworkOnly,
    },
    {
      handler: new NetworkFirst(),
      matcher: ({ url }) => url.pathname.startsWith("/api/"),
    },
    {
      handler: new NetworkOnly(),
      matcher: ({ url }) =>
        url.pathname.includes("/init-upload") || url.pathname.includes("/finalize-upload"),
    },
    {
      handler: new NetworkFirst(),
      matcher: ({ url }) => url.pathname.startsWith("/sw/"),
    },
    {
      // Only cache genuinely static assets — content-hashed filenames only
      handler: new CacheFirst({
        cacheName: "static-assets",
        plugins: [new ExpirationPlugin({ maxAgeSeconds: 60 * 60 * 24 * 30, maxEntries: 100 })],
      }),
      matcher: ({ url }) => /\.[0-9a-f]{8}\.(js|css|woff2|png|svg)$/.test(url.pathname),
    },
  ],
  skipWaiting: true,
});

serwist.addEventListeners();
