import { userApi } from "@/api/users";
import type { UserQueryApiState } from "@/schemas/user";
import { defineQueryOptions } from "@pinia/colada";

const normalizeUserFilters = (filters: UserQueryApiState) => {
  const { page, pageSize, sortBy, sortDirection, ...rest } = filters;
  return {
    page,
    pageSize,
    sortBy,
    sortDirection,
    filters: Object.fromEntries(
      Object.entries(rest).filter(([_, v]) => v !== undefined && v !== null),
    ),
  };
};

export const USER_QUERY_KEYS = {
  root: ["users"] as const,
  getUsers: (filters: UserQueryApiState) => [
    ...USER_QUERY_KEYS.root,
    "list",
    normalizeUserFilters(filters),
  ],
  getUserCount: ({
    userId,
    deletedOnly,
  }: {
    userId: string;
    deletedOnly: boolean;
  }) => [...USER_QUERY_KEYS.root, "file-count", userId, deletedOnly],
  getUserStorage: ({
    userId,
    deletedOnly,
  }: {
    userId: string;
    deletedOnly: boolean;
  }) => [...USER_QUERY_KEYS.root, "file-storage", userId, deletedOnly],
};

export const getUsers = defineQueryOptions((filters: UserQueryApiState) => ({
  key: USER_QUERY_KEYS.getUsers(filters),
  query: () => userApi.getUsers(filters),
  staleTime: 30_000,
}));

export const getUserCount = defineQueryOptions(
  ({ userId, deletedOnly }: { userId: string; deletedOnly: boolean }) => ({
    key: USER_QUERY_KEYS.getUserCount({ userId, deletedOnly }),
    query: () => userApi.getFileCountPerUser(userId, deletedOnly),
    staleTime: 30_000,
  }),
);

export const getUserStorage = defineQueryOptions(
  ({ userId, deletedOnly }: { userId: string; deletedOnly: boolean }) => ({
    key: USER_QUERY_KEYS.getUserStorage({ userId, deletedOnly }),
    query: () => userApi.getFileSizePerUser(userId, deletedOnly),
    staleTime: 30_000,
  }),
);
