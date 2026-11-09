import { useQueryCache } from "@pinia/colada";
import { shallowRef } from "vue";

import type { GalleryPhoto } from "@/api/gallery";
import type { GallerySort } from "@/enums/gallery-sort";

import { monthKey } from "@/features/gallery/months";
import { galleryMonth } from "@/queries/gallery";

export interface MonthRef {
  year: number;
  month: number;
}

// A month that failed waits this long before scrolling past it asks again, so a server
// error does not turn every scroll event into another request.
const RETRY_AFTER_MS = 15_000;

/**
 * Loads timeline months on demand through the query cache, which dedupes concurrent requests
 * for one month and keeps loaded months across navigation. Months are fetched from the
 * scroller's visible range instead of one useQuery each, because the set of mounted months
 * changes on every scroll.
 */
export const useGalleryMonths = (sort: () => GallerySort, timeZone: () => string) => {
  const queryCache = useQueryCache();

  const loaded = shallowRef<ReadonlyMap<string, GalleryPhoto[]>>(new Map());
  // Kept as promises so a second caller for the same month waits on the request already out.
  const inFlight = new Map<string, Promise<void>>();
  const failedAt = new Map<string, number>();

  // Bumped on reset so responses for an older sort or library state are dropped.
  let generation = 0;

  const store = (key: string, photos: GalleryPhoto[]) => {
    const next = new Map(loaded.value);
    next.set(key, photos);
    loaded.value = next;
  };

  // `fresh` skips the cache, for a month whose photos changed on the server since it loaded.
  const fetchMonth = async (key: string, { year, month }: MonthRef, fresh = false) => {
    const startedIn = generation;

    const entry = queryCache.ensure(
      galleryMonth({ month, sort: sort(), timeZone: timeZone(), year }),
    );
    const state = await (fresh ? queryCache.fetch(entry) : queryCache.refresh(entry));

    if (startedIn !== generation) return;
    inFlight.delete(key);

    if (state.status === "success") {
      failedAt.delete(key);
      store(key, state.data);
      return;
    }

    // The placeholder stays in place and the next visible-range update retries it.
    failedAt.set(key, Date.now());
  };

  const loadMonth = (target: MonthRef) => {
    const key = monthKey(target.year, target.month);
    const lastFailure = failedAt.get(key);

    if (loaded.value.has(key)) return Promise.resolve();

    const pending = inFlight.get(key);
    if (pending) return pending;

    if (lastFailure !== undefined && Date.now() - lastFailure < RETRY_AFTER_MS) {
      return Promise.resolve();
    }

    const request = fetchMonth(key, target);
    inFlight.set(key, request);

    return request;
  };

  // Settles once every requested month has loaded or failed, for the viewer stepping into a
  // month it has to wait for. Scroll-driven callers fire and forget.
  const load = async (months: MonthRef[]) => {
    await Promise.all(months.map(loadMonth));
  };

  // Trashed photos leave their months in place, so the grid closes the gap where the photos
  // were instead of reloading the whole timeline and losing the scroll position.
  const drop = (fileIds: ReadonlySet<string>) => {
    const next = new Map(loaded.value);

    for (const [key, photos] of next) {
      const kept = photos.filter((photo) => !fileIds.has(photo.fileId));
      if (kept.length !== photos.length) next.set(key, kept);
    }

    loaded.value = next;
  };

  // Fetches months again even though they are loaded, for photos coming back from the trash.
  // The old photos stay on screen until the fresh ones replace them.
  const reload = async (months: MonthRef[]) => {
    await Promise.all(
      months.map(async (target) => {
        const key = monthKey(target.year, target.month);

        inFlight.delete(key);
        failedAt.delete(key);
        await fetchMonth(key, target, true);
      }),
    );
  };

  const reset = () => {
    generation += 1;
    inFlight.clear();
    failedAt.clear();
    loaded.value = new Map();
  };

  return { drop, load, loaded, reload, reset };
};
