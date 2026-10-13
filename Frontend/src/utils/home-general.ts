import { HomeCountdownStyle, HomeDailyCoverStyle, HomeFocusStyle } from "@/enums/home-widget";

export const HOME_COUNTDOWN_STYLES = [
  {
    value: HomeCountdownStyle.BigNumber,
    label: "Big number",
    description: "The days until your date, with Today on the day itself.",
  },
  {
    value: HomeCountdownStyle.Ring,
    label: "Ring",
    description: "A ring fills over the final thirty days.",
  },
  {
    value: HomeCountdownStyle.Flip,
    label: "Flip cards",
    description: "Calendar days in compact cards; a precise countdown when there is room.",
  },
  {
    value: HomeCountdownStyle.DayDots,
    label: "Day dots",
    description: "Thirty dots mark the final run-up to your date.",
  },
];

export const HOME_FOCUS_STYLES = [
  {
    value: HomeFocusStyle.Ring,
    label: "Ring",
    description: "A ring drains as your focus time passes.",
  },
  {
    value: HomeFocusStyle.Digital,
    label: "Digital",
    description: "A clear clock, progress bar and session dots.",
  },
  {
    value: HomeFocusStyle.Cycle,
    label: "Cycle",
    description: "Your focus sessions and breaks as a proportional timeline.",
  },
];

export const HOME_DAILY_COVER_STYLES = [
  {
    value: HomeDailyCoverStyle.Riso,
    label: "Riso",
    description: "Layered inks, halftone dots and a little grain.",
  },
  {
    value: HomeDailyCoverStyle.Bauhaus,
    label: "Bauhaus",
    description: "A grid of circles, wedges and quarter circles.",
  },
  {
    value: HomeDailyCoverStyle.Horizon,
    label: "Horizon",
    description: "A sun above softly layered ridgelines.",
  },
  {
    value: HomeDailyCoverStyle.DatePoster,
    label: "Date poster",
    description: "Daily art above a clear calendar band.",
  },
];

export const homeLocalDay = (date: Date) =>
  `${String(date.getFullYear()).padStart(4, "0")}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;

export const homeCalendarDate = (value: string | null): Date | null => {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/u.test(value)) return null;

  const [year, month, day] = value.split("-").map(Number) as [number, number, number];
  if (year < 1) return null;

  const date = new Date(0);
  date.setHours(0, 0, 0, 0);
  date.setFullYear(year, month - 1, day);

  return homeLocalDay(date) === value ? date : null;
};

const calendarDayNumber = (date: Date) => {
  const utc = new Date(0);
  utc.setUTCFullYear(date.getFullYear(), date.getMonth(), date.getDate());
  utc.setUTCHours(0, 0, 0, 0);

  return utc.getTime() / 86_400_000;
};

export const homeCountdown = (target: string | Date | null, now: Date, dateLabel?: string) => {
  const date = target instanceof Date ? target : homeCalendarDate(target);
  if (!date) return null;

  const days = calendarDayNumber(date) - calendarDayNumber(now);
  const minutes = Math.max(0, Math.floor((date.getTime() - now.getTime()) / 60_000));
  let unit = Math.abs(days) === 1 ? "day" : "days";

  if (days < 0) unit += " ago";
  if (days === 0) unit = "";

  return {
    days,
    value: days === 0 ? "Today" : String(Math.abs(days)),
    unit,
    date:
      dateLabel ??
      date.toLocaleDateString(undefined, {
        weekday: "long",
        month: "long",
        day: "numeric",
        year: "numeric",
      }),
    progress: Math.min(1, Math.max(0, (30 - days) / 30)),
    fullDays: Math.floor(minutes / 1440),
    hours: Math.floor(minutes / 60) % 24,
    minutes: minutes % 60,
  };
};
