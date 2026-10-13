import type { WrappedPeriodPoint } from "@/api/stats";

export interface WrappedDayBar extends WrappedPeriodPoint {
  day: number;
  future: boolean;
  height: number;
}

export const wrappedDailyLandscape = (
  month: string,
  through: string,
  series: WrappedPeriodPoint[],
): WrappedDayBar[] => {
  if (!/^\d{4}-(?:0[1-9]|1[0-2])$/u.test(month)) return [];

  const start = new Date(`${month}-01T00:00:00Z`);
  const cutoff = Date.parse(through);

  if (!Number.isFinite(start.getTime()) || !Number.isFinite(cutoff)) return [];

  const year = start.getUTCFullYear();
  const monthIndex = start.getUTCMonth();
  const count = new Date(Date.UTC(year, monthIndex + 1, 0)).getUTCDate();
  const byDate = new Map(series.map((point) => [point.date, point]));

  const days = Array.from({ length: count }, (_, index) => {
    const day = index + 1;
    const date = `${month}-${String(day).padStart(2, "0")}`;
    const future = Date.UTC(year, monthIndex, day) >= cutoff;
    const point = byDate.get(date);
    let seconds = 0;
    let plays = 0;

    if (!future && point) {
      if (Number.isFinite(point.seconds)) seconds = Math.max(0, point.seconds);
      if (Number.isFinite(point.plays)) plays = Math.max(0, point.plays);
    }

    return { date, day, seconds, plays, future, recorded: !future && Boolean(point) };
  });

  if (!days.some((day) => day.recorded)) return [];

  const maximum = Math.max(1, ...days.map((day) => day.seconds));

  return days.map(({ recorded: _recorded, ...day }) => ({
    ...day,
    height: (day.seconds / maximum) * 100,
  }));
};
