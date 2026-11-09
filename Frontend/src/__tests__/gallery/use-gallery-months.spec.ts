import type * as Colada from "@pinia/colada";

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { GalleryPhoto } from "@/api/gallery";

import { GallerySort } from "@/enums/gallery-sort";

const cache = vi.hoisted(() => ({
  ensure: vi.fn((options: { key: unknown[] }) => ({ key: options.key })),
  fetch: vi.fn(),
  refresh: vi.fn(),
}));

vi.mock("@pinia/colada", async (importOriginal) => ({
  ...(await importOriginal<typeof Colada>()),
  useQueryCache: () => cache,
}));

const { useGalleryMonths } = await import("@/composables/useGalleryMonths");

const photo = (id: string) => ({ fileId: id }) as GalleryPhoto;

const deferred = <T>() => {
  const handle: { resolve: (value: T) => void } = { resolve: () => undefined };
  const promise = new Promise<T>((done) => {
    handle.resolve = done;
  });

  return { promise, resolve: (value: T) => handle.resolve(value) };
};

const flush = () =>
  new Promise((resolve) => {
    setTimeout(resolve, 0);
  });

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
});

afterEach(() => {
  vi.useRealTimers();
  vi.clearAllMocks();
});

describe("useGalleryMonths", () => {
  it("stores a loaded month under its key", async () => {
    cache.refresh.mockResolvedValue({ data: [photo("a")], status: "success" });
    const months = useGalleryMonths(
      () => GallerySort.Taken,
      () => "UTC",
    );

    months.load([{ month: 6, year: 2019 }]);
    await flush();

    expect(months.loaded.value.get("2019-06")).toEqual([photo("a")]);
  });

  it("requests a month once while it is in flight", async () => {
    const pending = deferred<unknown>();
    cache.refresh.mockReturnValue(pending.promise);
    const months = useGalleryMonths(
      () => GallerySort.Taken,
      () => "UTC",
    );

    months.load([{ month: 6, year: 2019 }]);
    months.load([{ month: 6, year: 2019 }]);
    pending.resolve({ data: [], status: "success" });
    await flush();

    expect(cache.refresh).toHaveBeenCalledTimes(1);
  });

  it("lets a second caller wait for the month already in flight", async () => {
    const pending = deferred<unknown>();
    cache.refresh.mockReturnValue(pending.promise);
    const months = useGalleryMonths(
      () => GallerySort.Taken,
      () => "UTC",
    );

    void months.load([{ month: 6, year: 2019 }]);
    const waiting = months.load([{ month: 6, year: 2019 }]);
    pending.resolve({ data: [photo("a")], status: "success" });
    await waiting;

    expect(months.loaded.value.get("2019-06")).toEqual([photo("a")]);
  });

  it("drops photos from their months in place", async () => {
    cache.refresh.mockResolvedValue({ data: [photo("a"), photo("b")], status: "success" });
    const months = useGalleryMonths(
      () => GallerySort.Taken,
      () => "UTC",
    );

    await months.load([{ month: 6, year: 2019 }]);
    months.drop(new Set(["a"]));

    expect(months.loaded.value.get("2019-06")).toEqual([photo("b")]);
  });

  it("reloads a loaded month past the cache", async () => {
    cache.refresh.mockResolvedValue({ data: [photo("b")], status: "success" });
    cache.fetch.mockResolvedValue({ data: [photo("a"), photo("b")], status: "success" });
    const months = useGalleryMonths(
      () => GallerySort.Taken,
      () => "UTC",
    );

    await months.load([{ month: 6, year: 2019 }]);
    await months.reload([{ month: 6, year: 2019 }]);

    expect(cache.fetch).toHaveBeenCalledTimes(1);
    expect(months.loaded.value.get("2019-06")).toEqual([photo("a"), photo("b")]);
  });

  it("drops a response that started before a reset", async () => {
    const pending = deferred<unknown>();
    cache.refresh.mockReturnValue(pending.promise);
    const months = useGalleryMonths(
      () => GallerySort.Taken,
      () => "UTC",
    );

    months.load([{ month: 6, year: 2019 }]);
    months.reset();
    pending.resolve({ data: [photo("stale")], status: "success" });
    await flush();

    expect(months.loaded.value.size).toBe(0);
  });

  it("waits before retrying a month that failed", async () => {
    cache.refresh.mockResolvedValue({ error: new Error("boom"), status: "error" });
    const months = useGalleryMonths(
      () => GallerySort.Taken,
      () => "UTC",
    );

    months.load([{ month: 6, year: 2019 }]);
    await flush();
    months.load([{ month: 6, year: 2019 }]);
    await flush();

    expect(cache.refresh).toHaveBeenCalledTimes(1);

    vi.setSystemTime(Date.now() + 16_000);
    months.load([{ month: 6, year: 2019 }]);
    await flush();

    expect(cache.refresh).toHaveBeenCalledTimes(2);
  });

  it("keys months by sort and zone so the two orderings never share a cache entry", async () => {
    cache.refresh.mockResolvedValue({ data: [], status: "success" });
    const months = useGalleryMonths(
      () => GallerySort.Uploaded,
      () => "Europe/Sofia",
    );

    months.load([{ month: 6, year: 2019 }]);
    await flush();

    expect(cache.ensure.mock.calls[0][0].key).toEqual([
      "gallery",
      "month",
      GallerySort.Uploaded,
      "Europe/Sofia",
      "2019-06",
    ]);
  });
});
