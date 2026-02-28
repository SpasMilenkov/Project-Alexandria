import type { CreateUserSchema } from "@/schemas/user";
import type { UpdateUserDto, UserDetailsDto, UserQueryDto } from "@/types/user";

import type { PaginatedResponse } from "./directory";

import apiClient from "./client";

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

  updateUser: async (userId: string, payload: UpdateUserDto): Promise<UserDetailsDto> => {
    const result = await apiClient.patch<UserDetailsDto>("/users", {
      payload,
      userId,
    });
    return result.data;
  },
};
