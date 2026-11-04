import { describe, expect, it } from "vitest";

import {
  isApiRequest,
  isProtectedRequest,
  resolveApiBase,
} from "@/utils/serviceWorkerPolicy";

const appOrigin = "https://alexandria.test";

describe("service worker API policy", () => {
  it("matches the configured API path without matching lookalike prefixes", () => {
    const apiBase = resolveApiBase("/api", appOrigin);

    expect(isApiRequest(new URL("/api", appOrigin), apiBase)).toBe(true);
    expect(isApiRequest(new URL("/api/files/1", appOrigin), apiBase)).toBe(true);
    expect(isApiRequest(new URL("/apiary/files/1", appOrigin), apiBase)).toBe(false);
    expect(isApiRequest(new URL("/files/1", appOrigin), apiBase)).toBe(false);
  });

  it("matches an absolute API origin and normalizes its trailing slash", () => {
    const apiBase = resolveApiBase("https://api.alexandria.test/api/", appOrigin);

    expect(isApiRequest(new URL("https://api.alexandria.test/api/files", appOrigin), apiBase)).toBe(
      true,
    );
    expect(isApiRequest(new URL("https://alexandria.test/api/files", appOrigin), apiBase)).toBe(
      false,
    );
  });

  it("treats every path on a root API origin as protected", () => {
    const apiBase = resolveApiBase("https://api.alexandria.test", appOrigin);

    expect(isApiRequest(new URL("https://api.alexandria.test/files/1", appOrigin), apiBase)).toBe(
      true,
    );
    expect(isApiRequest(new URL("https://alexandria.test/files/1", appOrigin), apiBase)).toBe(false);
  });

  it("falls back to the local API origin when no base URL is configured", () => {
    const apiBase = resolveApiBase(undefined, appOrigin);

    expect(apiBase.origin).toBe("http://localhost:5000");
    expect(isApiRequest(new URL("http://localhost:5000/files", appOrigin), apiBase)).toBe(true);
  });

  it("keeps protected media outside the worker cache", () => {
    const apiBase = resolveApiBase("/api", appOrigin);

    expect(isProtectedRequest(new URL("/images/user-1?v=1", appOrigin), apiBase, appOrigin)).toBe(
      true,
    );
    expect(isProtectedRequest(new URL("/stream/song/1", appOrigin), apiBase, appOrigin)).toBe(true);
    expect(isProtectedRequest(new URL("/preview/version-1/preview", appOrigin), apiBase, appOrigin)).toBe(
      true,
    );
    expect(isProtectedRequest(new URL("/images/user-1", "https://cdn.test"), apiBase, appOrigin)).toBe(
      false,
    );
    expect(isProtectedRequest(new URL("/assets/app-12345678.js", appOrigin), apiBase, appOrigin)).toBe(
      false,
    );
  });
});
