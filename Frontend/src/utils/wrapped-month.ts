import { HomeRecapPeriod } from "@/enums/home-widget";
import { MIN_WRAPPED_YEAR } from "@/utils/wrapped-display.utils";

export const wrappedMonth = (period: HomeRecapPeriod, now = new Date()): string => {
  const offset = period === HomeRecapPeriod.PreviousMonth ? 1 : 0;
  const start = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - offset, 1));

  return start.toISOString().slice(0, 7);
};

export const parseWrappedMonth = (value: unknown, now = new Date()): string | null => {
  if (typeof value !== "string" || !/^\d{4}-(?:0[1-9]|1[0-2])$/u.test(value)) return null;
  if (Number(value.slice(0, 4)) < MIN_WRAPPED_YEAR) return null;
  if (value > wrappedMonth(HomeRecapPeriod.CurrentMonth, now)) return null;

  return value;
};

export const wrappedMonthRange = (month: string, now = new Date()) => {
  const year = Number(month.slice(0, 4));
  const index = Number(month.slice(5, 7)) - 1;
  const from = new Date(Date.UTC(year, index, 1)).toISOString();
  const end = new Date(Date.UTC(year, index + 1, 1));
  const isFinal = end.getTime() <= now.getTime();

  return { from, to: isFinal ? end.toISOString() : undefined, isFinal };
};

export const wrappedMonthLabel = (month: string): string =>
  new Intl.DateTimeFormat(undefined, { month: "long", year: "numeric", timeZone: "UTC" }).format(
    new Date(`${month}-01T00:00:00Z`),
  );

export const wrappedMonthLocation = (month: string) => ({
  name: "wrapped",
  query: { month },
});
