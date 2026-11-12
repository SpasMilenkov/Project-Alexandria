import type { AuditLogResult } from "@/api/activity";

import { AuditEventCode, EntityType, LogSource, OperationType } from "@/enums";

export const VIEWER = "11111111-1111-1111-1111-111111111111";
export const OTHER = "22222222-2222-2222-2222-222222222222";

let sequence = 0;

export const auditRow = (patch: Partial<AuditLogResult> = {}): AuditLogResult => {
  sequence += 1;

  return {
    correlationId: null,
    entityId: `00000000-0000-0000-0000-${String(sequence).padStart(12, "0")}`,
    entityName: null,
    entityType: EntityType.File,
    eventCode: AuditEventCode.FileCreated,
    fallbackDescription: null,
    id: `row-${sequence}`,
    ipAddress: null,
    logSource: LogSource.Trigger,
    metadata: null,
    operationType: OperationType.Create,
    ownerId: VIEWER,
    parentEntityId: null,
    timestamp: "2026-11-10T12:00:00.000Z",
    userId: VIEWER,
    userName: "Viewer",
    ...patch,
  };
};
