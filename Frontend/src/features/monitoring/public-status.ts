import type { DailyStatusPoint, PublicServiceState } from "@/api/publicStatus";

import { ServiceType } from "@/enums";
import { formatCalendarDate, formatDaysAgo } from "@/utils/date-formatters";

// Friendlier copy than the admin maps — this surface is read by non-admins and
// deliberately decoupled from the admin severity/label vocabulary (D11).
export const PUBLIC_SERVICE_LABELS: Record<ServiceType, string> = {
  [ServiceType.Api]: "Core Platform",
  [ServiceType.DocumentPreviews]: "Document Previews",
  [ServiceType.Lyrics]: "Lyrics Lookup",
  [ServiceType.MediaMetadata]: "Audio Analysis",
  [ServiceType.MediaPreviews]: "Media Previews",
  [ServiceType.Playlist]: "Playlists",
  [ServiceType.Transpilation]: "Video Streaming",
};

type PublicBadgeColor = "error" | "success" | "warning";

export const PUBLIC_STATE_BADGE_COLORS: Record<PublicServiceState, PublicBadgeColor> = {
  Degraded: "warning",
  Down: "error",
  Healthy: "success",
};

export const PUBLIC_STATE_LABELS: Record<PublicServiceState, string> = {
  Degraded: "Degraded",
  Down: "Down",
  Healthy: "Operational",
};

export const PUBLIC_STATE_BAR_COLORS: Record<PublicServiceState, string> = {
  Degraded: "bg-amber-500",
  Down: "bg-red-500",
  Healthy: "bg-emerald-500",
};

export interface OverallPublicStatus {
  color: PublicBadgeColor;
  icon: string;
  label: string;
}

// Worst current state across services — the glance-and-leave signal
export const overallPublicState = (statuses: PublicServiceState[]): OverallPublicStatus => {
  if (statuses.includes("Down")) {
    return { color: "error", icon: "i-lucide-alert-circle", label: "Service disruption" };
  }
  if (statuses.includes("Degraded")) {
    return {
      color: "warning",
      icon: "i-lucide-triangle-alert",
      label: "Some systems are experiencing issues",
    };
  }
  return { color: "success", icon: "i-lucide-circle-check", label: "All systems operational" };
};

export interface ServiceWindowStats {
  operationalPercent: number;
  issueDays: number;
  daysSinceIssue: number | null;
}

// Days are ordered oldest to newest, so the last point is today.
const daysSinceLast = (flags: boolean[]): number | null => {
  const lastIndex = flags.lastIndexOf(true);
  if (lastIndex === -1) {
    return null;
  }
  return flags.length - 1 - lastIndex;
};

// Floored so a single bad day never rounds up to a perfect 100%.
const operationalShare = (cleanDays: number, totalDays: number) => {
  if (totalDays === 0) {
    return 100;
  }
  return Math.floor((cleanDays / totalDays) * 1000) / 10;
};

export const serviceWindowStats = (days: DailyStatusPoint[]): ServiceWindowStats => {
  const issueFlags = days.map((day) => day.status !== "Healthy");
  const issueDays = issueFlags.filter(Boolean).length;

  return {
    daysSinceIssue: daysSinceLast(issueFlags),
    issueDays,
    operationalPercent: operationalShare(days.length - issueDays, days.length),
  };
};

export interface FleetWindowStats {
  cleanDays: number;
  totalDays: number;
  daysSinceDisruption: number | null;
}

// Services share the same day sequence, so days line up by index.
export const fleetWindowStats = (services: DailyStatusPoint[][]): FleetWindowStats => {
  const totalDays = Math.max(0, ...services.map((days) => days.length));
  const disruptedFlags = Array.from({ length: totalDays }, (_, index) =>
    services.some((days) => {
      const status = days[index]?.status;
      return status !== undefined && status !== "Healthy";
    }),
  );

  return {
    cleanDays: disruptedFlags.filter((disrupted) => !disrupted).length,
    daysSinceDisruption: daysSinceLast(disruptedFlags),
    totalDays,
  };
};

export const formatOperationalPercent = (value: number) => `${value}%`;

export const describeDaysSince = (daysSince: number | null) => {
  if (daysSince === null) {
    return "None";
  }
  return formatDaysAgo(daysSince, { capitalize: true });
};

export const describeLastIssue = (daysSince: number | null, windowDays: number) => {
  if (daysSince === null) {
    return `No issues in ${windowDays} days`;
  }
  if (daysSince === 0) {
    return "Issue today";
  }
  return `Last issue ${formatDaysAgo(daysSince)}`;
};

// History days are UTC calendar dates; formatting in UTC keeps them from shifting a day.
export const formatStatusDay = (date: string) =>
  formatCalendarDate(`${date}T00:00:00Z`, { timeZone: "UTC" });
