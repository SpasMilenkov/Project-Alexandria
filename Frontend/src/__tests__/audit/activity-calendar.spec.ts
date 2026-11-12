import { describe, expect, it } from "vitest";
import { ref } from "vue";

import type { ActivityStatisticsOverview } from "@/api/activity";

import { calendarDayKey, useActivityCalendar } from "@/composables/useActivityCalendar";
import { OperationType } from "@/enums";

const overview = (days: Record<string, number>): ActivityStatisticsOverview => ({
  activityPerDay: Object.fromEntries(
    Object.entries(days).map(([key, count]) => [
      key,
      { countPerOperation: { Create: count }, totalOperations: count },
    ]),
  ) as never,
  totalActivity: Object.values(days).reduce((sum, count) => sum + count, 0),
});

const findDay = (weeks: { date: Date | null; count: number }[][], month: number, day: number) =>
  weeks.flat().find((cell) => cell.date?.getMonth() === month && cell.date.getDate() === day);

describe("useActivityCalendar", () => {
  it("places days by their date key, not by day of year", () => {
    const data = ref(overview({ "2026-03-01": 4, "2025-03-01": 9 }));
    const { weeks } = useActivityCalendar(data, ref(2026), ref<OperationType | null>(null));

    expect(findDay(weeks.value, 2, 1)?.count).toBe(4);
    expect(findDay(weeks.value, 1, 28)?.count).toBe(0);
  });

  it("reads breakdowns keyed by operation name", () => {
    const data = ref(overview({ "2026-01-02": 3 }));
    const { weeks } = useActivityCalendar(
      data,
      ref(2026),
      ref<OperationType | null>(OperationType.Create),
    );

    expect(findDay(weeks.value, 0, 2)?.count).toBe(3);
  });

  it("builds zero-padded keys", () => {
    expect(calendarDayKey(2026, 0, 5)).toBe("2026-01-05");
  });
});
