import type { PublicServiceState } from "@/api/publicStatus";

import { ServiceType } from "@/enums";

// Friendlier copy than the admin maps — this surface is read by non-admins and
// deliberately decoupled from the admin severity/label vocabulary (D11).
export const PUBLIC_SERVICE_LABELS: Record<ServiceType, string> = {
  [ServiceType.Api]: "Core Platform",
  [ServiceType.DocumentPreviews]: "Document Previews",
  [ServiceType.Lyrics]: "Lyrics Lookup",
  [ServiceType.MediaMetadata]: "Audio Analysis",
  [ServiceType.MediaPreviews]: "Media Previews",
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
