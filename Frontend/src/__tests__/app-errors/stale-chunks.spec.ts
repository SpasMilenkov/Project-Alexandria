import { afterEach, describe, expect, it, vi } from "vitest";

import { isStaleChunkError, reloadForStaleChunk } from "@/features/app-errors/stale-chunks";

describe("stale chunk detection", () => {
  it.each([
    "Failed to fetch dynamically imported module: http://host/assets/HomeView-abc.js",
    "error loading dynamically imported module: http://host/assets/HomeView-abc.js",
    "Importing a module script failed.",
    "Unable to preload CSS for /assets/HomeView-abc.css",
  ])("recognises %s", (message) => {
    expect(isStaleChunkError(new Error(message))).toBe(true);
  });

  it("ignores ordinary errors and non-errors", () => {
    expect(isStaleChunkError(new Error("Cannot read properties of undefined"))).toBe(false);
    expect(isStaleChunkError("Failed to fetch dynamically imported module")).toBe(false);
    expect(isStaleChunkError(undefined)).toBe(false);
  });
});

describe("stale chunk reload", () => {
  afterEach(() => {
    sessionStorage.clear();
    vi.restoreAllMocks();
  });

  it("navigates to the target page on the first failure", () => {
    const navigate = vi.fn();

    expect(reloadForStaleChunk("/home", { navigate, now: 100_000 })).toBe(true);
    expect(navigate).toHaveBeenCalledWith("/home");
  });

  it("refuses a second reload within the cooldown so a missing file cannot loop", () => {
    const navigate = vi.fn();

    reloadForStaleChunk("/home", { navigate, now: 100_000 });

    expect(reloadForStaleChunk("/home", { navigate, now: 105_000 })).toBe(false);
    expect(navigate).toHaveBeenCalledTimes(1);
  });

  it("reloads again once the cooldown has passed", () => {
    const navigate = vi.fn();

    reloadForStaleChunk("/home", { navigate, now: 100_000 });

    expect(reloadForStaleChunk("/stats", { navigate, now: 111_000 })).toBe(true);
    expect(navigate).toHaveBeenLastCalledWith("/stats");
  });

  it("stays put when session storage is unavailable", () => {
    const navigate = vi.fn();
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("blocked");
    });

    expect(reloadForStaleChunk("/home", { navigate, now: 100_000 })).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });
});
