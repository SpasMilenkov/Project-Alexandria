import type { WrappedCardFacts, WrappedPeriodPoint } from "@/api/stats";

import { bounded } from "@/utils/wrapped-art.utils";

const utcStamp = (value?: string | null): number => {
  const time = Date.parse(value ?? "");

  return Number.isFinite(time) ? time : 0;
};

export const dateLabel = (value?: string | null): string => {
  if (!value || !Number.isFinite(Date.parse(value))) return "Unknown date";

  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    timeZone: "UTC",
  }).format(new Date(value));
};

export interface CalendarCell {
  date: string;
  seconds: number;
  plays: number;
  inRange: boolean;
  streak: boolean;
  future: boolean;
}

export const calendarMonths = (
  series: WrappedPeriodPoint[],
  from: string,
  to: string,
  streakFrom?: string | null,
  streakTo?: string | null,
) => {
  const start = new Date(from);
  const end = new Date(to);

  if (!Number.isFinite(start.getTime()) || !Number.isFinite(end.getTime()) || start >= end)
    return [];

  const lookup = new Map(series.map((day) => [day.date.slice(0, 10), day]));
  const months: { key: string; label: string; cells: (CalendarCell | null)[] }[] = [];
  const month = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth(), 1));

  while (month < end && months.length < 13) {
    const leading = (month.getUTCDay() + 6) % 7;
    const monthEnd = new Date(Date.UTC(month.getUTCFullYear(), month.getUTCMonth() + 1, 1));
    const count = Math.round((monthEnd.getTime() - month.getTime()) / 86400000);
    const cells: (CalendarCell | null)[] = Array.from({ length: 42 }, () => null);

    for (let day = 0; day < count; day++) {
      const time = month.getTime() + day * 86400000;
      const date = new Date(time).toISOString().slice(0, 10);
      const point = lookup.get(date);
      const inRange = time + 86400000 > start.getTime() && time < end.getTime();

      cells[leading + day] = {
        date,
        seconds: point?.seconds ?? 0,
        plays: point?.plays ?? 0,
        inRange,

        streak:
          inRange &&
          time >= utcStamp(streakFrom?.slice(0, 10)) &&
          time <= utcStamp(streakTo?.slice(0, 10)),

        future: time >= end.getTime(),
      };
    }

    months.push({
      key: month.toISOString(),

      label: new Intl.DateTimeFormat(undefined, {
        month: "long",
        year: "numeric",
        timeZone: "UTC",
      }).format(month),

      cells,
    });

    month.setUTCMonth(month.getUTCMonth() + 1);
  }

  return months;
};

const WIDTH = 720;
const HEIGHT = 176;
const PAD = 32;
const BASE = 120; // y of the flatline
const BEAT_WIDTH = 120; // reserves room for the rise, crest, and settling wave
const R_OFFSET = 40; // x offset of the crest inside a beat
const SPIKE_HEIGHT = 90; // the return beat
const LAST_BLIP = { height: 26, scale: 0.55 }; // small beat marking the last play
const ECHO = { height: 32, scale: 0.65 }; // settling beat after the return
const ECHO_GAP = 14;
const TAIL_FLAT = 24;
const MIN_GAP = 40;
const FULL_SCALE_DAYS = 365;
const BRACKET_Y = 162;
const LABEL_HALF = 64; // keeps the "N days" label inside the graph

const fixed = (n: number) => n.toFixed(1);

// Broad slopes and horizontal tangents keep each pulse smooth even on narrow screens.
const beat = (x: number, height: number, scale = 1): string => {
  const p = (dx: number, dy: number) => `${fixed(x + dx * scale)} ${fixed(BASE + dy)}`;

  return [
    `H ${fixed(x)}`,
    `C ${p(14, 0)} ${p(18, -height)} ${p(R_OFFSET, -height)}`,
    `C ${p(62, -height)} ${p(62, height * 0.24)} ${p(78, height * 0.24)}`,
    `C ${p(90, height * 0.24)} ${p(90, -height * 0.14)} ${p(102, -height * 0.14)}`,
    `C ${p(114, -height * 0.14)} ${p(114, 0)} ${p(BEAT_WIDTH, 0)}`,
  ].join(" ");
};

