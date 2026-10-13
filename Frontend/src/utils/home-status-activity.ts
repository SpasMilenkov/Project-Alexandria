import type {
  DailyServiceStatus,
  PublicServiceCurrent,
  PublicServiceState,
} from "@/api/publicStatus";

import { AuditEventCode, type AuditLogResult, OperationType } from "@/api/activity";
import { useAuditMessage } from "@/composables/useAuditMessage";
import { ServiceType } from "@/enums";
import { HomeActivityStyle, HomeStatusStyle } from "@/enums/home-widget";
import { PUBLIC_SERVICE_LABELS } from "@/utils/publicStatus.utils";

export const HOME_STATUS_STYLES = [
  {
    value: HomeStatusStyle.Beacon,
    label: "Beacon",
    description: "One verdict, with services when space allows.",
  },
  {
    value: HomeStatusStyle.Services,
    label: "Services",
    description: "A compact list of service states.",
  },
  {
    value: HomeStatusStyle.History,
    label: "History strip",
    description: "Thirty days of recorded incidents per service.",
  },
] as const;

export const HOME_ACTIVITY_STYLES = [
  {
    value: HomeActivityStyle.Feed,
    label: "Feed",
    description: "Recent events, with their time and action type.",
  },
  {
    value: HomeActivityStyle.ByDay,
    label: "By day",
    description: "The same recent events grouped by day.",
  },
] as const;

export type HomeServiceState = PublicServiceState | "Unknown";

export const HOME_STATUS_PRESENTATION = {
  Healthy: {
    label: "Operational",
    verdict: "No active incidents",
    icon: "i-lucide-circle-check",
    color: "success",
    text: "text-emerald-700 dark:text-emerald-400",
    fill: "bg-emerald-500",
    wash: "bg-emerald-500/10",
  },
  Degraded: {
    label: "Degraded",
    verdict: "Some services degraded",
    icon: "i-lucide-triangle-alert",
    color: "warning",
    text: "text-amber-700 dark:text-amber-400",
    fill: "bg-amber-500",
    wash: "bg-amber-500/10",
  },
  Down: {
    label: "Down",
    verdict: "Service disruption",
    icon: "i-lucide-alert-circle",
    color: "error",
    text: "text-red-700 dark:text-red-400",
    fill: "bg-red-500",
    wash: "bg-red-500/10",
  },
  Unknown: {
    label: "Unknown",
    verdict: "Status unknown",
    icon: "i-lucide-circle",
    color: "neutral",
    text: "text-gray-600 dark:text-gray-400",
    fill: "bg-gray-400 dark:bg-gray-600",
    wash: "bg-gray-500/10",
  },
} as const;

const SERVICES = [
  ServiceType.Api,
  ServiceType.MediaPreviews,
  ServiceType.DocumentPreviews,
  ServiceType.Transpilation,
  ServiceType.Lyrics,
  ServiceType.MediaMetadata,
  ServiceType.Playlist,
];

const serviceState = (value: unknown): HomeServiceState => {
  if (value === "Healthy" || value === "Degraded" || value === "Down") return value;

  return "Unknown";
};

export const homeCurrentStatusRows = (current: PublicServiceCurrent[]) =>
  SERVICES.map((service) => ({
    service,
    label: PUBLIC_SERVICE_LABELS[service],
    state: serviceState(current.find((item) => item.serviceType === service)?.status),
  }));

export const homeOverallStatus = (states: HomeServiceState[]): HomeServiceState => {
  if (states.includes("Down")) return "Down";
  if (states.includes("Degraded")) return "Degraded";
  if (!states.length || states.includes("Unknown")) return "Unknown";

  return "Healthy";
};

export const homeHistoryRows = (history: DailyServiceStatus[], now = new Date()) => {
  const today = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate());
  const dates = Array.from({ length: 30 }, (_, index) =>
    new Date(today - (29 - index) * 86_400_000).toISOString().slice(0, 10),
  );

  return SERVICES.map((service) => {
    const recorded = history.find((item) => item.serviceType === service)?.days ?? [];

    return {
      service,
      label: PUBLIC_SERVICE_LABELS[service],
      days: dates.map((date) => ({
        date,
        state: serviceState(recorded.find((day) => day.date === date)?.status),
      })),
    };
  });
};

const NAMED_EVENTS = new Set([
  AuditEventCode.FileCreated,
  AuditEventCode.FileRenamed,
  AuditEventCode.FileMoved,
  AuditEventCode.FileSoftDeleted,
  AuditEventCode.FileRestored,
  AuditEventCode.FileDeletedPermanently,
  AuditEventCode.DirectoryCreated,
  AuditEventCode.DirectoryRenamed,
  AuditEventCode.DirectoryMoved,
  AuditEventCode.DirectorySoftDeleted,
  AuditEventCode.DirectoryRestored,
  AuditEventCode.DirectoryDeletedPermanently,
  AuditEventCode.TagCreated,
  AuditEventCode.TagRenamed,
  AuditEventCode.TagUpdated,
  AuditEventCode.TagSoftDeleted,
  AuditEventCode.TagRestored,
  AuditEventCode.TagDeletedPermanently,
]);

