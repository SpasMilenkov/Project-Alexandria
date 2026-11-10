import type { GalleryMonth, GalleryPhoto } from "@/api/gallery";

import { formatCount } from "@/utils/count";

import { monthKey } from "./months";

type LoadedMonths = ReadonlyMap<string, GalleryPhoto[]>;

/** Every loaded photo in timeline order, newest first, skipping months not loaded yet. */
export const photosInOrder = (months: GalleryMonth[], loaded: LoadedMonths) =>
  months.flatMap((month) => loaded.get(monthKey(month.year, month.month)) ?? []);

/**
 * The photos from one to the other inclusive, whichever comes first in the timeline, for
 * shift-click. Empty when either end is no longer loaded, so a stale anchor selects nothing
 * rather than guessing.
 */
export const photoRange = (
  months: GalleryMonth[],
  loaded: LoadedMonths,
  fromId: string,
  toId: string,
) => {
  const photos = photosInOrder(months, loaded);
  const from = photos.findIndex((photo) => photo.fileId === fromId);
  const to = photos.findIndex((photo) => photo.fileId === toId);

  if (from < 0 || to < 0) return [];

  return photos.slice(Math.min(from, to), Math.max(from, to) + 1);
};

/** The months the photos belong to, for reloading them once the photos come back. */
export const monthsHolding = (
  months: GalleryMonth[],
  loaded: LoadedMonths,
  fileIds: ReadonlySet<string>,
) =>
  months.filter((month) =>
    (loaded.get(monthKey(month.year, month.month)) ?? []).some((photo) =>
      fileIds.has(photo.fileId),
    ),
  );

/** "the photo" style subjects for toasts: a name for one, a count for several. */
export const photosSubject = (photos: GalleryPhoto[]) => {
  if (photos.length === 1) return `"${photos[0].name}"`;

  return formatCount(photos.length, "photo");
};

/**
 * What a click on a photo does, following the file explorer: ctrl or cmd toggles one photo,
 * shift selects from the anchor to here (replacing the selection, so it works both ways round
 * the anchor), and ctrl+shift adds that range instead. A plain mouse click while selecting
 * picks just that photo, as in the explorer; a tap toggles, since touch has no modifiers.
 */
export type PhotoClick = "open" | "toggle" | "only" | "range" | "add-range";

/** A click that changes the selection rather than opening the photo. */
export type PhotoPick = Exclude<PhotoClick, "open">;

export interface PhotoClickInput {
  shift: boolean;
  // Ctrl on Windows and Linux, cmd on macOS.
  toggleKey: boolean;
  selecting: boolean;
  touch: boolean;
}

export const photoClick = ({ shift, toggleKey, selecting, touch }: PhotoClickInput): PhotoClick => {
  if (shift && toggleKey) return "add-range";
  if (shift) return "range";
  if (toggleKey) return "toggle";
  if (!selecting) return "open";

  return touch ? "toggle" : "only";
};
