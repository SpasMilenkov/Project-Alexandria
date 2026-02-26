import { defineQueryOptions } from "@pinia/colada";

import type { UserQueryApiState } from "@/schemas/user";

import { userApi } from "@/api/users";

const normalizeUserFilters = (filters: UserQueryApiState) => {
  const { page, pageSize, sortBy, sortDirection, ...rest } = filters;
  return {
    filters: Object.fromEntries(
      Object.entries(rest).filter(([_, v]) => v !== undefined && v !== null),
    ),
    page,
    pageSize,
    sortBy,
    sortDirection,
  };
};

export const USER_QUERY_KEYS = {
  getUserCount: ({ userId, deletedOnly }: { userId: string; deletedOnly: boolean }) => [
    ...USER_QUERY_KEYS.root,
    "file-count",
    userId,
    deletedOnly,
  ],
  getUserStorage: ({ userId, deletedOnly }: { userId: string; deletedOnly: boolean }) => [
    ...USER_QUERY_KEYS.root,
    "file-storage",
    userId,
    deletedOnly,
  ],
  getUsers: (filters: UserQueryApiState) => [
    ...USER_QUERY_KEYS.root,
    "list",
    normalizeUserFilters(filters),
  ],
  root: ["users"] as const,
};

export const getUsers = defineQueryOptions((filters: UserQueryApiState) => ({
  key: USER_QUERY_KEYS.getUsers(filters),
  query: () => userApi.getUsers(filters),
  staleTime: 30_000,
}));

export const getUserCount = defineQueryOptions(
  ({ userId, deletedOnly }: { userId: string; deletedOnly: boolean }) => ({
    key: USER_QUERY_KEYS.getUserCount({ deletedOnly, userId }),
    query: () => userApi.getFileCountPerUser(userId, deletedOnly),
    staleTime: 30_000,
  }),
);

export const getUserStorage = defineQueryOptions(
  ({ userId, deletedOnly }: { userId: string; deletedOnly: boolean }) => ({
    key: USER_QUERY_KEYS.getUserStorage({ deletedOnly, userId }),
    query: () => userApi.getFileSizePerUser(userId, deletedOnly),
    staleTime: 30_000,
  }),
);
