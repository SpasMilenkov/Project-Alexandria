import type { MediaFileDto } from "@/api/streaming";

import { formatDayLabel, utcDayKey } from "@/utils/date-formatters";

const DAY_MS = 86_400_000;
const WEEK_MS = 7 * DAY_MS;

// Derived from the loaded page only; a full page of new files means there may be more.
export const addedThisWeekLabel = (
  items: MediaFileDto[],
  totalCount: number,
  now = Date.now(),
): string | null => {
  const added = items.filter(
    (file) => file.createdAt && now - new Date(file.createdAt).getTime() < WEEK_MS,
  ).length;

  if (added === 0) return null;

  const more = added === items.length && totalCount > items.length;

  return `${added}${more ? "+" : ""} added this week`;
};

export interface WeekDay {
  date: string;
  label: string;
  longLabel: string;
  seconds: number;
  plays: number;
  isToday: boolean;
}

interface TimelineDayInput {
  date: string;
  seconds: number;
  plays?: number;
}

const WEEKDAY = new Intl.DateTimeFormat(undefined, { weekday: "narrow", timeZone: "UTC" });

const LONG_DAY = new Intl.DateTimeFormat(undefined, {
  day: "numeric",
  month: "short",
  timeZone: "UTC",
  weekday: "short",
});

// Timeline days are UTC buckets and only include days with listening, so the gaps are filled here.
export const buildWeekDays = (from: string, days: TimelineDayInput[]): WeekDay[] => {
  const start = new Date(from);
  const byDate = new Map(days.map((day) => [day.date, day]));

  return Array.from({ length: 7 }, (_, index) => {
    const date = new Date(
      Date.UTC(start.getUTCFullYear(), start.getUTCMonth(), start.getUTCDate() + index),
    );
    const key = utcDayKey(date);
    const day = byDate.get(key);

    return {
      date: key,
      label: WEEKDAY.format(date),
      longLabel: LONG_DAY.format(date),
      seconds: day?.seconds ?? 0,
      plays: day?.plays ?? 0,
      isToday: index === 6,
    };
  });
};

export interface HistoryDayGroup<T> {
  label: string;
  items: T[];
}

export const groupByDay = <T extends { lastAccessedAt: string }>(
  items: T[],
  now = Date.now(),
): HistoryDayGroup<T>[] => {
  const groups: HistoryDayGroup<T>[] = [];

  for (const item of items) {
    const label = formatDayLabel(item.lastAccessedAt, now);
    const last = groups.at(-1);

    if (last?.label === label) {
      last.items.push(item);
    } else {
      groups.push({ label, items: [item] });
    }
  }

  return groups;
};
