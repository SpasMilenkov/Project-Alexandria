import type { MomentLimit } from "./link-picker";

export type TimestampUnit = "hours" | "minutes" | "seconds";

export type TimestampParts = Record<TimestampUnit, number>;

// Links past a day are typos, matching what links accept when typed.
const MAX_SECONDS = 24 * 3600 - 1;

/** Hours get a segment only for recordings an hour or longer, or of unknown length. */
export const showsHours = (limit: MomentLimit): boolean => limit === null || limit >= 3600;

export const timestampUnits = (limit: MomentLimit): TimestampUnit[] => {
  if (showsHours(limit)) return ["hours", "minutes", "seconds"];

  return ["minutes", "seconds"];
};

/** The last second a link can start at: one before the end, which would play nothing. */
export const lastStart = (limit: MomentLimit): number => {
  if (limit === null) return MAX_SECONDS;

  return Math.max(0, Math.min(MAX_SECONDS, limit - 1));
};

export const clampTimestamp = (seconds: number, limit: MomentLimit): number =>
  Math.min(Math.max(0, Math.floor(seconds)), lastStart(limit));

export const splitTimestamp = (total: number, limit: MomentLimit): TimestampParts => {
  const seconds = total % 60;

  if (!showsHours(limit)) return { hours: 0, minutes: Math.floor(total / 60), seconds };

  return { hours: Math.floor(total / 3600), minutes: Math.floor((total % 3600) / 60), seconds };
};

export const joinTimestamp = (parts: TimestampParts): number =>
  parts.hours * 3600 + parts.minutes * 60 + parts.seconds;

/** The largest value a segment takes; the total is clamped to the recording's length after. */
export const unitMax = (unit: TimestampUnit, limit: MomentLimit): number => {
  if (unit !== "hours") return 59;

  return Math.floor(lastStart(limit) / 3600);
};

/** Two digits per segment, as a clock shows them. */
export const padUnit = (value: number): string => String(value).padStart(2, "0");
