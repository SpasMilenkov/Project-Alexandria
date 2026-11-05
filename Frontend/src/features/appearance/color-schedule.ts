export type SchedulePhase = "light" | "dark";

export interface ScheduledSwitch {
  phase: SchedulePhase;
  at: Date;
}

const TIME_PATTERN = /^(?<hours>[01]\d|2[0-3]):(?<minutes>[0-5]\d)$/u;

const parseTime = (value: string): { hours: number; minutes: number } | null => {
  const groups = TIME_PATTERN.exec(value)?.groups;

  if (!groups) return null;

  return { hours: Number(groups.hours), minutes: Number(groups.minutes) };
};

const atLocalTime = (base: Date, dayOffset: number, time: { hours: number; minutes: number }) =>
  new Date(
    base.getFullYear(),
    base.getMonth(),
    base.getDate() + dayOffset,
    time.hours,
    time.minutes,
  );

/**
 * The most recent scheduled switch at or before `now`, in local time. Two daily
 * boundaries ("HH:MM") alternate the phase, so the night window may span midnight.
 * Returns null when either time is invalid or both are equal (no schedule to follow).
 */
export const lastScheduledSwitch = (
  now: Date,
  lightFrom: string,
  darkFrom: string,
): ScheduledSwitch | null => {
  const light = parseTime(lightFrom);
  const dark = parseTime(darkFrom);

  if (!light || !dark || lightFrom === darkFrom) return null;

  const candidates: ScheduledSwitch[] = [-1, 0].flatMap((dayOffset) => [
    { at: atLocalTime(now, dayOffset, light), phase: "light" as const },
    { at: atLocalTime(now, dayOffset, dark), phase: "dark" as const },
  ]);

  return candidates
    .filter((candidate) => candidate.at.getTime() <= now.getTime())
    .reduce((latest, candidate) => (candidate.at > latest.at ? candidate : latest));
};
