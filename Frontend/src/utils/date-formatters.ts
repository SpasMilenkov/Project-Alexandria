export type DateInput = string | number | Date;

const SECOND_MS = 1000;
const MINUTE_MS = 60 * SECOND_MS;
const HOUR_MS = 60 * MINUTE_MS;
const DAY_MS = 24 * HOUR_MS;

// Averaged so month and year counts stay right across short months and leap years.
const MONTH_DAYS = 30.4375;
const YEAR_DAYS = 365.25;

export const formatDuration = (seconds: number): string => {
  const s = Math.floor(seconds);
  const d = Math.floor(s / 86400);
  const h = Math.floor((s % 86400) / 3600);
  const m = Math.floor((s % 3600) / 60);
  const sec = s % 60;

  const pad = (n: number) => String(n).padStart(2, "0");

  if (d > 0) return `${d}:${pad(h)}:${pad(m)}:${pad(sec)}`;
  if (h > 0) return `${h}:${pad(m)}:${pad(sec)}`;
  return `${m}:${pad(sec)}`;
};

const CALENDAR_DATE = new Intl.DateTimeFormat(undefined, {
  day: "numeric",
  month: "short",
  year: "numeric",
});

// Absolute day for detail panels, paired with the relative formatTimeAgo label.
export const formatCalendarDate = (value: DateInput): string =>
  CALENDAR_DATE.format(new Date(value));

interface ElapsedUnit {
  unit: Intl.RelativeTimeFormatUnit;
  value: number;
}

// Counts round down, so a moment never reads as further away than it is.
const largestUnit = (elapsedMs: number): ElapsedUnit => {
  const hours = Math.floor(elapsedMs / HOUR_MS);
  const days = Math.floor(elapsedMs / DAY_MS);

  if (hours < 1) return { unit: "minute", value: Math.floor(elapsedMs / MINUTE_MS) };
  if (days < 1) return { unit: "hour", value: hours };
  if (days < 7) return { unit: "day", value: days };
  if (days < 30) return { unit: "week", value: Math.floor(days / 7) };
  if (days < 365) return { unit: "month", value: Math.max(1, Math.floor(days / MONTH_DAYS)) };

  return { unit: "year", value: Math.max(1, Math.floor(days / YEAR_DAYS)) };
};

// "1 day ago" rather than "yesterday", which would be wrong for 30 hours spanning two midnights.
const RELATIVE_FORMATS = {
  long: new Intl.RelativeTimeFormat(undefined, { numeric: "always", style: "long" }),
  narrow: new Intl.RelativeTimeFormat(undefined, { numeric: "always", style: "narrow" }),
};

export interface TimeAgoOptions {
  /** "long" reads "3 minutes ago"; "narrow" reads "3m ago" for chips and dense rows. */
  style?: "long" | "narrow";
  /** Older moments show as a calendar date instead, where "7 months ago" is too vague. */
  absoluteAfterDays?: number;
  /** Pins the clock, for tests and for rows formatted against one shared instant. */
  now?: number;
}

/**
 * "just now", "3 minutes ago", "2 weeks ago", or "in 3 days" for future moments. Always
 * lowercase, so it reads the same alone and inside a sentence ("Deleted 3 days ago").
 */
export const formatTimeAgo = (value: DateInput, options: TimeAgoOptions = {}): string => {
  const { absoluteAfterDays, now = Date.now(), style = "long" } = options;
  const time = new Date(value).getTime();

  if (Number.isNaN(time)) return "";

  const offsetMs = time - now;
  const elapsedMs = Math.abs(offsetMs);

  if (absoluteAfterDays !== undefined && elapsedMs >= absoluteAfterDays * DAY_MS)
    return formatCalendarDate(time);
  if (elapsedMs < MINUTE_MS) return "just now";

  const { unit, value: count } = largestUnit(elapsedMs);

  return RELATIVE_FORMATS[style].format(offsetMs < 0 ? -count : count, unit);
};

const narrowUnit = (unit: "second" | "minute" | "hour" | "day") =>
  new Intl.NumberFormat(undefined, { style: "unit", unit, unitDisplay: "narrow" });

const AGE_SECONDS = narrowUnit("second");
const AGE_MINUTES = narrowUnit("minute");
const AGE_HOURS = narrowUnit("hour");
const AGE_DAYS = narrowUnit("day");

/**
 * How long something has lasted since `value`, as a bare span for "for 12m" phrasing. Stops at
 * days because narrow minutes and months would both read "m".
 */
export const formatAgeShort = (value: DateInput, now = Date.now()): string => {
  const elapsedMs = Math.max(0, now - new Date(value).getTime());

  if (Number.isNaN(elapsedMs)) return "";
  if (elapsedMs < MINUTE_MS) return AGE_SECONDS.format(Math.floor(elapsedMs / SECOND_MS));
  if (elapsedMs < HOUR_MS) return AGE_MINUTES.format(Math.floor(elapsedMs / MINUTE_MS));
  if (elapsedMs < DAY_MS) return AGE_HOURS.format(Math.floor(elapsedMs / HOUR_MS));

  return AGE_DAYS.format(Math.floor(elapsedMs / DAY_MS));
};

// "yyyy-MM-dd" in UTC, the wire format of the backend's DateOnly day buckets.
export const utcDayKey = (date: Date | number): string => new Date(date).toISOString().slice(0, 10);

// "yyyy-MM" in UTC.
export const utcMonthKey = (date: Date | number): string =>
  new Date(date).toISOString().slice(0, 7);

// "yyyy-MM-dd" on the viewer's local calendar, for days the user picked or sees as "today".
export const localDayKey = (date: Date): string =>
  `${String(date.getFullYear()).padStart(4, "0")}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
