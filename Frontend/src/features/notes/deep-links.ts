import { formatPlaybackTime } from "@/utils/date-formatters";

// "?t=90", "?t=1m30s", "?t=1h2m3s" (YouTube's forms) and "?t=1:30", "?t=1:02:03" (as people
// write times down). Anything else is not a time and the link simply opens.
const UNIT_TIME = /^(?:(?<h>\d+)h)?(?:(?<m>\d+)m)?(?:(?<s>\d+)s?)?$/u;
const CLOCK_TIME = /^(?:(?<h>\d+):)?(?<m>\d{1,2}):(?<s>\d{2})$/u;

const PAGE = /(?:^|&)page=(?<page>\d+)(?:&|$)/u;

// Hours past this are typos, not lecture recordings.
const MAX_SECONDS = 24 * 3600;

const splitSuffix = (suffix: string): { query: string; fragment: string } => {
  const hash = suffix.indexOf("#");
  const beforeHash = hash < 0 ? suffix : suffix.slice(0, hash);
  const fragment = hash < 0 ? "" : suffix.slice(hash + 1);

  return { fragment, query: beforeHash.startsWith("?") ? beforeHash.slice(1) : "" };
};

const toSeconds = (groups: Record<string, string | undefined>): number =>
  Number(groups.h ?? 0) * 3600 + Number(groups.m ?? 0) * 60 + Number(groups.s ?? 0);

const parseTime = (value: string): number | null => {
  const clock = CLOCK_TIME.exec(value)?.groups;

  if (clock) return toSeconds(clock);

  const units = value === "" ? undefined : UNIT_TIME.exec(value)?.groups;

  if (units) return toSeconds(units);

  return null;
};

/** A start time as people type it ("90", "1:30", "1m30s"), in whole seconds, or null. */
export const parseStartTime = (value: string): number | null => {
  const seconds = parseTime(value.trim().toLowerCase());

  if (seconds === null || seconds > MAX_SECONDS) return null;

  return seconds;
};

/** Where a `?t=` link starts playback, in whole seconds, or null when it names no time. */
export const startSecondsOf = (suffix: string): number | null => {
  const { query } = splitSuffix(suffix);
  const value = new URLSearchParams(query).get("t");

  if (value === null) return null;

  return parseStartTime(value);
};

/** The PDF page a `#page=` link opens, or null. Pages count from 1. */
export const pdfPageOf = (suffix: string): number | null => {
  const { fragment } = splitSuffix(suffix);
  const page = Number(PAGE.exec(fragment)?.groups?.page ?? Number.NaN);

  if (!Number.isInteger(page) || page < 1) return null;

  return page;
};

/** The suffix a timestamp link is written with: whole seconds, the form every reader accepts. */
export const startSuffix = (seconds: number): string => `?t=${Math.max(0, Math.floor(seconds))}`;

/** The suffix a PDF page link is written with. */
export const pageSuffix = (page: number): string => `#page=${Math.max(1, Math.floor(page))}`;

/** What a chip adds after its name for a deep link: "12:04" or "p. 3". */
export const deepLinkLabel = (suffix: string): string | null => {
  const seconds = startSecondsOf(suffix);

  if (seconds !== null) return formatPlaybackTime(seconds);

  const page = pdfPageOf(suffix);

  if (page !== null) return `p. ${page}`;

  return null;
};

/** A preview URL opened at a page; the browser's PDF viewer reads the fragment. */
export const atPdfPage = (url: string, page: number | null): string => {
  if (page === null) return url;

  return `${url.split("#")[0]}#page=${page}`;
};
