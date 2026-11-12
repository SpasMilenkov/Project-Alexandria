import { describe, expect, it } from "vitest";

import { AuditEventCode, EntityType } from "@/enums";
import { auditRestoreTarget, canRestoreFromAudit } from "@/features/audit/restore";

import { OTHER, VIEWER, auditRow } from "./fixtures";

const NOW = Date.parse("2026-11-10T12:00:00.000Z");

const trashed = (patch = {}) =>
  auditRow({
    eventCode: AuditEventCode.FileSoftDeleted,
    timestamp: "2026-11-09T12:00:00.000Z",
    ...patch,
  });

describe("canRestoreFromAudit", () => {
  it("offers a restore for the viewer's recent trash", () => {
    const row = trashed();

    expect(canRestoreFromAudit(row, [row], VIEWER, NOW)).toBe(true);
    expect(auditRestoreTarget(row)).toEqual({ fileId: row.entityId, kind: "file" });
  });

  it("hides it once the trash window has passed", () => {
    const row = trashed({ timestamp: "2026-10-01T12:00:00.000Z" });

    expect(canRestoreFromAudit(row, [row], VIEWER, NOW)).toBe(false);
  });

  it("hides it when a newer row already restored or purged the item", () => {
    const row = trashed();
    const restored = auditRow({
      entityId: row.entityId,
      eventCode: AuditEventCode.FileRestored,
      timestamp: "2026-11-10T08:00:00.000Z",
    });

    expect(canRestoreFromAudit(row, [restored, row], VIEWER, NOW)).toBe(false);
  });

  it("never offers it for someone else's data", () => {
    const row = trashed({ ownerId: OTHER, userId: OTHER });

    expect(canRestoreFromAudit(row, [row], VIEWER, NOW)).toBe(false);
  });

  it("restores a version through its file", () => {
    const row = auditRow({
      entityType: EntityType.FileVersion,
      eventCode: AuditEventCode.FileVersionSoftDeleted,
      parentEntityId: "file-1",
    });

    expect(auditRestoreTarget(row)).toEqual({
      fileId: "file-1",
      kind: "version",
      versionId: row.entityId,
    });
  });
});
