import type { OverviewSummaryHeaderDtoResponse, WrappedPeriodPoint } from "@/api/stats";

import { MIN_WRAPPED_YEAR } from "@/features/wrapped/display";
import { formatCalendarDate, utcDayKey } from "@/utils/date-formatters";

export const annualRecapHeaders = (
  headers: OverviewSummaryHeaderDtoResponse[],
  currentYear: number,
) =>
  headers
    .filter((header) => {
      const start = new Date(header.periodStart);
      const year = start.getUTCFullYear();

      return (
        year >= MIN_WRAPPED_YEAR &&
        year < currentYear &&
        start.getTime() === Date.UTC(year, 0, 1) &&
        Date.parse(header.periodEnd) === Date.UTC(year + 1, 0, 1)
      );
    })
    .sort((a, b) => b.periodStart.localeCompare(a.periodStart));

export const annualRecapMonths = (
  series: WrappedPeriodPoint[],
  year: number,
): WrappedPeriodPoint[] => {
  const byMonth = new Map(
    series
      .filter((point) => new Date(point.date).getUTCFullYear() === year)
      .map((point) => [new Date(point.date).getUTCMonth(), point]),
  );

  return Array.from({ length: 12 }, (_, month) => {
    const point = byMonth.get(month);

    return {
      date: utcDayKey(Date.UTC(year, month, 1)),
      seconds: point?.seconds ?? 0,
      plays: point?.plays ?? 0,
    };
  });
};

export const recapTimestamp = (iso: string, current: boolean): string => {
  const date = new Date(iso);

  if (!Number.isFinite(date.getTime())) return "";

  const label = formatCalendarDate(date, { timeZone: "UTC" });

  if (current) return `Updated ${label}`;

  return `Generated ${label}`;
};
