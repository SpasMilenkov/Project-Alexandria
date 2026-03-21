import type { SortDirection } from "@/enums/SortDirection";

import type { PaginatedResponse } from "./directory";

import apiClient from "./client";

export enum OperationType {
  Read = 0,
  Create = 1,
  Update = 2,
  Delete = 3,
  Login = 4,
  Logout = 5,
}

export enum LogSource {
  API = 1,
  Trigger = 2,
  System = 3,
}

export enum EntityType {
  File = 0,
  FileVersion = 1,
  Directory = 2,
  Tag = 3,
  User = 4,
  Preview = 5,
}

export enum AuditEventCode {
  // File events
  FileCreated = 0,
  FileRenamed = 1,
  FileMoved = 2,
  FileSoftDeleted = 3,
  FileRestored = 4,
  FileDeletedPermanently = 5,
  // FileVersion events
  FileVersionCreated = 6,
  FileVersionSoftDeleted = 7,
  FileVersionRestored = 8,
  FileVersionDeletedPermanently = 9,
  // Directory events
  DirectoryCreated = 10,
  DirectoryRenamed = 11,
  DirectoryMoved = 12,
  DirectorySoftDeleted = 13,
  DirectoryRestored = 14,
  DirectoryDeletedPermanently = 15,
  // Preview events
  PreviewCreated = 16,
  PreviewSoftDeleted = 17,
  PreviewRestored = 18,
  PreviewDeletedPermanently = 19,
  // Tag events
  TagCreated = 20,
  TagRenamed = 21,
  TagUpdated = 22,
  TagSoftDeleted = 23,
  TagRestored = 24,
  TagDeletedPermanently = 25,
  // User events
  UserCreated = 26,
  UserDeleted = 27,
  UserLogin = 28,
  UserEmailChanged = 29,
  UserPasswordChanged = 30,
  UserLockedOut = 31,
  UserLockoutLifted = 32,
  UserTwoFactorEnabled = 33,
  UserTwoFactorDisabled = 34,
  // Sentinel
  Unknown = 35,
}

export interface ActivityQuery {
  startDate: Date;
  endDate: Date;
  entityType?: EntityType;
  operationType?: OperationType;
}

export interface ActivitySummary {
  totalOperations: number;
  countPerOperation: Partial<Record<OperationType, number>>;
}

export interface ActivityStatisticsOverview {
  totalActivity: number;
  activityPerDay: Record<number, ActivitySummary>;
}

export interface AuditLogResult {
  operationType: OperationType;
  entityType: EntityType;
  userId: string;
  entityId: string;
  eventCode: AuditEventCode;
  metadata?: string;
  timestamp: string;
  logSource: LogSource;
}

export interface AuditLogQuery {
  page: number;
  pageSize: number;
  sortBy: string;
  sortDirection: SortDirection;

  userId?: string;
  entityId?: string;
  operationType?: OperationType;
  entityType?: EntityType;
  ipAddress?: string;
  before?: Date;
  after?: Date;
}

export const activityApi = {
  getActivitySummary: async (query: ActivityQuery): Promise<ActivityStatisticsOverview> => {
    const result = await apiClient.get<ActivityStatisticsOverview>("/activity/summary", {
      params: {
        endDate: query.endDate.toISOString(),
        entityType: query.entityType ?? undefined,
        operationType: query.operationType ?? undefined,
        startDate: query.startDate.toISOString(),
      },
    });
    return result.data;
  },
  getUserActivity: async (query: AuditLogQuery): Promise<PaginatedResponse<AuditLogResult>> => {
    const result = await apiClient.get<PaginatedResponse<AuditLogResult>>("/activity/user", {
      params: {
        after: query.after,
        before: query.before,
        entityId: query.entityId,
        entityType: query.entityType,
        ipAddress: query.ipAddress,
        operationType: query.operationType,
        page: query.page,
        pageSize: query.pageSize,
        sortBy: query.sortBy,
        sortDirection: query.sortDirection,
        userId: query.userId,
      },
    });
    return result.data;
  },
};
