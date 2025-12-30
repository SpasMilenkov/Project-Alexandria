import type { SortDirection } from "@/enums/SortDirection";
import apiClient from "./client";
import type { PaginatedResponse } from "./directory";


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
  userId: string;
  entityId: string;
  operationType: OperationType;
  entityType: EntityType;
  ipAddress: string;
  before: Date;
  after: Date;
}

export const activityApi = {
  getUserActivity: async (
    query: AuditLogQuery
  ): Promise<PaginatedResponse<AuditLogResult>> => {
    const result = await apiClient.get<PaginatedResponse<AuditLogResult>>("/activity/user", {
      params: {
        page: query.page,
        pageSize: query.pageSize,
        sortBy: query.sortBy,
        sortDirection: query.sortDirection,
        userId: query.userId,
        entityId: query.entityId,
        operationType: query.operationType,
        entityType: query.entityType,
        ipAddress: query.ipAddress,
        before: query.before,
        after: query.after,
      },
    });
    return result.data
  },
};
