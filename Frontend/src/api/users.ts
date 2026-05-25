import type { OnboardingStep } from "@/enums";
import type { CreateUserSchema } from "@/schemas/user";
import type { UpdateUserDto, UserDetailsDto, UserProfile, UserQueryDto } from "@/types/user";

import { logger } from "@/utils/logger";

import type { PaginatedResponse } from "./directory";

import {apiClient} from "./client";

export const userApi = {
  createUser: async (query: CreateUserSchema) => {
    const result = await apiClient.post<UserDetailsDto>("/users", {
      ...query,
    });

    return result.data;
  },

  deleteUsers: async (userIds: string[]): Promise<void> => {
    await apiClient.delete("/users", {
      data: { userIds },
    });
  },

  finishTour: async () => {
    logger.debug("HERE I AM ");
    await apiClient.patch("/users/finish-tour", {});
  },

  getFileCountPerUser: async (userId: string, deletedOnly: boolean): Promise<number> => {
    const result = await apiClient.get<number>("/users/file-count", {
      params: {
        deletedOnly,
        userId,
      },
    });
    return result.data;
  },

  getFileSizePerUser: async (userId: string, deletedOnly: boolean): Promise<number> => {
    const result = await apiClient.get<number>("/users/file-size", {
      params: {
        deletedOnly,
        userId,
      },
    });
    return result.data;
  },

  getOnboardingStep: async (): Promise<OnboardingStep> => {
    const result = await apiClient.get<{ onboardingStep: OnboardingStep }>("users/onboarding");

    return result.data.onboardingStep;
  },

  getUserProfile: async (): Promise<UserProfile> => {
    const result = await apiClient.get<UserProfile>("/users/profile");

    return result.data;
  },

  getUsers: async (query: UserQueryDto): Promise<PaginatedResponse<UserDetailsDto>> => {
    const result = await apiClient.get<PaginatedResponse<UserDetailsDto>>("/users", {
      params: query,
    });
    return result.data;
  },

  restrictUser: async (userId: string, lockoutEndDate: string): Promise<void> => {
    await apiClient.patch("/users/restrict", {
      lockoutEndDate,
      userId,
    });
  },

  setupProfile: async () => {
    await apiClient.patch("/users/setup-profile");
  },

  updateUser: async (userId: string, payload: UpdateUserDto): Promise<UserDetailsDto> => {
    const result = await apiClient.patch<UserDetailsDto>(`/users/${userId}`, { payload });
    return result.data;
  },
};
