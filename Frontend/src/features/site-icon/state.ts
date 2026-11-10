import { ACTIVITY_GLYPHS, PLACE_GLYPHS, type SiteIconPlace } from "@/features/site-icon/places";
import { formatCount } from "@/utils/count";

export interface UploadActivity {
  kind: "upload" | "extract";
  progress: number;
  fileCount: number;
  label: string | null;
}

export type UploadOutcome = "done" | "failed";

// Present only while audio or video is actually playing; a paused player is not an activity.
export interface PlaybackActivity {
  title: string;
}

export interface ServiceHealthCounts {
  down: number;
  degraded: number;
}

export interface SiteIconInput {
  place: SiteIconPlace;
  pageTitle: string;
  accentRgb: string;
  isOnline: boolean;
  upload: UploadActivity | null;
  uploadOutcome: UploadOutcome | null;
  playback: PlaybackActivity | null;
  health: ServiceHealthCounts | null;
}

export interface SiteIconState {
  tile: string;
  glyph: string;
  fill: number | null;
  dot: string | null;
  title: string;
}

export const APP_NAME = "Alexandria";

const OFFLINE_TILE = "#6b7280";

export const DOT_COLORS = {
  degraded: "#f59e0b",
  done: "#16a34a",
  down: "#dc2626",
} as const;

const toHex = (value: number) => Math.round(value).toString(16).padStart(2, "0");

// The raw 500 accents are too light behind a white glyph at 16px; a slight darkening
// lands near the 600 shade without a second palette to maintain.
export const accentTile = (accentRgb: string): string => {
  const channels = accentRgb
    .split(/[\s,]+/u)
    .filter(Boolean)
    .map(Number);

  return `#${channels.map((channel) => toHex(channel * 0.88)).join("")}`;
};

// Eighths keep a long upload to a handful of favicon swaps. Rounding down keeps the fill
// from running ahead of the real upload, and the floor of one eighth keeps a started
// upload from looking idle.
export const stepProgress = (percent: number): number => {
  const eighths = Math.floor((Math.min(percent, 100) / 100) * 8);

  return Math.max(1, eighths) / 8;
};

const resolveGlyph = (input: SiteIconInput): string => {
  if (input.uploadOutcome === "failed") return ACTIVITY_GLYPHS.failed;

  if (input.upload) return ACTIVITY_GLYPHS[input.upload.kind];

  if (input.playback) return ACTIVITY_GLYPHS.playing;

  return PLACE_GLYPHS[input.place];
};

const resolveDot = (input: SiteIconInput): string | null => {
  if (input.uploadOutcome === "failed") return DOT_COLORS.down;

  if (input.health && input.health.down > 0) return DOT_COLORS.down;

  if (input.health && input.health.degraded > 0) return DOT_COLORS.degraded;

  if (input.uploadOutcome === "done") return DOT_COLORS.done;

  return null;
};

const uploadTitle = (upload: UploadActivity): string => {
  const percent = `${Math.round(upload.progress)}%`;
  const subject = upload.label ?? formatCount(upload.fileCount, "file");

  if (upload.kind === "extract") return `${percent} · Extracting ${subject}`;

  return `↑ ${percent} · Uploading ${subject}`;
};

const resolveTitle = (input: SiteIconInput): string => {
  const page = input.pageTitle;

  if (input.uploadOutcome === "failed") return `Upload failed · ${page}`;

  if (!input.isOnline) return `Offline · ${page}`;

  if (input.upload) return uploadTitle(input.upload);

  if (input.playback) return input.playback.title;

  if (input.health && input.health.down > 0) {
    return `${formatCount(input.health.down, "service")} down · ${page}`;
  }

  if (input.health && input.health.degraded > 0) {
    return `${formatCount(input.health.degraded, "service")} degraded · ${page}`;
  }

  if (input.uploadOutcome === "done") return `✓ Upload finished · ${page}`;

  if (page === APP_NAME) return APP_NAME;

  return `${page} · ${APP_NAME}`;
};

export const resolveSiteIcon = (input: SiteIconInput): SiteIconState => ({
  dot: resolveDot(input),
  fill: input.upload ? stepProgress(input.upload.progress) : null,
  glyph: resolveGlyph(input),
  tile: input.isOnline ? accentTile(input.accentRgb) : OFFLINE_TILE,
  title: resolveTitle(input),
});
