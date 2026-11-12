import { describe, expect, it } from "vitest";

import { AuditEventCode, EntityType, LogSource, OperationType } from "@/enums";
import { SYSTEM_USER_ID, auditActorPhrase, describeAuditActor } from "@/features/audit/actor";
import { buildAuditTimeline } from "@/features/audit/grouping";
import { auditEntityName, auditEntityRoute, collectGoneEntityIds } from "@/features/audit/links";

import { OTHER, VIEWER, auditRow } from "./fixtures";

const REQUEST = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

describe("buildAuditTimeline", () => {
  it("keeps a single upload as separate rows", () => {
    const file = auditRow({ correlationId: REQUEST });
    const version = auditRow({
      correlationId: REQUEST,
      entityType: EntityType.FileVersion,
      eventCode: AuditEventCode.FileVersionCreated,
      parentEntityId: file.entityId,
    });

    const items = buildAuditTimeline([version, file]);

    expect(items.map((item) => item.kind)).toEqual(["event", "event"]);
  });

  it("collapses a multi-file upload into one summary", () => {
    const rows = [1, 2, 3].flatMap(() => {
      const file = auditRow({ correlationId: REQUEST });

      return [
        auditRow({
          correlationId: REQUEST,
          entityType: EntityType.FileVersion,
          eventCode: AuditEventCode.FileVersionCreated,
          parentEntityId: file.entityId,
        }),
        file,
      ];
    });

    const [item, ...rest] = buildAuditTimeline(rows);

    expect(rest).toHaveLength(0);
    expect(item).toMatchObject({ kind: "group", rows, title: "Uploaded 3 files" });
  });

  it("titles a bulk move from its summary row", () => {
    const summary = auditRow({
      correlationId: REQUEST,
      entityId: "00000000-0000-0000-0000-000000000000",
      entityName: "Music",
      eventCode: AuditEventCode.FilesMovedBulk,
      logSource: LogSource.API,
      metadata: JSON.stringify({ destination_name: "Music", file_count: 2 }),
      operationType: OperationType.Update,
    });
    const moves = [1, 2].map(() =>
      auditRow({ correlationId: REQUEST, eventCode: AuditEventCode.FileMoved }),
    );

    const [item] = buildAuditTimeline([summary, ...moves]);

    expect(item).toMatchObject({ kind: "group", summary, title: "Moved 2 files to Music" });
  });

  it("does not merge rows from different requests or without one", () => {
    const items = buildAuditTimeline([
      auditRow({ correlationId: REQUEST }),
      auditRow({ correlationId: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" }),
      auditRow(),
      auditRow(),
    ]);

    expect(items.every((item) => item.kind === "event")).toBe(true);
  });
});

describe("actor wording", () => {
  it("names the viewer, the system and other people", () => {
    expect(describeAuditActor(auditRow(), VIEWER).label).toBe("You");
    expect(describeAuditActor(auditRow({ userId: SYSTEM_USER_ID }), VIEWER).kind).toBe("system");
    expect(describeAuditActor(auditRow({ userId: OTHER, userName: "admin" }), VIEWER).label).toBe(
      "admin",
    );
  });

  it("says when someone else changed the viewer's data", () => {
    const actor = describeAuditActor(auditRow({ userId: OTHER, userName: "admin" }), VIEWER);

    expect(auditActorPhrase(actor)).toBe("by admin, on your account");
  });
});

describe("entity names and links", () => {
  it("prefers the recorded name and survives deletes", () => {
    const row = auditRow({ entityName: "report.pdf", eventCode: AuditEventCode.FileSoftDeleted });

    expect(auditEntityName(row)).toBe("report.pdf");
    expect(auditEntityRoute(row, VIEWER, new Set())).toBeNull();
  });

  it("names a move after the file, not the destination", () => {
    const row = auditRow({
      eventCode: AuditEventCode.FileMoved,
      metadata: JSON.stringify({ name: "song.mp3", new: "Music", old: "Inbox" }),
    });

    expect(auditEntityName(row)).toBe("song.mp3");
  });

  it("links folders to the explorer but never files, which have no page", () => {
    const version = auditRow({
      entityType: EntityType.FileVersion,
      eventCode: AuditEventCode.FileVersionCreated,
      parentEntityId: "f1",
    });
    const folder = auditRow({
      entityType: EntityType.Directory,
      eventCode: AuditEventCode.DirectoryCreated,
    });

    expect(auditEntityRoute(version, VIEWER, new Set())).toBeNull();
    expect(auditEntityRoute(folder, VIEWER, new Set())).toEqual({
      name: "dashboard",
      params: { dirId: folder.entityId },
    });
  });

  it("renders deleted or foreign entities as plain text", () => {
    const created = auditRow();
    const gone = collectGoneEntityIds([
      auditRow({ entityId: created.entityId, eventCode: AuditEventCode.FileDeletedPermanently }),
    ]);

    expect(auditEntityRoute(created, VIEWER, gone)).toBeNull();
    expect(auditEntityRoute(auditRow({ ownerId: OTHER }), VIEWER, new Set())).toBeNull();
  });
});
