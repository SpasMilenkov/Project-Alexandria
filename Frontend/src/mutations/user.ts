import { userApi } from "@/api/users";
import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";
import type { CreateUserSchema, UpdateUserSchema } from "@/schemas/user";
import { USER_QUERY_KEYS } from "@/queries/user";
import { authApi } from "@/api/auth";

export const useCreateUser = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (data: CreateUserSchema) => userApi.createUser(data),
    onSuccess() {
      queryCache.invalidateQueries({ key: USER_QUERY_KEYS.root });
    },
  });
});

export const useUpdateUser = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ userId, data }: { userId: string; data: UpdateUserSchema }) =>
      userApi.updateUser(userId, data),
    onSuccess() {
      queryCache.invalidateQueries({ key: USER_QUERY_KEYS.root });
    },
  });
});

export const useRestrictUser = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({
      userId,
      lockoutEndDate,
    }: {
      userId: string;
      lockoutEndDate: string;
    }) => userApi.restrictUser(userId, lockoutEndDate),
    onSuccess() {
      queryCache.invalidateQueries({ key: USER_QUERY_KEYS.root });
    },
  });
});

export const useDeleteUsers = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (userIds: string[]) => userApi.deleteUsers(userIds),
    onSuccess() {
      queryCache.invalidateQueries({ key: USER_QUERY_KEYS.root });
    },
  });
});
