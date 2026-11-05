import { type Ref, computed, onBeforeUnmount, onMounted, shallowRef, watch } from "vue";

import { HomeClockHourFormat } from "@/enums/home-widget";

const formatters = new Map<string, Intl.DateTimeFormat>();

const clockFormatter = (
  locale: string | undefined,
  timeZone: string | undefined,
  options: Intl.DateTimeFormatOptions,
) => {
  const key = JSON.stringify([locale, timeZone, options]);
  const cached = formatters.get(key);

  if (cached) return cached;

  const formatter = new Intl.DateTimeFormat(locale, { ...options, timeZone });

  if (formatters.size >= 256) formatters.delete(formatters.keys().next().value!);

  formatters.set(key, formatter);

  return formatter;
};

export const homeClockNumbers = (timestamp: number, timeZone: string | undefined) => {
  const parts = clockFormatter("en-US-u-nu-latn", timeZone, {
    hourCycle: "h23",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).formatToParts(timestamp);

  const value = (type: Intl.DateTimeFormatPartTypes) =>
    Number(parts.find((part) => part.type === type)?.value ?? 0);

  return { hour24: value("hour"), minuteNumber: value("minute"), secondNumber: value("second") };
};

export const homeClockGreeting = (hour: number) => {
  if (hour < 5) return "Good night";
  if (hour < 12) return "Good morning";
  if (hour < 18) return "Good afternoon";

  return "Good evening";
};

export const homeClockReadout = (
  timestamp: number,
  timeZone: string | undefined,
  hourFormat = HomeClockHourFormat.DeviceDefault,
  locale?: string,
  showSeconds = false,
) => {
  const options: Intl.DateTimeFormatOptions = { hour: "2-digit", minute: "2-digit" };

  if (showSeconds) options.second = "2-digit";

  if (hourFormat === HomeClockHourFormat.TwelveHour) options.hourCycle = "h12";
  if (hourFormat === HomeClockHourFormat.TwentyFourHour) options.hourCycle = "h23";

  const formatter = clockFormatter(locale, timeZone, options);
  const parts = formatter.formatToParts(timestamp);
  const value = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((part) => part.type === type)?.value ?? "";
  const numbers = homeClockNumbers(timestamp, timeZone);
  let digits = `${value("hour")}:${value("minute")}`;

  if (showSeconds) digits += `:${value("second")}`;

  return {
    ...numbers,
    time: formatter.format(timestamp),
    digits,
    hour: value("hour"),
    minute: value("minute"),
    second: value("second"),
    period: value("dayPeriod"),
    greeting: homeClockGreeting(numbers.hour24),
  };
};

export type HomeClockReadout = ReturnType<typeof homeClockReadout>;

const dates = new Map<string, { date: string; compactDate: string }>();
const dateWindows = new Map<string, string>();

const zonedDayKey = (timestamp: number, timeZone: string | undefined, locale?: string) => {
  const day = clockFormatter("en-US-u-nu-latn", timeZone, {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(timestamp);

  return JSON.stringify([locale, timeZone, day]);
};

export const homeClockDateLabels = (
  timestamp: number,
  timeZone: string | undefined,
  locale?: string,
) => {
  const key = zonedDayKey(timestamp, timeZone, locale);
  const cached = dates.get(key);
  if (cached) return cached;

  const labels = {
    date: clockFormatter(locale, timeZone, {
      weekday: "long",
      month: "long",
      day: "numeric",
    }).format(timestamp),
    compactDate: clockFormatter(locale, timeZone, {
      weekday: "short",
      month: "short",
      day: "numeric",
    }).format(timestamp),
  };
  if (dates.size >= 256) dates.delete(dates.keys().next().value!);
  dates.set(key, labels);

  return labels;
};

export const homeClockDateWindow = (
  timestamp: number,
  timeZone: string | undefined,
  locale?: string,
) => {
  const key = zonedDayKey(timestamp, timeZone, locale);
  const cached = dateWindows.get(key);
  if (cached !== undefined) return cached;

  const label = clockFormatter(locale, timeZone, { weekday: "short", day: "numeric" })
    .format(timestamp)
    .toLocaleUpperCase(locale);
  if (dateWindows.size >= 256) dateWindows.delete(dateWindows.keys().next().value!);
  dateWindows.set(key, label);

  return label;
};

const hourWords = [
  "twelve",
  "one",
  "two",
  "three",
  "four",
  "five",
  "six",
  "seven",
  "eight",
  "nine",
  "ten",
  "eleven",
];

const minuteWords = [
  "",
  "five past",
  "ten past",
  "quarter past",
  "twenty past",
  "twenty-five past",
  "half past",
  "twenty-five to",
  "twenty to",
  "quarter to",
  "ten to",
  "five to",
];

const clockDayPeriod = (hour: number) => {
  if (hour < 5) return "at night";
  if (hour < 12) return "in the morning";
  if (hour < 17) return "in the afternoon";
  if (hour < 21) return "in the evening";

  return "at night";
};

export const homeClockWords = (hour: number, minute: number) => {
  let segment = Math.round(minute / 5);
  let spokenHour = hour % 12;

  if (segment > 6) spokenHour = (spokenHour + 1) % 12;
  if (segment === 12) segment = 0;

  const period = clockDayPeriod(hour);

  if (segment === 0) return { lead: hourWords[spokenHour]!, ending: "o’clock", period };

  return { lead: minuteWords[segment]!, ending: hourWords[spokenHour]!, period };
};

export const homeClockRingProgress = (hour: number, minute: number, second: number) => ({
  hour: (((hour % 12) + minute / 60 + second / 3600) / 12) * 100,
  minute: ((minute + second / 60) / 60) * 100,
  second: (second / 60) * 100,
});

export const homeClockHandAngles = (hour: number, minute: number, second: number) => ({
  hour: ((hour % 12) + minute / 60 + second / 3600) * 30,
  minute: (minute + second / 60) * 6,
  second: second * 6,
});

interface ClockClient {
  smooth: boolean;
  frameTimestamp: Ref<number>;
}

const timestamp = shallowRef(Math.floor(Date.now() / 1000) * 1000);
const reducedMotion = shallowRef(false);
const documentVisible = shallowRef(true);
const clients = new Set<ClockClient>();
let secondTimer: ReturnType<typeof setTimeout> | undefined = undefined;
let frame: number | undefined = undefined;
let mediaQuery: MediaQueryList | undefined = undefined;

const updateTimestamp = () => {
  timestamp.value = Math.floor(Date.now() / 1000) * 1000;
};

const animate = () => {
  const now = Date.now();

  for (const client of clients) {
    if (client.smooth) client.frameTimestamp.value = now;
  }

  frame = requestAnimationFrame(animate);
};

const syncAnimation = () => {
  const active = documentVisible.value && [...clients].some((client) => client.smooth);

  if (active && frame === undefined) frame = requestAnimationFrame(animate);

  if (!active && frame !== undefined) {
    cancelAnimationFrame(frame);
    frame = undefined;
  }
};

const scheduleSecond = () => {
  if (secondTimer !== undefined) clearTimeout(secondTimer);

  secondTimer = undefined;

  if (!clients.size || !documentVisible.value) return;

  secondTimer = setTimeout(
    () => {
      updateTimestamp();
      scheduleSecond();
    },
    1000 - (Date.now() % 1000),
  );
};

const visibilityChanged = () => {
  documentVisible.value = document.visibilityState !== "hidden";
  updateTimestamp();
  scheduleSecond();
  syncAnimation();
};

const motionChanged = () => {
  reducedMotion.value = mediaQuery?.matches ?? false;
};

const startClock = () => {
  documentVisible.value = document.visibilityState !== "hidden";
  mediaQuery = window.matchMedia?.("(prefers-reduced-motion: reduce)");
  motionChanged();
  mediaQuery?.addEventListener("change", motionChanged);
  document.addEventListener("visibilitychange", visibilityChanged);
  updateTimestamp();
  scheduleSecond();
};

const stopClock = () => {
  if (secondTimer !== undefined) clearTimeout(secondTimer);
  if (frame !== undefined) cancelAnimationFrame(frame);

  secondTimer = undefined;
  frame = undefined;
  mediaQuery?.removeEventListener("change", motionChanged);
  mediaQuery = undefined;
  document.removeEventListener("visibilitychange", visibilityChanged);
};

export const useHomeClockTiming = (element: Ref<HTMLElement | null>, smooth: () => boolean) => {
  if (!clients.size) updateTimestamp();

  const inView = shallowRef(true);
  const frameTimestamp = shallowRef(Date.now());
  const motionActive = computed(
    () => inView.value && documentVisible.value && !reducedMotion.value,
  );
  const client: ClockClient = { smooth: false, frameTimestamp };
  let observer: IntersectionObserver | undefined = undefined;

  watch(
    [smooth, motionActive],
    () => {
      client.smooth = smooth() && motionActive.value;
      frameTimestamp.value = Date.now();
      syncAnimation();
    },
    { immediate: true, flush: "sync" },
  );

  onMounted(() => {
    clients.add(client);

    if (clients.size === 1) startClock();

    syncAnimation();

    if (typeof IntersectionObserver !== "undefined" && element.value) {
      observer = new IntersectionObserver((entries) => {
        inView.value = entries[0]?.isIntersecting ?? false;
      });
      observer.observe(element.value);
    }
  });

  onBeforeUnmount(() => {
    observer?.disconnect();
    clients.delete(client);

    if (clients.size) syncAnimation();
    else stopClock();
  });

  return { timestamp, frameTimestamp, motionActive };
};
