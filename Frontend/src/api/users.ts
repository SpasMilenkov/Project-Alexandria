import type { OnboardingStep } from "@/enums";
import type { CreateUserSchema, UpdateProfileSchema, UpdateUserSchema } from "@/schemas/user";
import type { UserDetailsDto, UserProfile, UserProfileStats, UserQueryDto } from "@/types/user";

import { logger } from "@/utils/logger";
import { gbToBytes } from "@/utils/size";

import type { PaginatedResponse } from "./directory";

import { api } from "./client";

export const userApi = {
  createUser: (query: CreateUserSchema) => {
    const { storageQuotaGb, ...rest } = query;
    return api.post<UserDetailsDto>("/users", {
      ...rest,
      storageQuotaBytes: storageQuotaGb === undefined ? undefined : gbToBytes(storageQuotaGb),
    });
  },

  deleteAvatar: async (): Promise<void> => {
    await api.delete("/users/profile/avatar");
  },

  deleteUsers: async (userIds: string[]): Promise<void> => {
    await api.delete("/users", {
      body: { userIds },
    });
  },

  finishTour: async () => {
    logger.debug("HERE I AM ");
    await api.patch("/users/finish-tour", {});
  },

  getFileCountPerUser: (userId: string, deletedOnly: boolean): Promise<number> =>
    api.get<number>("/users/file-count", {
      params: {
        deletedOnly,
        userId,
      },
    }),

  getFileSizePerUser: (userId: string, deletedOnly: boolean): Promise<number> =>
    api.get<number>("/users/file-size", {
      params: {
        deletedOnly,
        userId,
      },
    }),

  getOnboardingStep: async (): Promise<OnboardingStep> => {
    const result = await api.get<{ onboardingStep: OnboardingStep }>("users/onboarding");

    return result.onboardingStep;
  },

  getUserProfile: (): Promise<UserProfile> => api.get<UserProfile>("/users/profile"),

  getUserProfileStats: (): Promise<UserProfileStats> =>
    api.get<UserProfileStats>("/users/profile/stats"),

  getUsers: (query: UserQueryDto): Promise<PaginatedResponse<UserDetailsDto>> =>
    api.get<PaginatedResponse<UserDetailsDto>>("/users", {
      params: query,
    }),

  restrictUser: async (userId: string, lockoutEndDate: string): Promise<void> => {
    await api.patch("/users/restrict", {
      lockoutEndDate,
      userId,
    });
  },

  setupProfile: async () => {
    await api.patch("/users/setup-profile");
  },

  updateProfile: (payload: UpdateProfileSchema): Promise<UserProfile> =>
    api.patch<UserProfile>("/users/profile", payload),

  uploadAvatar: (image: Blob): Promise<{ avatarUrl: string }> => {
    const form = new FormData();
    form.append("file", image, "avatar");

    return api.post<{ avatarUrl: string }>("/users/profile/avatar", form);
  },

  updateUser: (userId: string, payload: UpdateUserSchema): Promise<UserDetailsDto> => {
    const { storageQuotaGb, ...rest } = payload;
    return api.patch<UserDetailsDto>(`/users/${userId}`, {
      payload: {
        ...rest,
        storageQuotaBytes: storageQuotaGb === undefined ? undefined : gbToBytes(storageQuotaGb),
      },
    });
  },
};
