import { formatOffset } from "./capture-metadata";

// Formatters run in UTC on a pre-shifted timestamp, so the digits stay those of the author's
// clock instead of moving into the viewer's zone.
const DOCUMENT_DATE = new Intl.DateTimeFormat(undefined, {
  day: "numeric",
  month: "short",
  year: "numeric",
  timeZone: "UTC",
});

const LANGUAGE_NAMES = new Intl.DisplayNames(undefined, { type: "language" });

// Intl throws on malformed tags, and files carry whatever their app wrote, so only tags shaped
// like BCP 47 are handed to it.
const LANGUAGE_TAG = /^[a-z]{2,3}(?:-[a-z0-9]{2,8})*$/iu;

const NUMBER = new Intl.NumberFormat();

export interface DocumentMoment {
  date: string;
  /** Present only when the file recorded its time zone. */
  zoneNote?: string;
}

/** The day as the author's own clock showed it. */
export const describeDocumentMoment = (
  at: string,
  offsetMinutes: number | null,
): DocumentMoment => {
  const wallClock = new Date(new Date(at).getTime() + (offsetMinutes ?? 0) * 60_000);
  const moment: DocumentMoment = { date: DOCUMENT_DATE.format(wallClock) };

  if (offsetMinutes !== null)
    moment.zoneNote = `Author's time zone, ${formatOffset(offsetMinutes)}`;

  return moment;
};

export const formatEditingTime = (minutes: number): string => {
  if (minutes < 60) return `${minutes} min`;

  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;

  if (rest === 0) return `${NUMBER.format(hours)} h`;

  return `${NUMBER.format(hours)} h ${rest} min`;
};

/** A readable language name for a tag like "en-US", or the tag itself when it is not one. */
export const formatLanguage = (tag: string): string => {
  if (!LANGUAGE_TAG.test(tag)) return tag;

  return LANGUAGE_NAMES.of(tag) ?? tag;
};

/** "Book 2", or "Book 2.5" for the novellas series slot in between. */
export const formatSeriesPosition = (index: number): string => `Book ${NUMBER.format(index)}`;

export interface DocumentCounts {
  pageCount: number | null;
  slideCount: number | null;
  sheetCount: number | null;
  wordCount: number | null;
}

export interface CountTile {
  key: string;
  label: string;
  value: string;
}

/** One tile per count the file recorded, in reading order. */
export const countTiles = (counts: DocumentCounts): CountTile[] => {
  const tiles: CountTile[] = [];

  if (counts.pageCount !== null) {
    tiles.push({ key: "pages", label: "Pages", value: NUMBER.format(counts.pageCount) });
  }

  if (counts.slideCount !== null) {
    tiles.push({ key: "slides", label: "Slides", value: NUMBER.format(counts.slideCount) });
  }

  if (counts.sheetCount !== null) {
    tiles.push({ key: "sheets", label: "Sheets", value: NUMBER.format(counts.sheetCount) });
  }

  if (counts.wordCount !== null) {
    tiles.push({ key: "words", label: "Words", value: NUMBER.format(counts.wordCount) });
  }

  return tiles;
};
