import type { LocationQuery, LocationQueryRaw } from "vue-router";

import type { AuditFilterRequest, AuditLogQuery } from "@/api/activity";
import type { AdminAuditFilterRequest, AdminAuditQuery } from "@/api/adminAudit";

import { AuditEventCode } from "@/enums/audit-event-code";
import { AuditSortBy } from "@/enums/audit-sort-by";
import { EntityType } from "@/enums/entity-type";
import { LogSource } from "@/enums/log-source";
import { OperationType } from "@/enums/operation-type";
import { SortDirection } from "@/enums/SortDirection";

// Every audit filter, as both pages and every deep link share it. A missing value means
// "no filter on that field".
export interface AuditFilters {
  user?: string;
  owner?: string;
  entityType?: EntityType;
  entityId?: string;
  events: AuditEventCode[];
  operation?: OperationType;
  source?: LogSource;
  ip?: string;
  from?: string;
  to?: string;
  correlationId?: string;
  page: number;
}

export const AUDIT_PAGE_SIZE = 20;

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu;

const MAX_IP_LENGTH = 45;

export const emptyAuditFilters = (): AuditFilters => ({ events: [], page: 1 });

const firstValue = (raw: LocationQuery[string] | undefined): string | undefined => {
  const value = Array.isArray(raw) ? raw[0] : raw;
  if (typeof value !== "string") return undefined;

  const trimmed = value.trim();

  return trimmed || undefined;
};

const allValues = (raw: LocationQuery[string] | undefined): string[] => {
  if (Array.isArray(raw)) return raw.filter((value): value is string => typeof value === "string");
  if (typeof raw === "string") return [raw];

  return [];
};

const parseGuid = (raw: LocationQuery[string] | undefined): string | undefined => {
  const value = firstValue(raw);
  if (!value || !GUID.test(value)) return undefined;

  return value.toLowerCase();
};

const isEnumMember = <T extends number>(
  enumObject: Record<string, string | number>,
  value: number,
) => Object.values(enumObject).includes(value as T);

const parseEnum = <T extends number>(
  raw: LocationQuery[string] | undefined,
  enumObject: Record<string, string | number>,
): T | undefined => {
  const value = firstValue(raw);
  if (value === undefined || !/^\d+$/u.test(value)) return undefined;

  const number = Number(value);
  if (!isEnumMember<T>(enumObject, number)) return undefined;

  return number as T;
};

const parseDate = (raw: LocationQuery[string] | undefined): string | undefined => {
  const value = firstValue(raw);
  if (!value) return undefined;

  const time = Date.parse(value);
  if (!Number.isFinite(time)) return undefined;

  return new Date(time).toISOString();
};

const parsePage = (raw: LocationQuery[string] | undefined): number => {
  const value = firstValue(raw);
  if (!value || !/^\d+$/u.test(value)) return 1;

  return Math.max(1, Number(value));
};

const parseIp = (raw: LocationQuery[string] | undefined): string | undefined => {
  const value = firstValue(raw);
  if (!value || value.length > MAX_IP_LENGTH) return undefined;

  return value;
};

const parseEvents = (raw: LocationQuery[string] | undefined): AuditEventCode[] => {
  const codes = new Set<AuditEventCode>();

  for (const value of allValues(raw)) {
    const code = parseEnum<AuditEventCode>(value, AuditEventCode);
    if (code !== undefined) codes.add(code);
  }

  return [...codes].sort((a, b) => a - b);
};

// One parser for every consumer, so a malformed link degrades to "no filter" instead of a
// request the backend rejects.
export const parseAuditQuery = (query: LocationQuery): AuditFilters => {
  const filters: AuditFilters = {
    correlationId: parseGuid(query.correlationId),
    entityId: parseGuid(query.entityId),
    entityType: parseEnum<EntityType>(query.entityType, EntityType),
    events: parseEvents(query.event),
    from: parseDate(query.from),
    ip: parseIp(query.ip),
    operation: parseEnum<OperationType>(query.operation, OperationType),
    owner: parseGuid(query.owner),
    page: parsePage(query.page),
    source: parseEnum<LogSource>(query.source, LogSource),
    to: parseDate(query.to),
    user: parseGuid(query.user),
  };

  // A reversed range would be rejected by the API; dropping the end keeps the start useful.
  if (filters.from && filters.to && filters.to < filters.from) filters.to = undefined;

  return filters;
};

