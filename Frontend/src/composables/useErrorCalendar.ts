import { computed, type Ref } from "vue";

import type { ErrorAggregate } from "@/api/monitoring";

import { OperationalEventSeverity, ServiceType } from "@/enums";

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

// Matches the backend's DateOnly wire format ("yyyy-MM-dd") so aggregates can
// be looked up without any timezone-sensitive Date parsing.
export const toDayKey = (date: Date): string => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
};

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
      const matching = (byDay.value.get(toDayKey(new Date(year, 0, d))) ?? []).filter(
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

      const allEntries = byDay.value.get(toDayKey(date)) ?? [];
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
        isPadding: false,
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

  const monthLabelByWeek = computed<Record<number, string>>(() => {
    const map: Record<number, string> = {};
    let lastMonth = -1;
    for (const day of allSlots.value) {
      if (day.isPadding || !day.date) continue;
      const month = day.date.getMonth();
      if (month !== lastMonth && map[day.weekIndex] === undefined) {
        map[day.weekIndex] = MONTH_NAMES[month];
      }
      lastMonth = month;
    }
    return map;
  });

  const totalFiltered = computed<number>(() =>
    allSlots.value.reduce((sum, day) => sum + (day.isPadding ? 0 : day.count), 0),
  );

  return { weeks, monthLabelByWeek, totalFiltered };
};
