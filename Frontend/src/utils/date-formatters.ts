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

export interface DateFormatOptions {
  /** "UTC" for days the backend buckets in UTC, so they do not shift a day for the viewer. */
  timeZone?: string;
}

export interface TimeFormatOptions extends DateFormatOptions {
  seconds?: boolean;
}

export interface FullDateOptions extends DateFormatOptions {
  /** "Wed, Mar 4, 2026" for narrow layouts instead of "Wednesday, March 4, 2026". */
  compact?: boolean;
}

const formatterCache = new Map<string, Intl.DateTimeFormat>();

// Building an Intl formatter costs far more than using one, and lists format many rows.
const formatterFor = (options: Intl.DateTimeFormatOptions): Intl.DateTimeFormat => {
  const key = JSON.stringify(options);
  const cached = formatterCache.get(key);

  if (cached) return cached;

  const formatter = new Intl.DateTimeFormat(undefined, options);
  formatterCache.set(key, formatter);

  return formatter;
};

const toValidDate = (value: DateInput): Date | null => {
  const date = new Date(value);

  return Number.isNaN(date.getTime()) ? null : date;
};

// Intl throws on an invalid date, and a blank label beats a crashed render.
const formatWith = (options: Intl.DateTimeFormatOptions, value: DateInput): string => {
  const date = toValidDate(value);

  return date ? formatterFor(options).format(date) : "";
};

const formatRangeWith = (
  options: Intl.DateTimeFormatOptions,
  start: DateInput,
  end: DateInput,
): string => {
  const from = toValidDate(start);
  const to = toValidDate(end);

  if (!from || !to) return "";

  return formatterFor(options).formatRange(from, to);
};

const dayMonthOptions = ({ timeZone }: DateFormatOptions): Intl.DateTimeFormatOptions => ({
  day: "numeric",
  month: "short",
  timeZone,
});

const calendarOptions = ({ timeZone }: DateFormatOptions): Intl.DateTimeFormatOptions => ({
  day: "numeric",
  month: "short",
  timeZone,
  year: "numeric",
});

const clockOptions = ({ seconds, timeZone }: TimeFormatOptions): Intl.DateTimeFormatOptions => ({
  hour: "numeric",
  minute: "2-digit",
  second: seconds ? "2-digit" : undefined,
  timeZone,
});

/** "Mar 4", for chips and tooltips where the year is obvious. */
export const formatDayMonth = (value: DateInput, options: DateFormatOptions = {}): string =>
  formatWith(dayMonthOptions(options), value);

/** "Mar 4 – 9", collapsing whatever the two ends share. */
export const formatDayMonthRange = (
  start: DateInput,
  end: DateInput,
  options: DateFormatOptions = {},
): string => formatRangeWith(dayMonthOptions(options), start, end);

/** "Mar 4, 2026", the absolute day paired with a relative formatTimeAgo label. */
export const formatCalendarDate = (value: DateInput, options: DateFormatOptions = {}): string =>
  formatWith(calendarOptions(options), value);

/** "Mar 4 – 9, 2026", collapsing whatever the two ends share. */
export const formatCalendarDateRange = (
  start: DateInput,
  end: DateInput,
  options: DateFormatOptions = {},
): string => formatRangeWith(calendarOptions(options), start, end);

/** "Wednesday, March 4, 2026", for headings that name a single day. */
export const formatFullDate = (value: DateInput, options: FullDateOptions = {}): string => {
  const style = options.compact ? "short" : "long";

  return formatWith(
    {
      day: "numeric",
      month: style,
      timeZone: options.timeZone,
      weekday: style,
      year: "numeric",
    },
    value,
  );
};

/** "2:07 PM", or "14:07" where the viewer's locale uses a 24-hour clock. */
export const formatClockTime = (value: DateInput, options: TimeFormatOptions = {}): string =>
  formatWith(clockOptions(options), value);

/** "Mar 4, 2026, 2:07 PM", for an exact moment such as an audit entry or an expiry. */
export const formatDateTime = (value: DateInput, options: TimeFormatOptions = {}): string =>
  formatWith({ ...calendarOptions(options), ...clockOptions(options) }, value);

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

// Counts midnights on the viewer's calendar rather than 24-hour spans, so 23:50 the night before
// is one day back and a daylight-saving day still counts as one.
const localDayNumber = (date: Date): number => {
  const utc = new Date(0);
  utc.setUTCFullYear(date.getFullYear(), date.getMonth(), date.getDate());

  return utc.getTime() / DAY_MS;
};

/** Whole calendar days from `from` to `to` on the viewer's calendar; negative when `to` is earlier. */
export const calendarDaysBetween = (from: DateInput, to: DateInput): number =>
  localDayNumber(new Date(to)) - localDayNumber(new Date(from));

const RELATIVE_DAYS = new Intl.RelativeTimeFormat(undefined, { numeric: "auto" });

const capitalizeFirst = (text: string): string => text.charAt(0).toLocaleUpperCase() + text.slice(1);

export interface DaysAgoOptions {
  /** "Today" for a heading or a tile, rather than "today" inside a sentence. */
  capitalize?: boolean;
}

/** "today", "yesterday", "12 days ago", or "in 3 days", for a distance in calendar days. */
export const formatDaysAgo = (days: number, options: DaysAgoOptions = {}): string => {
  const label = RELATIVE_DAYS.format(-days, "day");

  return options.capitalize ? capitalizeFirst(label) : label;
};

/**
 * Heading for a day in a grouped history: "Today", "Yesterday", the weekday within the past
 * week, then "Mar 4", with the year added once it is no longer the current one.
 */
export const formatDayLabel = (value: DateInput, now: DateInput = Date.now()): string => {
  const date = toValidDate(value);

  if (!date) return "";

  const days = calendarDaysBetween(date, now);

  if (days === 0 || days === 1) return formatDaysAgo(days, { capitalize: true });
  if (days > 1 && days < 7) return formatWith({ weekday: "long" }, date);
  if (date.getFullYear() === new Date(now).getFullYear()) return formatDayMonth(date);

  return formatCalendarDate(date);
};

// "yyyy-MM-dd" in UTC, the wire format of the backend's DateOnly day buckets.
export const utcDayKey = (date: Date | number): string => new Date(date).toISOString().slice(0, 10);

// "yyyy-MM" in UTC.
export const utcMonthKey = (date: Date | number): string =>
  new Date(date).toISOString().slice(0, 7);

// "yyyy-MM-dd" on the viewer's local calendar, for days the user picked or sees as "today".
export const localDayKey = (date: Date): string =>
  `${String(date.getFullYear()).padStart(4, "0")}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
