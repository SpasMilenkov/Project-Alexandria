import type { AuditLogResult } from "@/api/activity";

import { LogSource } from "@/enums/log-source";

// Mirrors SystemConfig.SystemId on the backend: rows with no signed-in actor carry it.
export const SYSTEM_USER_ID = "00000000-0000-0000-0000-000000000001";

export type AuditActorKind = "self" | "system" | "other";

export interface AuditActor {
  kind: AuditActorKind;
  label: string;
  // Someone other than the viewer acted on the viewer's own data.
  affectsViewer: boolean;
}

const isSystemActor = (row: AuditLogResult): boolean =>
  row.userId === SYSTEM_USER_ID || row.logSource === LogSource.System;

export const describeAuditActor = (
  row: AuditLogResult,
  viewerId: string | undefined,
): AuditActor => {
  const affectsViewer = Boolean(viewerId) && row.ownerId === viewerId && row.userId !== viewerId;

  if (viewerId && row.userId === viewerId)
    return { affectsViewer: false, kind: "self", label: "You" };
  if (isSystemActor(row)) return { affectsViewer, kind: "system", label: "System" };

  return { affectsViewer, kind: "other", label: row.userName ?? "A deleted account" };
};

// "by you", "by the system", "by Alice"; a change made to the viewer's data says so.
export const auditActorPhrase = (actor: AuditActor): string => {
  if (actor.kind === "self") return "by you";

  const by = actor.kind === "system" ? "by the system" : `by ${actor.label}`;
  if (!actor.affectsViewer) return by;

  return `${by}, on your account`;
};
