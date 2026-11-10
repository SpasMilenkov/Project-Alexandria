import type { GalleryMonth, GalleryPhoto } from "@/api/gallery";

import { GallerySort } from "@/enums/gallery-sort";
import { formatDateTime } from "@/utils/date-formatters";

import { monthKey } from "./months";

export interface ViewerPosition {
  monthIndex: number;
  photoIndex: number;
}

export type ViewerStep =
  | { kind: "photo"; photo: GalleryPhoto }
  | { kind: "load"; month: GalleryMonth }
  | { kind: "end" };

type LoadedMonths = ReadonlyMap<string, GalleryPhoto[]>;

const photosOf = (loaded: LoadedMonths, month: GalleryMonth) =>
  loaded.get(monthKey(month.year, month.month));

/** Where a photo sits in the timeline, or null when its month is not loaded. */
export const locatePhoto = (
  months: GalleryMonth[],
  loaded: LoadedMonths,
  fileId: string,
): ViewerPosition | null => {
  for (const [monthIndex, month] of months.entries()) {
    const photoIndex = photosOf(loaded, month)?.findIndex((photo) => photo.fileId === fileId) ?? -1;

    if (photoIndex >= 0) return { monthIndex, photoIndex };
  }

  return null;
};

export const findPhoto = (months: GalleryMonth[], loaded: LoadedMonths, fileId: string) => {
  const position = locatePhoto(months, loaded, fileId);
  if (!position) return null;

  return photosOf(loaded, months[position.monthIndex])?.[position.photoIndex] ?? null;
};

/**
 * The photo one step away in timeline order, crossing into the neighbouring month when the
 * current one runs out. A neighbouring month that has not loaded yet comes back as a `load`
 * step, so the caller can fetch it and ask again instead of skipping over it.
 */
export const stepFrom = (
  months: GalleryMonth[],
  loaded: LoadedMonths,
  { monthIndex, photoIndex }: ViewerPosition,
  direction: 1 | -1,
): ViewerStep => {
  const current = photosOf(loaded, months[monthIndex]) ?? [];
  const sibling = current[photoIndex + direction];
  if (sibling) return { kind: "photo", photo: sibling };

  for (
    let index = monthIndex + direction;
    index >= 0 && index < months.length;
    index += direction
  ) {
    const month = months[index];
    const photos = photosOf(loaded, month);

    if (photos === undefined) return { kind: "load", month };

    // A month whose photos were all removed since the timeline loaded is passed over.
    const edge = direction === 1 ? photos[0] : photos[photos.length - 1];
    if (edge) return { kind: "photo", photo: edge };
  }

  return { kind: "end" };
};

/** The date the timeline files a photo under, read the same way its month was bucketed. */
export const photoDateLabel = (photo: GalleryPhoto, sort: GallerySort) => {
  if (sort === GallerySort.Taken) return formatDateTime(photo.takenAt, { timeZone: "UTC" });

  return formatDateTime(photo.uploadedAt);
};

// Formats every browser renders as-is. Anything else (HEIC, TIFF, RAW) waits for the server's
// rendered preview, since the original would not display.
const BROWSER_IMAGE_TYPES = new Set([
  "image/avif",
  "image/bmp",
  "image/gif",
  "image/jpeg",
  "image/png",
  "image/webp",
]);

export const browserCanShowOriginal = (mimeType: string) =>
  BROWSER_IMAGE_TYPES.has(mimeType.toLowerCase());

// The server preview is a still frame, so an animated GIF would stop moving in it.
export const prefersOriginal = (mimeType: string) => mimeType.toLowerCase() === "image/gif";