// Producers emit plain strings; the page is only written when it is past the first.
export const toAuditRouteQuery = (filters: Partial<AuditFilters>): LocationQueryRaw => {
  const query: LocationQueryRaw = {};

  if (filters.user) query.user = filters.user;
  if (filters.owner) query.owner = filters.owner;
  if (filters.entityType !== undefined) query.entityType = String(filters.entityType);
  if (filters.entityId) query.entityId = filters.entityId;
  if (filters.events?.length) query.event = filters.events.map(String);
  if (filters.operation !== undefined) query.operation = String(filters.operation);
  if (filters.source !== undefined) query.source = String(filters.source);
  if (filters.ip) query.ip = filters.ip;
  if (filters.from) query.from = filters.from;
  if (filters.to) query.to = filters.to;
  if (filters.correlationId) query.correlationId = filters.correlationId;
  if (filters.page && filters.page > 1) query.page = String(filters.page);

  return query;
};

// The personal endpoint has no event-code or correlation filter, so those stay unused there.
export const toPersonalActivityQuery = (filters: AuditFilters): AuditLogQuery => ({
  after: filters.from,
  before: filters.to,
  entityId: filters.entityId,
  entityType: filters.entityType,
  ipAddress: filters.ip,
  operationType: filters.operation,
  page: filters.page,
  pageSize: AUDIT_PAGE_SIZE,
  sortBy: AuditSortBy.Timestamp,
  sortDirection: SortDirection.Desc,
  source: filters.source,
});

export const toAuditFilterRequest = (filters: AuditFilters): AuditFilterRequest => ({
  correlationId: filters.correlationId,
  entityId: filters.entityId,
  entityType: filters.entityType,
  eventCodes: filters.events.length ? filters.events : undefined,
  from: filters.from,
  ipAddress: filters.ip,
  operationType: filters.operation,
  source: filters.source,
  to: filters.to,
});

export const toPersonalExportFilters = (filters: AuditFilters): AuditFilterRequest => ({
  entityId: filters.entityId,
  entityType: filters.entityType,
  from: filters.from,
  ipAddress: filters.ip,
  operationType: filters.operation,
  source: filters.source,
  to: filters.to,
});

export const toAdminAuditFilterRequest = (filters: AuditFilters): AdminAuditFilterRequest => ({
  ...toAuditFilterRequest(filters),
  ownerId: filters.owner,
  userId: filters.user,
});

export const toAdminAuditQuery = (filters: AuditFilters): AdminAuditQuery => ({
  ...toAdminAuditFilterRequest(filters),
  page: filters.page,
  pageSize: AUDIT_PAGE_SIZE,
  sortBy: AuditSortBy.Timestamp,
  sortDirection: SortDirection.Desc,
});

// The activity calendar buckets by UTC day, so a day filter covers that same UTC day.
export const utcDayRange = (year: number, month: number, day: number) => ({
  from: new Date(Date.UTC(year, month, day)).toISOString(),
  to: new Date(Date.UTC(year, month, day + 1) - 1).toISOString(),
});

// Fields a page can show; the rest are ignored when counting active filters.
export type AuditFilterField = Exclude<keyof AuditFilters, "page">;

export const PERSONAL_FILTER_FIELDS: AuditFilterField[] = [
  "entityType",
  "entityId",
  "operation",
  "source",
  "from",
  "to",
];

export const ADMIN_FILTER_FIELDS: AuditFilterField[] = [
  "user",
  "owner",
  "entityType",
  "entityId",
  "events",
  "operation",
  "source",
  "ip",
  "from",
  "to",
  "correlationId",
];

export const hasFilterValue = (filters: AuditFilters, field: AuditFilterField): boolean => {
  if (field === "events") return filters.events.length > 0;

  return filters[field] !== undefined;
};
