import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import type { ChangePasswordSchema, CreateUserSchema, UpdateUserSchema } from "@/schemas/user";

import { authApi } from "@/api/auth";
import { userApi } from "@/api/users";
import { USER_QUERY_KEYS } from "@/queries/user";

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
    mutation: ({ userId, lockoutEndDate }: { userId: string; lockoutEndDate: string }) =>
      userApi.restrictUser(userId, lockoutEndDate),
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

export const changeInitialPassword = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (payload: ChangePasswordSchema) => authApi.changeInitialPassword(payload),
    onSuccess() {
      queryCache.invalidateQueries({ key: USER_QUERY_KEYS.getOnboardingStep() });
    },
  });
});

export const setupProfile = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: () => userApi.setupProfile(),
    onSuccess() {
      queryCache.invalidateQueries({ key: USER_QUERY_KEYS.getOnboardingStep() });
    },
  });
});

export const finishTour = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: () => userApi.finishTour(),
    onSuccess() {
      queryCache.invalidateQueries({ key: USER_QUERY_KEYS.getOnboardingStep() });
    },
  });
});
