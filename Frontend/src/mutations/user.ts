import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import type {
  ChangePasswordSchema,
  CreateUserSchema,
  UpdateProfileSchema,
  UpdateUserSchema,
} from "@/schemas/user";
import type { UserProfile } from "@/types/user";

import { authApi } from "@/api/auth";
import { userApi } from "@/api/users";
import { USER_QUERY_KEYS } from "@/queries/user";
import { useAuthStore } from "@/stores/auth";

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

export const useUpdateProfile = defineMutation(() => {
  const queryCache = useQueryCache();
  const authStore = useAuthStore();

  return useMutation({
    mutation: (payload: UpdateProfileSchema) => userApi.updateProfile(payload),
    onSuccess(profile) {
      queryCache.setQueryData(USER_QUERY_KEYS.getProfile(), profile);
      authStore.applyProfile({ name: profile.username });
    },
    onSettled() {
      // Admin lists show usernames too.
      queryCache.invalidateQueries({ key: USER_QUERY_KEYS.root });
    },
  });
});

const patchCachedAvatar = (
  queryCache: ReturnType<typeof useQueryCache>,
  avatarUrl: string | null,
) => {
  const cached = queryCache.getQueryData<UserProfile>(USER_QUERY_KEYS.getProfile());
  if (!cached) return;

  queryCache.setQueryData(USER_QUERY_KEYS.getProfile(), { ...cached, avatarUrl });
};

export const useUploadAvatar = defineMutation(() => {
  const queryCache = useQueryCache();
  const authStore = useAuthStore();

  return useMutation({
    mutation: (image: Blob) => userApi.uploadAvatar(image),
    onSuccess({ avatarUrl }) {
      patchCachedAvatar(queryCache, avatarUrl);
      authStore.applyProfile({ avatarUrl });
    },
  });
});

export const useRemoveAvatar = defineMutation(() => {
  const queryCache = useQueryCache();
  const authStore = useAuthStore();

  return useMutation({
    mutation: () => userApi.deleteAvatar(),
    onSuccess() {
      patchCachedAvatar(queryCache, null);
      authStore.applyProfile({ avatarUrl: null });
    },
  });
});
