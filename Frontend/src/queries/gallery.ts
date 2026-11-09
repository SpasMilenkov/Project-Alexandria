import { defineQueryOptions } from "@pinia/colada";

import type { GallerySort } from "@/enums/gallery-sort";

import { type GalleryPhoto, galleryApi } from "@/api/gallery";
import { monthBounds, monthKey } from "@/features/gallery/months";

export const GALLERY_QUERY_KEYS = {
  root: ["gallery"] as const,
  timeline: (sort: GallerySort, timeZone: string) => [
    ...GALLERY_QUERY_KEYS.root,
    "timeline",
    sort,
    timeZone,
  ],
  // The zone is part of the key because uploaded months are the viewer's local months.
  month: (sort: GallerySort, timeZone: string, key: string) => [
    ...GALLERY_QUERY_KEYS.root,
    "month",
    sort,
    timeZone,
    key,
  ],
};

const MONTH_PAGE_SIZE = 500;
const GALLERY_STALE_MS = 30_000;

export interface GalleryMonthQuery {
  sort: GallerySort;
  timeZone: string;
  year: number;
  month: number;
}

// A month is the unit the timeline lays out, so its pages are fetched back to back. Each page
// continues from the previous cursor and stays inside the month through `since`.
const fetchMonth = async ({ sort, year, month }: GalleryMonthQuery) => {
  const { before, since } = monthBounds(sort, year, month);
  const photos: GalleryPhoto[] = [];
  let cursor: string | null = null;

  do {
    const params = cursor === null ? { before, since } : { cursor, since };
    // oxlint-disable-next-line no-await-in-loop -- each page needs the previous page's cursor
    const page = await galleryApi.getPhotos({ ...params, limit: MONTH_PAGE_SIZE, sort });

    photos.push(...page.items);
    cursor = page.nextCursor;
  } while (cursor !== null);

  return photos;
};

export const galleryTimeline = defineQueryOptions(
  ({ sort, timeZone }: { sort: GallerySort; timeZone: string }) => ({
    key: GALLERY_QUERY_KEYS.timeline(sort, timeZone),
    query: () => galleryApi.getTimeline(sort, timeZone),
    staleTime: GALLERY_STALE_MS,
  }),
);

export const galleryMonth = defineQueryOptions((query: GalleryMonthQuery) => ({
  key: GALLERY_QUERY_KEYS.month(query.sort, query.timeZone, monthKey(query.year, query.month)),
  query: () => fetchMonth(query),
  staleTime: GALLERY_STALE_MS,
}));
