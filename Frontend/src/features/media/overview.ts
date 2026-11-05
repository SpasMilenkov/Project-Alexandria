import type { MediaFileDto } from "@/api/streaming";

import { utcDayKey } from "@/utils/date-formatters";

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

export const daysAwayLabel = (lastPlayedAt: string, now = Date.now()): string => {
  const days = Math.max(1, Math.floor((now - new Date(lastPlayedAt).getTime()) / DAY_MS));

  return `${days} ${days === 1 ? "day" : "days"} ago`;
};

export const formatListeningTime = (seconds: number): string => {
  const totalMinutes = Math.floor(seconds / 60);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  if (hours === 0) return `${minutes}m`;
  if (minutes === 0) return `${hours}h`;

  return `${hours}h ${minutes}m`;
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

const startOfLocalDay = (time: number) => {
  const date = new Date(time);

  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
};

const WEEKDAY_LONG = new Intl.DateTimeFormat(undefined, { weekday: "long" });
const SHORT_DATE = new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric" });

export const historyDayLabel = (iso: string, now = Date.now()): string => {
  const time = new Date(iso).getTime();
  const daysAgo = Math.round((startOfLocalDay(now) - startOfLocalDay(time)) / DAY_MS);

  if (daysAgo <= 0) return "Today";
  if (daysAgo === 1) return "Yesterday";
  if (daysAgo < 7) return WEEKDAY_LONG.format(time);

  return SHORT_DATE.format(time);
};

export const groupByDay = <T extends { lastAccessedAt: string }>(
  items: T[],
  now = Date.now(),
): HistoryDayGroup<T>[] => {
  const groups: HistoryDayGroup<T>[] = [];

  for (const item of items) {
    const label = historyDayLabel(item.lastAccessedAt, now);
    const last = groups.at(-1);

    if (last?.label === label) {
      last.items.push(item);
    } else {
      groups.push({ label, items: [item] });
    }
  }

  return groups;
};
