import {
  OperationalEventCode,
  OperationalEventSeverity,
  OperationalEventStatus,
  ServiceType,
} from "@/enums";

export const SERVICE_LABELS: Record<ServiceType, string> = {
  [ServiceType.Api]: "API",
  [ServiceType.DocumentPreviews]: "Document Previews",
  [ServiceType.Lyrics]: "Lyrics",
  [ServiceType.MediaMetadata]: "Media Metadata",
  [ServiceType.MediaPreviews]: "Media Previews",
  [ServiceType.Transpilation]: "Transpilation",
};

export const SERVICE_ICONS: Record<ServiceType, string> = {
  [ServiceType.Api]: "mdi:server",
  [ServiceType.DocumentPreviews]: "mdi:file-document-outline",
  [ServiceType.Lyrics]: "mdi:script-text-outline",
  [ServiceType.MediaMetadata]: "mdi:tag-text-outline",
  [ServiceType.MediaPreviews]: "mdi:image-multiple-outline",
  [ServiceType.Transpilation]: "mdi:file-swap-outline",
};

export const EVENT_CODE_LABELS: Record<OperationalEventCode, string> = {
  [OperationalEventCode.ErrorRateThresholdExceeded]: "Error Rate Threshold Exceeded",
  [OperationalEventCode.HealthcheckUnhealthy]: "Healthcheck Unhealthy",
  [OperationalEventCode.HealthcheckUnreachable]: "Healthcheck Unreachable",
  [OperationalEventCode.UserReportedProblem]: "User Reported Problem",
};

export const EVENT_CODE_ICONS: Record<OperationalEventCode, string> = {
  [OperationalEventCode.ErrorRateThresholdExceeded]: "mdi:trending-up",
  [OperationalEventCode.HealthcheckUnhealthy]: "mdi:heart-off-outline",
  [OperationalEventCode.HealthcheckUnreachable]: "mdi:lan-disconnect",
  [OperationalEventCode.UserReportedProblem]: "mdi:account-alert-outline",
};

export const SEVERITY_LABELS: Record<OperationalEventSeverity, string> = {
  [OperationalEventSeverity.DegradedPerformance]: "Degraded Performance",
  [OperationalEventSeverity.Failure]: "Failure",
  [OperationalEventSeverity.PartialFailure]: "Partial Failure",
};

type SeverityBadgeColor = "error" | "warning" | "info";

export const SEVERITY_BADGE_COLORS: Record<OperationalEventSeverity, SeverityBadgeColor> = {
  [OperationalEventSeverity.DegradedPerformance]: "info",
  [OperationalEventSeverity.Failure]: "error",
  [OperationalEventSeverity.PartialFailure]: "warning",
};

export const STATUS_LABELS: Record<OperationalEventStatus, string> = {
  [OperationalEventStatus.Active]: "Active",
  [OperationalEventStatus.Resolved]: "Resolved",
};

// Mirrors GetPublicStatusEndpoint: no active event = healthy, Failure = down.
export const serviceHealth = (
  activeEvent: { severity: OperationalEventSeverity } | undefined | null,
): { label: string; dot: string; text: string } => {
  if (!activeEvent)
    return {
      dot: "bg-emerald-500",
      label: "Healthy",
      text: "text-emerald-600 dark:text-emerald-400",
    };
  if (activeEvent.severity === OperationalEventSeverity.Failure)
    return { dot: "bg-red-500", label: "Down", text: "text-red-600 dark:text-red-400" };
  return { dot: "bg-amber-500", label: "Degraded", text: "text-amber-600 dark:text-amber-400" };
};
