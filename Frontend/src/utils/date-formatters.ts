export type DateInput = string | number | Date;

const SECOND_MS = 1000;
const MINUTE_MS = 60 * SECOND_MS;
const HOUR_MS = 60 * MINUTE_MS;
const DAY_MS = 24 * HOUR_MS;

// Averaged so month and year counts stay right across short months and leap years.
const MONTH_DAYS = 30.4375;
const YEAR_DAYS = 365.25;

const pad = (value: number) => String(value).padStart(2, "0");

/**
 * Player clock time: "3:07", or "1:02:03" past an hour, with hours counting on past a day.
 * Players report NaN or a negative value before media loads, which reads as "0:00".
 */
export const formatPlaybackTime = (seconds: number): string => {
  if (!Number.isFinite(seconds) || seconds < 0) return "0:00";

  const whole = Math.floor(seconds);
  const hours = Math.floor(whole / 3600);
  const minutes = Math.floor((whole % 3600) / 60);
  const rest = whole % 60;

  if (hours > 0) return `${hours}:${pad(minutes)}:${pad(rest)}`;

  return `${minutes}:${pad(rest)}`;
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
  /** False where the surrounding view already names the year ("Wednesday, March 4"). */
  showYear?: boolean;
}

export interface MonthFormatOptions extends DateFormatOptions {
  /** "long" reads "March", "short" reads "Mar", and "narrow" reads "M" for tight chart axes. */
  style?: "long" | "short" | "narrow";
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

export type ChartBucket = "hour" | "day";

/**
 * Axis and tooltip label for a time-series bucket. Hourly buckets name their day too, because a
 * 24h window crosses midnight and a week of hours would repeat the same times. Daily buckets
 * start at UTC midnight on the backend, so they are read in UTC to keep their own date.
 */
export const formatBucketLabel = (value: DateInput, bucket: ChartBucket): string => {
  if (bucket === "day") return formatDayMonth(value, { timeZone: "UTC" });

  return formatWith({ ...dayMonthOptions({}), ...clockOptions({}) }, value);
};

/** "Wednesday, March 4, 2026", for headings that name a single day. */
export const formatFullDate = (value: DateInput, options: FullDateOptions = {}): string => {
  const style = options.compact ? "short" : "long";

  return formatWith(
    {
      day: "numeric",
      month: style,
      timeZone: options.timeZone,
      weekday: style,
      year: options.showYear === false ? undefined : "numeric",
    },
    value,
  );
};

/** The month alone: "March", "Mar", or "M". */
export const formatMonth = (value: DateInput, options: MonthFormatOptions = {}): string =>
  formatWith({ month: options.style ?? "long", timeZone: options.timeZone }, value);

/** "March 2026", for a month heading or a month-long period. */
export const formatMonthYear = (value: DateInput, options: DateFormatOptions = {}): string =>
  formatWith({ month: "long", timeZone: options.timeZone, year: "numeric" }, value);

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
const largestElapsedUnit = (elapsedMs: number): ElapsedUnit => {
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

  const { unit, value: count } = largestElapsedUnit(elapsedMs);

  return RELATIVE_FORMATS[style].format(offsetMs < 0 ? -count : count, unit);
};

type DurationUnit = "day" | "hour" | "minute" | "second" | "millisecond";

export type DurationStyle = "narrow" | "short";

const unitFormatters = new Map<string, Intl.NumberFormat>();

const formatUnit = (value: number, unit: DurationUnit, style: DurationStyle): string => {
  const key = `${unit}|${style}`;
  let formatter = unitFormatters.get(key);

  if (!formatter) {
    formatter = new Intl.NumberFormat(undefined, {
      maximumFractionDigits: 1,
      style: "unit",
      unit,
      unitDisplay: style,
    });
    unitFormatters.set(key, formatter);
  }

  return formatter.format(value);
};

const UNIT_SECONDS: readonly (readonly [DurationUnit, number])[] = [
  ["day", 86_400],
  ["hour", 3600],
  ["minute", 60],
  ["second", 1],
];

export interface DurationOptions {
  /** "narrow" reads "1h 5m" for dense rows and stats; "short" reads "1 hr 5 min" in sentences. */
  style?: DurationStyle;
  /** Units shown, largest first. The rest are dropped rather than rounded, so 59m 50s never reads 1h. */
  maxUnits?: number;
  /** "hour" keeps long totals such as listening time in hours ("122h") instead of days. */
  largestUnit?: "day" | "hour";
}

/**
 * A span of time for people to read: "850ms", "12.3s", "4m 12s", "1h 5m", "2d 3h". Not the
 * player clock, which is formatPlaybackTime.
 */
export const formatDuration = (seconds: number, options: DurationOptions = {}): string => {
  const { largestUnit = "day", maxUnits = 2, style = "narrow" } = options;
  const total = Number.isFinite(seconds) && seconds > 0 ? seconds : 0;

  if (total > 0 && total < 1) return formatUnit(Math.round(total * 1000), "millisecond", style);
  if (total < 60) return formatUnit(Math.floor(total * 10) / 10, "second", style);

  const units = UNIT_SECONDS.filter(([unit]) => largestUnit === "day" || unit !== "day");
  const first = units.findIndex(([, size]) => total >= size);
  const parts: string[] = [];
  let rest = Math.floor(total);

  for (const [unit, size] of units.slice(first, first + maxUnits)) {
    const value = Math.floor(rest / size);
    rest -= value * size;

    if (value > 0) parts.push(formatUnit(value, unit, style));
  }

  return parts.join(" ");
};

/**
 * How long something has lasted since `value`, as a bare span for "for 12m" phrasing. Stops at
 * days because narrow minutes and months would both read "m".
 */
export const formatAgeShort = (value: DateInput, now = Date.now()): string => {
  const elapsedMs = Math.max(0, now - new Date(value).getTime());

  if (Number.isNaN(elapsedMs)) return "";
  if (elapsedMs < MINUTE_MS)
    return formatUnit(Math.floor(elapsedMs / SECOND_MS), "second", "narrow");
  if (elapsedMs < HOUR_MS) return formatUnit(Math.floor(elapsedMs / MINUTE_MS), "minute", "narrow");
  if (elapsedMs < DAY_MS) return formatUnit(Math.floor(elapsedMs / HOUR_MS), "hour", "narrow");

  return formatUnit(Math.floor(elapsedMs / DAY_MS), "day", "narrow");
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

const capitalizeFirst = (text: string): string =>
  text.charAt(0).toLocaleUpperCase() + text.slice(1);

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
  `${String(date.getFullYear()).padStart(4, "0")}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
