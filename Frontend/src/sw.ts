/// <reference lib="webworker" />
/// <reference types="vite/client" />
import {
  CacheFirst,
  ExpirationPlugin,
  type HTTPMethod,
  NetworkFirst,
  NetworkOnly,
  type PrecacheEntry,
  Route,
  Serwist,
  type SerwistGlobalConfig,
  type SerwistPlugin,
} from "serwist";

import { isProtectedRequest, resolveConfiguredApiBase } from "./utils/serviceWorkerPolicy";

declare const self: ServiceWorkerGlobalScope;

declare global {
  interface WorkerGlobalScope extends SerwistGlobalConfig {
    __SW_MANIFEST: (PrecacheEntry | string)[] | undefined;
  }
}

const API_BASE = resolveConfiguredApiBase(self.location.origin);
const STATIC_CACHE_NAME = "static-assets";
const PROTECTED_METHODS: HTTPMethod[] = ["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE"];

const noStoreRequests: SerwistPlugin = {
  requestWillFetch: ({ request }) => new Request(request, { cache: "no-store" }),
};

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
      // Only cache genuinely static assets — content-hashed filenames only
      handler: new CacheFirst({
        cacheName: STATIC_CACHE_NAME,
        plugins: [new ExpirationPlugin({ maxAgeSeconds: 60 * 60 * 24 * 30, maxEntries: 100 })],
      }),
      matcher: ({ url }) =>
        /\.[0-9a-f]{8}\.(?<extension>js|css|woff2|png|svg)$/u.test(url.pathname),
    },
  ],
  skipWaiting: true,
});

const protectedHandler = new NetworkOnly({ plugins: [noStoreRequests] });

for (const method of PROTECTED_METHODS) {
  const route = new Route(
    ({ url }) => isProtectedRequest(url, API_BASE, self.location.origin),
    protectedHandler,
    method,
  );
  const routes = serwist.routes.get(method);

  if (routes) {
    routes.unshift(route);
  } else {
    serwist.routes.set(method, [route]);
  }
}

serwist.addEventListeners();
