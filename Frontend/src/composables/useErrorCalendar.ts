import { computed, type Ref } from "vue";

import type { ErrorAggregate } from "@/api/monitoring";

import { OperationalEventSeverity, ServiceType } from "@/enums";
import { localDayKey } from "@/utils/date-formatters";

export interface CalendarBreakdownEntry {
  service: ServiceType;
  severity: OperationalEventSeverity;
  count: number;
}

export interface ErrorCalendarDay {
  date: Date | null;
  dayOfYear: number;
  weekIndex: number;
  dayOfWeek: number;
  count: number;
  // True when any Failure is present among the day's counted events — drives
  // the red vs amber hue family in the heatmap.
  hasFailure: boolean;
  // Most severe code present (Failure=0 is the worst); null on clean days.
  worstSeverity: OperationalEventSeverity | null;
  entries: CalendarBreakdownEntry[];
  isPadding: boolean;
  intensity: 0 | 1 | 2 | 3 | 4;
}

const isLeapYear = (year: number): boolean =>
  (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;

const computeIntensity = (count: number, max: number): 0 | 1 | 2 | 3 | 4 => {
  if (count === 0 || max === 0) return 0;
  const ratio = count / max;
  if (ratio <= 0.25) return 1;
  if (ratio <= 0.5) return 2;
  if (ratio <= 0.75) return 3;
  return 4;
};

const MONTH_NAMES = [
  "Jan",
  "Feb",
  "Mar",
  "Apr",
  "May",
  "Jun",
  "Jul",
  "Aug",
  "Sep",
  "Oct",
  "Nov",
  "Dec",
];

export interface ErrorCalendarMonthBlock {
  label: string;
  month: number;
  weeks: ErrorCalendarDay[][];
}

const isInMonth = (day: ErrorCalendarDay, month: number): boolean =>
  !day.isPadding && day.date?.getMonth() === month;

const toPadding = (day: ErrorCalendarDay): ErrorCalendarDay => ({
  ...day,
  count: 0,
  date: null,
  dayOfYear: -1,
  entries: [],
  hasFailure: false,
  intensity: 0,
  isPadding: true,
  worstSeverity: null,
});

const severityWorstFirst = (a: OperationalEventSeverity, b: OperationalEventSeverity): number =>
  a - b;

export const useErrorCalendar = (
  data: Ref<ErrorAggregate[] | undefined>,
  selectedYear: Ref<number>,
  selectedService: Ref<ServiceType | null>,
) => {
  const byDay = computed(() => {
    const map = new Map<string, CalendarBreakdownEntry[]>();
    for (const aggregate of data.value ?? []) {
      if (!aggregate.count) continue;
      const list = map.get(aggregate.day) ?? [];
      list.push({
        service: aggregate.serviceType,
        severity: aggregate.severity,
        count: aggregate.count,
      });
      map.set(aggregate.day, list);
    }
    return map;
  });

  const allSlots = computed<ErrorCalendarDay[]>(() => {
    const year = selectedYear.value;
    const service = selectedService.value;
    const totalDays = isLeapYear(year) ? 366 : 365;

    let maxCount = 0;
    for (let d = 1; d <= totalDays; d++) {
      const matching = (byDay.value.get(localDayKey(new Date(year, 0, d))) ?? []).filter(
        (entry) => service === null || entry.service === service,
      );
      const count = matching.reduce((sum, entry) => sum + entry.count, 0);
      if (count > maxCount) maxCount = count;
    }

    // Monday-anchored offset (Mon = 0, Sun = 6)
    const jan1 = new Date(year, 0, 1);
    const startOffset = (jan1.getDay() + 6) % 7;

    const slots: ErrorCalendarDay[] = [];

    for (let i = 0; i < startOffset; i++) {
      slots.push({
        date: null,
        dayOfYear: -1,
        weekIndex: 0,
        dayOfWeek: i,
        count: 0,
        hasFailure: false,
        worstSeverity: null,
        entries: [],
        isPadding: true,
        intensity: 0,
      });
    }

    for (let d = 1; d <= totalDays; d++) {
      const totalIndex = startOffset + d - 1;
      const weekIndex = Math.floor(totalIndex / 7);
      const dayOfWeek = totalIndex % 7;
      const date = new Date(year, 0, d);

      const allEntries = byDay.value.get(localDayKey(date)) ?? [];
      const entries = allEntries
        .filter((entry) => service === null || entry.service === service)
        .sort((a, b) => b.count - a.count);
      const count = entries.reduce((sum, entry) => sum + entry.count, 0);
      const severities = entries.map((entry) => entry.severity);

      slots.push({
        date,
        dayOfYear: d,
        weekIndex,
        dayOfWeek,
        count,
        hasFailure: severities.includes(OperationalEventSeverity.Failure),
        worstSeverity: severities.length ? severities.reduce(severityWorstFirst) : null,
        entries,
        isPadding: false,
        intensity: computeIntensity(count, maxCount),
      });
    }

    while (slots.length % 7 !== 0) {
      slots.push({
        date: null,
        dayOfYear: -1,
        weekIndex: Math.floor(slots.length / 7),
        dayOfWeek: slots.length % 7,
        count: 0,
        hasFailure: false,
        worstSeverity: null,
        entries: [],
        isPadding: true,
        intensity: 0,
      });
    }

    return slots;
  });

  const weeks = computed<ErrorCalendarDay[][]>(() => {
    const result: ErrorCalendarDay[][] = [];
    for (let i = 0; i < allSlots.value.length; i += 7) {
      result.push(allSlots.value.slice(i, i + 7));
    }
    return result;
  });

  // Weeks straddling two months appear in both blocks, with the other month's days blanked out.
  const monthBlocks = computed<ErrorCalendarMonthBlock[]>(() =>
    MONTH_NAMES.map((label, month) => ({
      label,
      month,
      weeks: weeks.value
        .filter((week) => week.some((day) => isInMonth(day, month)))
        .map((week) => week.map((day) => (isInMonth(day, month) ? day : toPadding(day)))),
    })),
  );

  const totalFiltered = computed<number>(() =>
    allSlots.value.reduce((sum, day) => sum + (day.isPadding ? 0 : day.count), 0),
  );

  return { weeks, monthBlocks, totalFiltered };
};