export const returnGeometry = (from?: string | null, to?: string | null) => {
  const days = Math.max(0, (utcStamp(to) - utcStamp(from)) / 86400000);

  const blipX = PAD + 24;
  const gapStart = blipX + BEAT_WIDTH * LAST_BLIP.scale;
  const rightRun = BEAT_WIDTH + ECHO_GAP + BEAT_WIDTH * ECHO.scale + TAIL_FLAT;
  const maxGap = WIDTH - PAD - rightRun - gapStart;

  // Log scale so a 3-week and a 300-day gap are both readable.
  const t = bounded(Math.log1p(days) / Math.log1p(FULL_SCALE_DAYS));
  const gapEnd = gapStart + MIN_GAP + t * (maxGap - MIN_GAP);
  const echoX = gapEnd + BEAT_WIDTH + ECHO_GAP;

  const labelCenter = Math.min(WIDTH - LABEL_HALF, Math.max(LABEL_HALF, (gapStart + gapEnd) / 2));

  return {
    days,
    width: WIDTH,
    height: HEIGHT,
    // Solid line into the last play, then the quiet stretch, then the return.
    leadPath: `M ${PAD} ${BASE} ${beat(blipX, LAST_BLIP.height, LAST_BLIP.scale)}`,
    gapPath: `M ${fixed(gapStart)} ${BASE} H ${fixed(gapEnd)}`,
    returnPath: `M ${fixed(gapEnd)} ${BASE} ${beat(gapEnd, SPIKE_HEIGHT)} ${beat(echoX, ECHO.height, ECHO.scale)} H ${WIDTH - PAD}`,
    bracketPath: `M ${fixed(gapStart)} ${BRACKET_Y - 5} V ${BRACKET_Y} H ${fixed(gapEnd)} V ${BRACKET_Y - 5}`,
    apex: { x: gapEnd + R_OFFSET, y: BASE - SPIKE_HEIGHT },
    lastApex: { x: blipX + R_OFFSET * LAST_BLIP.scale, y: BASE - LAST_BLIP.height },
    labelLeft: labelCenter / WIDTH,
  };
};

export const replayMarks = (value: number) => {
  const count = Math.floor(bounded(value, 0, 1e7));
  const unit = Math.max(1, Math.ceil(count / 120));
  const markCount = Math.ceil(count / unit);

  const marks = Array.from({ length: markCount }, (_, i) => {
    const ring = Math.floor(i / 40);
    const countInRing = Math.min(40, markCount - ring * 40);
    const angle = ((i % 40) / countInRing) * Math.PI * 2 - Math.PI / 2;
    const radius = 102 + ring * 18;

    return {
      x1: 180 + Math.cos(angle) * radius,
      y1: 180 + Math.sin(angle) * radius,
      x2: 180 + Math.cos(angle) * (radius + 10),
      y2: 180 + Math.sin(angle) * (radius + 10),
      value: Math.min(unit, count - i * unit),
    };
  });

  return { unit, marks, remainder: count % unit };
};

export const waveformDays = (
  series: WrappedPeriodPoint[],
  peak: string | null | undefined,
  whole: boolean,
) => {
  if (whole || series.length <= 31) return series;

  const index = Math.max(
    0,
    series.findIndex((point) => point.date === peak),
  );

  const start = Math.max(0, Math.min(series.length - 31, index - 15));

  return series.slice(start, start + 31);
};

export const vinylGroove = (facts: WrappedCardFacts, from: string, to: string) => {
  const first = utcStamp(facts.from);
  const last = Math.max(first, utcStamp(facts.to));
  const periodStart = utcStamp(from);
  const duration = Math.max(86400000, utcStamp(to) - periodStart);
  const startAngle = ((first - periodStart) / duration) * Math.PI * 2 - Math.PI / 2;

  const at = (time: number) => {
    const elapsed = bounded((time - first) / duration);
    const angle = startAngle + elapsed * Math.PI * 12;
    const radius = 142 - elapsed * 108;

    return { x: 180 + Math.cos(angle) * radius, y: 180 + Math.sin(angle) * radius };
  };

  const max = Math.max(1, ...facts.series.map((day) => day.seconds));

  const segments = facts.series
    .filter((day) => utcStamp(day.date) <= last && utcStamp(day.date) + 86400000 > first)
    .map((day) => {
      const start = Math.max(first, utcStamp(day.date));
      const end = Math.min(last, utcStamp(day.date) + 86400000);
      const points = Array.from({ length: 5 }, (_, i) => at(start + ((end - start) * i) / 4));

      return {
        date: day.date,
        seconds: day.seconds,
        width: 0.8 + 2.2 * Math.sqrt(bounded(day.seconds / max)),

        path: points
          .map((p, i) => `${i === 0 ? "M" : "L"} ${p.x.toFixed(2)} ${p.y.toFixed(2)}`)
          .join(" "),
      };
    });

  return { first: at(first), last: at(last), single: first === last, segments };
};
