import { describe, expect, it } from "vitest";

import { AuditEventCode, EntityType, LogSource, OperationType } from "@/enums";
import {
  parseAuditQuery,
  toAdminAuditQuery,
  toAuditRouteQuery,
  toPersonalActivityQuery,
  toPersonalExportFilters,
  utcDayRange,
} from "@/features/audit/query";

const GUID = "6f1c2b9e-4d3a-4f6b-9a8e-2c1d0e9f8a7b";

describe("parseAuditQuery", () => {
  it("reads every documented parameter", () => {
    const filters = parseAuditQuery({
      correlationId: GUID,
      entityId: GUID.toUpperCase(),
      entityType: String(EntityType.Directory),
      event: [String(AuditEventCode.FileMoved), String(AuditEventCode.FileCreated)],
      from: "2026-11-01T00:00:00.000Z",
      ip: "192.168.1.20",
      operation: String(OperationType.Update),
      owner: GUID,
      page: "3",
      source: String(LogSource.System),
      to: "2026-11-02T00:00:00.000Z",
      user: GUID,
    });

    expect(filters).toEqual({
      correlationId: GUID,
      entityId: GUID,
      entityType: EntityType.Directory,
      events: [AuditEventCode.FileCreated, AuditEventCode.FileMoved],
      from: "2026-11-01T00:00:00.000Z",
      ip: "192.168.1.20",
      operation: OperationType.Update,
      owner: GUID,
      page: 3,
      source: LogSource.System,
      to: "2026-11-02T00:00:00.000Z",
      user: GUID,
    });
  });

  it("drops malformed values instead of sending them to the API", () => {
    const filters = parseAuditQuery({
      entityId: "not-a-guid",
      entityType: "99",
      event: ["2", "abc", "999", "2"],
      from: "yesterday",
      operation: "-1",
      page: "0",
      source: "0",
      user: "",
    });

    expect(filters).toEqual({ events: [AuditEventCode.FileMoved], page: 1 });
  });

  it("drops the end of a reversed date range", () => {
    const filters = parseAuditQuery({
      from: "2026-11-05T00:00:00.000Z",
      to: "2026-11-01T00:00:00.000Z",
    });

    expect(filters.from).toBe("2026-11-05T00:00:00.000Z");
    expect(filters.to).toBeUndefined();
  });

  it("round-trips through the route query", () => {
    const filters = parseAuditQuery({
      entityType: "0",
      event: ["1", "4"],
      page: "2",
      user: GUID,
    });

    expect(parseAuditQuery(toAuditRouteQuery(filters) as never)).toEqual(filters);
  });

  it("leaves the first page and empty filters out of the URL", () => {
    expect(toAuditRouteQuery({ events: [], page: 1 })).toEqual({});
  });
});

describe("request mapping", () => {
  const filters = parseAuditQuery({
    correlationId: GUID,
    entityType: "0",
    event: ["1"],
    from: "2026-11-01T00:00:00.000Z",
    to: "2026-11-02T00:00:00.000Z",
    user: GUID,
  });

  it("maps dates onto after/before for the personal history", () => {
    const query = toPersonalActivityQuery(filters);

    expect(query.after).toBe("2026-11-01T00:00:00.000Z");
    expect(query.before).toBe("2026-11-02T00:00:00.000Z");
    expect(query).not.toHaveProperty("eventCodes");
  });

  it("keeps admin-only filters out of the personal export", () => {
    const request = toPersonalExportFilters(filters);

    expect(request).not.toHaveProperty("userId");
    expect(request.eventCodes).toBeUndefined();
    expect(request.correlationId).toBeUndefined();
  });

  it("passes every filter to the admin search", () => {
    const query = toAdminAuditQuery(filters);

    expect(query.userId).toBe(GUID);
    expect(query.eventCodes).toEqual([AuditEventCode.FileRenamed]);
    expect(query.correlationId).toBe(GUID);
    expect(query.page).toBe(1);
  });
});

describe("utcDayRange", () => {
  it("covers one whole UTC day, including the last day of a leap February", () => {
    expect(utcDayRange(2028, 1, 29)).toEqual({
      from: "2028-02-29T00:00:00.000Z",
      to: "2028-02-29T23:59:59.999Z",
    });
  });
});