const RENAMED_EVENTS = new Set([
  AuditEventCode.FileRenamed,
  AuditEventCode.DirectoryRenamed,
  AuditEventCode.TagRenamed,
]);

export const homeActivityDescription = (event: AuditLogResult): string => {
  const { getMessage } = useAuditMessage();
  const label = getMessage(event.eventCode);
  if (!NAMED_EVENTS.has(event.eventCode) || !event.metadata) return label;

  try {
    const metadata: unknown = JSON.parse(event.metadata);
    if (!metadata || typeof metadata !== "object" || Array.isArray(metadata)) return label;

    const values = metadata as Record<string, unknown>;
    let name = values.name;
    if (RENAMED_EVENTS.has(event.eventCode) && typeof values.new === "string") name = values.new;
    if (typeof name !== "string" || !name.trim()) return label;

    return `${label} · ${name.trim()}`;
  } catch {
    return label;
  }
};

export const HOME_ACTIVITY_PRESENTATION = {
  [OperationType.Read]: {
    label: "Read",
    icon: "i-lucide-eye",
    color: "neutral",
    text: "text-gray-600 dark:text-gray-400",
  },
  [OperationType.Create]: {
    label: "Create",
    icon: "i-lucide-file-plus",
    color: "success",
    text: "text-emerald-700 dark:text-emerald-400",
  },
  [OperationType.Update]: {
    label: "Update",
    icon: "i-lucide-pencil",
    color: "warning",
    text: "text-amber-700 dark:text-amber-400",
  },
  [OperationType.Delete]: {
    label: "Delete",
    icon: "i-lucide-trash-2",
    color: "error",
    text: "text-red-700 dark:text-red-400",
  },
  [OperationType.Login]: {
    label: "Sign-in",
    icon: "i-lucide-log-in",
    color: "info",
    text: "text-blue-700 dark:text-blue-400",
  },
  [OperationType.Logout]: {
    label: "Sign-out",
    icon: "i-lucide-log-out",
    color: "neutral",
    text: "text-gray-600 dark:text-gray-400",
  },
} as const;

export const homeActivityPresentation = (operation: OperationType) =>
  HOME_ACTIVITY_PRESENTATION[operation] ?? HOME_ACTIVITY_PRESENTATION[OperationType.Read];

const localDateKey = (date: Date) => `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;

export interface HomeActivityGroup {
  label: string;
  events: AuditLogResult[];
}

export const homeActivityGroups = (
  events: AuditLogResult[],
  now = new Date(),
): HomeActivityGroup[] => {
  const yesterday = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1);
  const todayKey = localDateKey(now);
  const yesterdayKey = localDateKey(yesterday);
  const groups: HomeActivityGroup[] = [
    { label: "Today", events: [] },
    { label: "Yesterday", events: [] },
    { label: "Earlier", events: [] },
  ];

  for (const event of events) {
    const key = localDateKey(new Date(event.timestamp));
    let group = groups[2]!;
    if (key === todayKey) group = groups[0]!;
    else if (key === yesterdayKey) group = groups[1]!;

    group.events.push(event);
  }

  return groups.filter((group) => group.events.length > 0);
};

export type HomeActivityDisplayRow =
  | { kind: "heading"; label: string; key: string }
  | { kind: "event"; event: AuditLogResult; key: string };

export const homeActivityDisplayRows = (
  groups: HomeActivityGroup[],
  height: number,
): HomeActivityDisplayRow[] => {
  const rows: HomeActivityDisplayRow[] = [];
  let remaining = height;

  for (const group of groups) {
    if (remaining < 64) break;

    rows.push({ kind: "heading", label: group.label, key: group.label });
    remaining -= 24;

    for (const [index, event] of group.events.entries()) {
      if (remaining < 40) return rows;

      rows.push({ kind: "event", event, key: `${group.label}-${index}` });
      remaining -= 40;
    }
  }

  return rows;
};

export const homeActivityTime = (timestamp: string, now = new Date()): string => {
  const date = new Date(timestamp);
  if (!Number.isFinite(date.getTime())) return "Date unavailable";

  const elapsed = Math.max(0, now.getTime() - date.getTime());
  if (elapsed < 60_000) return "Just now";
  if (elapsed < 3_600_000) return `${Math.floor(elapsed / 60_000)}m ago`;
  if (localDateKey(date) === localDateKey(now))
    return date.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });

  return date.toLocaleDateString([], { month: "short", day: "numeric" });
};
