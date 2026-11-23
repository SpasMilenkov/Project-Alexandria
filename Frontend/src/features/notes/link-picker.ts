import { formatCount } from "@/utils/count";
import { formatPlaybackTime } from "@/utils/date-formatters";
import { PREVIEW_PAGE_LIMIT, hasPagedPreview } from "@/utils/files/mime-catalog";

import { pageSuffix, parseStartTime, pdfPageOf, startSecondsOf, startSuffix } from "./deep-links";

/** The part of a file a link can point into: a moment of audio or video, or a page of a PDF. */
export type LinkMoment = "time" | "page";

export const momentOfMime = (mimeType: string | null | undefined): LinkMoment | null => {
  if (!mimeType) return null;
  if (mimeType.startsWith("audio/") || mimeType.startsWith("video/")) return "time";
  if (hasPagedPreview(mimeType)) return "page";

  return null;
};

// The link's own syntax typed straight after a name, for anyone who knows it.
const TYPED_SUFFIX = /\?t=|#page=/iu;

/** The picker's text split into the name to search for and a `?t=`/`#page=` typed after it. */
export const splitPickerQuery = (query: string): { name: string; typedSuffix: string | null } => {
  const match = TYPED_SUFFIX.exec(query);

  if (!match) return { name: query, typedSuffix: null };

  return { name: query.slice(0, match.index).trim(), typedSuffix: query.slice(match.index) };
};

/** A typed `?t=`/`#page=` written the way links are saved, or null when it doesn't fit the file. */
export const normalizeTypedSuffix = (moment: LinkMoment | null, typed: string): string | null => {
  if (moment === "time") {
    const seconds = startSecondsOf(typed);

    return seconds === null ? null : startSuffix(seconds);
  }

  if (moment === "page") {
    const page = pdfPageOf(typed);

    return page === null ? null : pageSuffix(page);
  }

  return null;
};

/** A typed page past what every preview keeps, which no link can open. */
export const isPastPreview = (suffix: string): boolean =>
  (pdfPageOf(suffix) ?? 0) > PREVIEW_PAGE_LIMIT;

/** "at 1:30" or "page 4", for a suffix that was typed or picked. */
export const momentLabel = (suffix: string): string | null => {
  const seconds = startSecondsOf(suffix);

  if (seconds !== null) return `at ${formatPlaybackTime(seconds)}`;

  const page = pdfPageOf(suffix);

  if (page !== null) return `page ${page}`;

  return null;
};

const TIME_PREFIX = /^(?:\?t=|t=|@|at\s+)/iu;
const PAGE_INPUT = /^(?:#?page\s*=?\s*|p\.?\s*)?(?<page>\d+)$/iu;

/** What was typed after a completed name, read as a start time or a page. */
export type MomentInput =
  | { state: "empty" }
  | { state: "valid"; suffix: string }
  | { state: "invalid"; text: string; reason: "unreadable" | "too-far" };

/**
 * How far a link can point into a file: its length in whole seconds or its page count, when the
 * server knows it.
 */
export type MomentLimit = number | null;

export const readMomentInput = (
  moment: LinkMoment,
  text: string,
  playingSeconds: number | null,
  limit: MomentLimit = null,
): MomentInput => {
  const value = text.trim();

  if (value === "") return { state: "empty" };

  if (moment === "page") {
    const page = Number(PAGE_INPUT.exec(value)?.groups?.page ?? Number.NaN);

    if (!Number.isInteger(page) || page < 1) {
      return { reason: "unreadable", state: "invalid", text: value };
    }

    if (limit !== null && page > limit) return { reason: "too-far", state: "invalid", text: value };

    return { state: "valid", suffix: pageSuffix(page) };
  }

  const time = value.replace(TIME_PREFIX, "");

  if (time.toLowerCase() === "now" && playingSeconds !== null) {
    return { state: "valid", suffix: startSuffix(playingSeconds) };
  }

  const seconds = parseStartTime(time);

  if (seconds === null) return { reason: "unreadable", state: "invalid", text: value };

  // Starting exactly at the end plays nothing, so the last usable second is one before it.
  if (limit !== null && seconds >= Math.max(1, limit)) {
    return { reason: "too-far", state: "invalid", text: value };
  }

  return { state: "valid", suffix: startSuffix(seconds) };
};

// "Runs 12:34." or "The preview has 5 pages.", in front of the step's hint when it is known.
const extentOf = (moment: LinkMoment, limit: MomentLimit): string | null => {
  if (limit === null) return null;
  if (moment === "page") return `The preview has ${formatCount(limit, "page")}.`;

  return `Runs ${formatPlaybackTime(limit)}.`;
};

const tooFarMessage = (moment: LinkMoment, text: string, limit: MomentLimit): string => {
  if (moment === "page") return `"${text}" is past the preview's last page (${limit ?? "?"})`;

  return `"${text}" is past the end (${formatPlaybackTime(limit ?? 0)})`;
};

/** The words the picker shows while it waits for a time or a page after a completed name. */
export interface MomentStep {
  hint: string;
  description: string;
  // Set when the typed text can't be used, so Enter waits for a fix.
  blocked: boolean;
  suffix: string;
}

export const momentStep = (
  moment: LinkMoment,
  input: MomentInput,
  playingSeconds: number | null,
  limit: MomentLimit = null,
): MomentStep => {
  const now =
    playingSeconds === null ? "" : `, or "now" for ${formatPlaybackTime(playingSeconds)}`;

  const ask =
    moment === "time"
      ? `Type a start time like 1:30${now}. Enter starts from the beginning.`
      : "Type a page number. Enter opens the first page.";
  const extent = extentOf(moment, limit);
  const hint = extent === null ? ask : `${extent} ${ask}`;

  if (input.state === "invalid" && input.reason === "too-far") {
    const description = tooFarMessage(moment, input.text, limit);

    return { blocked: true, description, hint, suffix: "" };
  }

  if (input.state === "invalid") {
    const noun = moment === "time" ? "a time" : "a page number";

    return { blocked: true, description: `"${input.text}" isn't ${noun}`, hint, suffix: "" };
  }

  if (input.state === "valid") {
    const label = momentLabel(input.suffix) ?? "";
    const verb = moment === "time" ? "Starts" : "Opens at";

    return { blocked: false, description: `${verb} ${label}`, hint, suffix: input.suffix };
  }

  const fromStart = moment === "time" ? "From the beginning" : "From the first page";

  return { blocked: false, description: fromStart, hint, suffix: "" };
};
