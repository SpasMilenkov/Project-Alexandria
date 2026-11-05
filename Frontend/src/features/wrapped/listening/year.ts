import type { TimelineDayPoint, TimelineMonthPoint } from "@/api/stats";

import { utcDayKey, utcMonthKey } from "@/utils/date-formatters";

const DAY_MS = 86_400_000;

export interface HeatmapCell {
  date: string;
  seconds: number;
  plays: number;
  future: boolean;
}

export interface HeatmapWeek {
  key: string;
  month: string | null;
  cells: (HeatmapCell | null)[];
}

export interface YearMonthBar {
  key: string;
  label: string;
  initial: string;
  seconds: number;
  plays: number;
  height: number;
  current: boolean;
  future: boolean;
}

export const startOfUtcDay = (now: Date): number =>
  Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate());

export const yearBounds = (year: number) => ({
  from: new Date(Date.UTC(year, 0, 1)).toISOString(),
  to: new Date(Date.UTC(year + 1, 0, 1)).toISOString(),
});

// Weeks run Monday to Sunday so the columns line up with the rest of the app's calendars.
export const heatmapWeeks = (days: TimelineDayPoint[], year: number, today: number) => {
  const lookup = new Map(days.map((day) => [day.date.slice(0, 10), day]));
  const start = Date.UTC(year, 0, 1);
  const end = Date.UTC(year + 1, 0, 1);
  const leading = (new Date(start).getUTCDay() + 6) % 7;
  const monthFormat = new Intl.DateTimeFormat(undefined, { month: "short", timeZone: "UTC" });
  const weeks: HeatmapWeek[] = [];

  for (let cursor = start - leading * DAY_MS; cursor < end; cursor += 7 * DAY_MS) {
    let month: string | null = null;

    const cells = Array.from({ length: 7 }, (_, offset): HeatmapCell | null => {
      const time = cursor + offset * DAY_MS;

      if (time < start || time >= end) return null;

      const stamp = new Date(time);
      const date = utcDayKey(stamp);
      const point = lookup.get(date);

      if (stamp.getUTCDate() === 1) month = monthFormat.format(stamp);

      return {
        date,
        seconds: point?.seconds ?? 0,
        plays: point?.plays ?? 0,
        future: time > today,
      };
    });

    weeks.push({ key: utcDayKey(cursor), month, cells });
  }

  return weeks;
};

export const heatLevel = (seconds: number, max: number): number => {
  if (seconds <= 0 || max <= 0) return 0;

  return Math.min(4, Math.max(1, Math.ceil(Math.sqrt(seconds / max) * 4)));
};

export const longestStreak = (days: TimelineDayPoint[]): number => {
  const stamps = days
    .filter((day) => day.seconds > 0)
    .map((day) => Date.parse(day.date.slice(0, 10)))
    .filter(Number.isFinite);

  const active = [...new Set(stamps)].sort((a, b) => a - b);

  let best = 0;
  let run = 0;
  let previous = Number.NaN;

  for (const time of active) {
    run = time - previous === DAY_MS ? run + 1 : 1;
    best = Math.max(best, run);
    previous = time;
  }

  return best;
};

export const busiestDay = (days: TimelineDayPoint[]): TimelineDayPoint | null => {
  let top: TimelineDayPoint | null = null;

  for (const day of days) {
    if (day.seconds > (top?.seconds ?? 0)) top = day;
  }

  return top;
};

export const yearMonthBars = (
  months: TimelineMonthPoint[],
  year: number,
  now: Date,
): YearMonthBar[] => {
  const lookup = new Map(months.map((month) => [month.date.slice(0, 7), month]));
  const max = Math.max(0, ...months.map((month) => month.seconds));
  const longFormat = new Intl.DateTimeFormat(undefined, { month: "long", timeZone: "UTC" });
  const shortFormat = new Intl.DateTimeFormat(undefined, { month: "narrow", timeZone: "UTC" });
  const currentKey = utcMonthKey(now);

  return Array.from({ length: 12 }, (_, index) => {
    const stamp = new Date(Date.UTC(year, index, 1));
    const key = utcMonthKey(stamp);
    const point = lookup.get(key);
    const seconds = point?.seconds ?? 0;

    return {
      key,
      label: longFormat.format(stamp),
      initial: shortFormat.format(stamp),
      seconds,
      plays: point?.plays ?? 0,
      height: seconds > 0 && max > 0 ? Math.max(4, (seconds / max) * 100) : 0,
      current: key === currentKey,
      future: key > currentKey,
    };
  });
};
