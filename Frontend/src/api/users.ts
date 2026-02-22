import type { UpdateUserDto, UserDetailsDto, UserQueryDto } from "@/types/user";
import apiClient from "./client";

import type { PaginatedResponse } from "./directory";
import type { CreateUserSchema } from "@/schemas/user";

export const userApi = {
  createUser: async (query: CreateUserSchema) => {
    const result = await apiClient.post<UserDetailsDto>("/users", {
      ...query,
    });

    return result.data;
  },

  getUsers: async (
    query: UserQueryDto,
  ): Promise<PaginatedResponse<UserDetailsDto>> => {
    const result = await apiClient.get<PaginatedResponse<UserDetailsDto>>(
      "/users",
      {
        params: query,
      },
    );
    return result.data;
  },

  updateUser: async (
    userId: string,
    payload: UpdateUserDto,
  ): Promise<UserDetailsDto> => {
    const result = await apiClient.patch<UserDetailsDto>("/users", {
      userId,
      payload,
    });
    return result.data;
  },

  restrictUser: async (
    userId: string,
    lockoutEndDate: string,
  ): Promise<void> => {
    await apiClient.patch("/users/restrict", {
      userId,
      lockoutEndDate,
    });
  },

  getFileCountPerUser: async (
    userId: string,
    deletedOnly: boolean,
  ): Promise<number> => {
    const result = await apiClient.get<number>("/users/file-count", {
      params: {
        userId,
        deletedOnly,
      },
    });
    return result.data;
  },

  getFileSizePerUser: async (
    userId: string,
    deletedOnly: boolean,
  ): Promise<number> => {
    const result = await apiClient.get<number>("/users/file-size", {
      params: {
        userId,
        deletedOnly,
      },
    });
    return result.data;
  },

  deleteUsers: async (userIds: string[]): Promise<void> => {
    await apiClient.delete("/users", {
      data: { userIds },
    });
  },
};
