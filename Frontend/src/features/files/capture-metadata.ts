const trimNumber = (value: number, digits: number): string => String(Number(value.toFixed(digits)));

// Every formatter runs in UTC on a pre-shifted timestamp, so the digits stay those of the
// camera's clock instead of moving into the viewer's zone.
const CAPTURE_DATE = new Intl.DateTimeFormat(undefined, {
  day: "numeric",
  month: "short",
  year: "numeric",
  timeZone: "UTC",
});

const CAPTURE_TIME = new Intl.DateTimeFormat(undefined, {
  hour: "2-digit",
  minute: "2-digit",
  timeZone: "UTC",
});

const CAPTURE_WEEKDAY = new Intl.DateTimeFormat(undefined, { weekday: "long", timeZone: "UTC" });

const DAY_PARTS: readonly { fromHour: number; label: string }[] = [
  { fromHour: 21, label: "night" },
  { fromHour: 17, label: "evening" },
  { fromHour: 12, label: "afternoon" },
  { fromHour: 5, label: "morning" },
  { fromHour: 0, label: "night" },
];

export const formatOffset = (offsetMinutes: number): string => {
  if (offsetMinutes === 0) return "UTC";

  const sign = offsetMinutes > 0 ? "+" : "-";
  const absolute = Math.abs(offsetMinutes);
  const hours = String(Math.floor(absolute / 60)).padStart(2, "0");
  const minutes = String(absolute % 60).padStart(2, "0");

  return `UTC${sign}${hours}:${minutes}`;
};

const dayPartOf = (hour: number): string =>
  DAY_PARTS.find((part) => hour >= part.fromHour)?.label ?? "night";

export interface CaptureMoment {
  /** "Sunday afternoon", read off the clock where the photo was taken. */
  headline: string;
  date: string;
  time: string;
  zoneNote: string;
}

export const describeCaptureMoment = (
  capturedAt: string,
  offsetMinutes: number | null,
): CaptureMoment => {
  const instant = new Date(capturedAt).getTime();
  const wallClock = new Date(instant + (offsetMinutes ?? 0) * 60_000);

  const weekday = CAPTURE_WEEKDAY.format(wallClock);
  const headline = `${weekday} ${dayPartOf(wallClock.getUTCHours())}`;

  let zoneNote = "Camera clock, time zone unknown";
  if (offsetMinutes !== null) zoneNote = `Local time, ${formatOffset(offsetMinutes)}`;

  return {
    headline,
    date: CAPTURE_DATE.format(wallClock),
    time: CAPTURE_TIME.format(wallClock),
    zoneNote,
  };
};

/** Make and model, without repeating the make when the model already starts with it. */
export const formatCamera = (make: string | null, model: string | null): string | null => {
  const cleanMake = make?.trim() ?? "";
  const cleanModel = model?.trim() ?? "";

  if (!cleanMake) return cleanModel || null;
  if (!cleanModel) return cleanMake;
  if (cleanModel.toLowerCase().startsWith(cleanMake.toLowerCase())) return cleanModel;

  return `${cleanMake} ${cleanModel}`;
};

// Cameras show fast shutters as fractions and slow ones in seconds.
export const formatExposure = (seconds: number): string => {
  if (seconds > 0 && seconds < 0.3) return `1/${Math.round(1 / seconds)} s`;

  return `${trimNumber(seconds, 1)} s`;
};

export const formatAperture = (fNumber: number): string => `f/${trimNumber(fNumber, 1)}`;

export const formatFocalLength = (millimetres: number): string =>
  `${trimNumber(millimetres, 1)} mm`;

export const formatMegapixels = (width: number, height: number): string =>
  `${trimNumber((width * height) / 1_000_000, 1)} MP`;

export const describeShape = (width: number, height: number): string => {
  const ratio = Math.max(width, height) / Math.min(width, height);

  if (ratio >= 2.2) return "Panorama";
  if (ratio < 1.05) return "Square";
  if (width > height) return "Landscape";

  return "Portrait";
};

export const formatCoordinates = (latitude: number, longitude: number): string => {
  const latHemisphere = latitude >= 0 ? "N" : "S";
  const lonHemisphere = longitude >= 0 ? "E" : "W";

  return `${Math.abs(latitude).toFixed(5)}° ${latHemisphere}, ${Math.abs(longitude).toFixed(5)}° ${lonHemisphere}`;
};

// The hints below are rules of thumb for people who don't read exposure values. They stay
// silent in the middle ranges, where there is nothing notable to say.

export const shutterHint = (seconds: number): string | undefined => {
  if (seconds >= 1) return "Long exposure";
  if (seconds >= 1 / 30) return "Slow, motion may blur";
  if (seconds <= 1 / 500) return "Fast, freezes motion";

  return undefined;
};

export const isoHint = (iso: number): string | undefined => {
  if (iso >= 1600) return "Low light";
  if (iso <= 200) return "Good light";

  return undefined;
};

// Only the full-frame equivalent is comparable across cameras, since a phone's real focal
// length is a few millimetres whatever it frames.
export const focalRangeHint = (focal35mm: number): string => {
  if (focal35mm < 24) return "Ultra-wide";
  if (focal35mm < 35) return "Wide";
  if (focal35mm <= 70) return "Standard";

  return "Telephoto";
};

export interface ExposureSettings {
  fNumber: number | null;
  exposureSeconds: number | null;
  iso: number | null;
  focalLengthMm: number | null;
  focalLength35mm: number | null;
}

/** The one-line "f/1.8 · 1/120 s · ISO 200 · 26 mm" summary photo apps show. */
export const summarizeSettings = (settings: ExposureSettings): string => {
  const parts: string[] = [];

  if (settings.fNumber !== null) parts.push(formatAperture(settings.fNumber));
  if (settings.exposureSeconds !== null) parts.push(formatExposure(settings.exposureSeconds));
  if (settings.iso !== null) parts.push(`ISO ${settings.iso}`);

  const focal = settings.focalLength35mm ?? settings.focalLengthMm;
  if (focal !== null) parts.push(formatFocalLength(focal));

  return parts.join(" · ");
};
