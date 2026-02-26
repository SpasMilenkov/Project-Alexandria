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
  File,
  Directory,
  Tag,
  User,
}

export interface AuditLogResult {
  operationType: OperationType;
  entityTYpe: EntityType;
  userId: string;
  entityId: string;
  description: string;
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
