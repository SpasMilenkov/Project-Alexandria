import { computed, type Ref } from "vue";

import { OperationType, type ActivityStatisticsOverview } from "@/api/activity";

export interface CalendarDay {
  date: Date | null;
  dayOfYear: number;
  weekIndex: number;
  dayOfWeek: number;
  count: number;
  breakdown: Partial<Record<OperationType, number>>;
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

// C# serializes enum keys as their string names ("Create", "Read" …).
// Normalize them to numeric OperationType values so all lookups work correctly.
// oxlint-disable-next-line sort-keys
const OP_NAME_TO_VALUE: Record<string, OperationType> = {
  Read: OperationType.Read,
  Create: OperationType.Create,
  Update: OperationType.Update,
  Delete: OperationType.Delete,
  Login: OperationType.Login,
  Logout: OperationType.Logout,
};

const normalizeBreakdown = (
  raw: Partial<Record<OperationType, number>>,
): Partial<Record<OperationType, number>> => {
  const result: Partial<Record<OperationType, number>> = {};
  for (const [k, v] of Object.entries(raw)) {
    if (!v) continue;
    // Key may already be numeric (future-proof) or a string name from C#
    const op = isNaN(Number(k)) ? OP_NAME_TO_VALUE[k] : (Number(k) as OperationType);
    if (op !== undefined) result[op] = v;
  }
  return result;
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

export const useActivityCalendar = (
  data: Ref<ActivityStatisticsOverview | undefined>,
  selectedYear: Ref<number>,
  selectedOperation: Ref<OperationType | null>,
) => {
  const allSlots = computed<CalendarDay[]>(() => {
    const year = selectedYear.value;
    const op = selectedOperation.value;
    const totalDays = isLeapYear(year) ? 366 : 365;

    // Pre-normalize all breakdowns once per recompute
    const normalizedDays: Record<
      number,
      { totalOperations: number; countPerOperation: Partial<Record<OperationType, number>> }
    > = data.value
      ? Object.fromEntries(
          Object.entries(data.value.activityPerDay).map(([k, summary]) => [
            k,
            {
              totalOperations: summary.totalOperations,
              countPerOperation: normalizeBreakdown(summary.countPerOperation),
            },
          ]),
        )
      : {};

    // Compute max count for intensity scaling relative to the active filter
    let maxCount = 0;
    for (let d = 1; d <= totalDays; d++) {
      const summary = normalizedDays[d];
      if (!summary) continue;
      const count =
        op === null ? Number(summary.totalOperations) : (summary.countPerOperation[op] ?? 0);
      if (count > maxCount) maxCount = count;
    }

    // Monday-anchored offset (Mon = 0, Sun = 6)
    const jan1 = new Date(year, 0, 1);
    const startOffset = (jan1.getDay() + 6) % 7;

    const slots: CalendarDay[] = [];

    // Leading padding
    for (let i = 0; i < startOffset; i++) {
      slots.push({
        date: null,
        dayOfYear: -1,
        weekIndex: 0,
        dayOfWeek: i,
        count: 0,
        breakdown: {},
        isPadding: true,
        intensity: 0,
      });
    }

    // Real days
    for (let d = 1; d <= totalDays; d++) {
      const totalIndex = startOffset + d - 1;
      const weekIndex = Math.floor(totalIndex / 7);
      const dayOfWeek = totalIndex % 7;
      const date = new Date(year, 0, d);
      const summary = normalizedDays[d];
      const count = summary
        ? op === null
          ? Number(summary.totalOperations)
          : (summary.countPerOperation[op] ?? 0)
        : 0;

      slots.push({
        date,
        dayOfYear: d,
        weekIndex,
        dayOfWeek,
        count,
        breakdown: summary?.countPerOperation ?? {},
        isPadding: false,
        intensity: computeIntensity(count, maxCount),
      });
    }

    // Trailing padding
    while (slots.length % 7 !== 0) {
      slots.push({
        date: null,
        dayOfYear: -1,
        weekIndex: Math.floor(slots.length / 7),
        dayOfWeek: slots.length % 7,
        count: 0,
        breakdown: {},
        isPadding: true,
        intensity: 0,
      });
    }

    return slots;
  });

  const weeks = computed<CalendarDay[][]>(() => {
    const result: CalendarDay[][] = [];
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
      if (month !== lastMonth) {
        if (map[day.weekIndex] === undefined) {
          map[day.weekIndex] = MONTH_NAMES[month];
        }
        lastMonth = month;
      }
    }
    return map;
  });

  const totalFiltered = computed<number>(() => {
    if (!data.value) return 0;
    const op = selectedOperation.value;
    if (op === null) return data.value.totalActivity;
    return Object.values(data.value.activityPerDay).reduce((sum, summary) => {
      const normalized = normalizeBreakdown(summary.countPerOperation);
      return sum + (normalized[op] ?? 0);
    }, 0);
  });

  return { weeks, monthLabelByWeek, totalFiltered };
};
