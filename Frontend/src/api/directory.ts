import type { SortBy } from "@/enums/SortBy";
import type { SortDirection } from "@/enums/SortDirection";
import type {
  CreateDirectorySchema,
  DeleteDirectorySchema,
  UpdateDirectorySchema,
} from "@/schemas/directory";
import type { PaginationParams } from "@/types/pagination-params";

import apiClient from "./client";

// Response Types
export interface UserDto {
  id: string;
  email: string;
  name: string;
}

export interface FileSummary {
  id: string;
  fileName: string;
  mimeType: string;
  hasPreview: boolean;
  path: string;
}

export interface DirectorySummaryDto {
  id: string;
  name: string;
  parentId: string;
  createdAt: string;
  updatedAt: string;
  ownerUserDto: UserDto;
}

export interface RootContentDto {
  directories: DirectorySummaryDto;
  files: FileSummary;
}

export interface DirectoryDto {
  id: string;
  name: string;
  parentId: string;
  createdAt: string;
  updatedAt: string;
  ownerUserDto: UserDto;
  files: FileSummary[];
  directories: DirectorySummaryDto[];
}

export interface CreateDirectoryResponse {
  directory: DirectorySummaryDto;
}

export interface UpdateDirectoryResponse {
  directory: DirectoryDto;
}

export interface GetDirectoryResponse {
  directory: DirectorySummaryDto;
}

export interface GetDirectoryWithChildrenResponse {
  directory: DirectoryDto;
}

export interface ListRootDirectoriesResponse {
  directories: string[];
}

// Export enum DirectorySortBy {
//   Name = 0,
//   CreatedAt = 1,
//   UpdatedAt = 2,
// }

// Export enum SortDirection {
//   Asc = 0,
//   Desc = 1,
// }

// Main request interface
export interface SearchDirectoryRequest {
  // Identity & structure
  directoryId?: string | null;
  parentDirectoryId?: string | null;

  // Text search
  nameContains?: string | null;

  // Ownership & sharing
  ownerId?: string | null;
  isShared?: boolean | null;

  // Time filters (ISO 8601 strings for API calls)
  createdAfter?: string | null;
  createdBefore?: string | null;
  updatedAfter?: string | null;
  updatedBefore?: string | null;
  deletedAt?: string | null;
  deletedAfter?: string | null;
  deletedBefore?: string | null;

  // Contents
  hasFiles?: boolean | null;
  hasSubdirectories?: boolean | null;

  // Flags
  isDeleted?: boolean;
  isStarred?: boolean;

  // Paging & sorting
  page?: number;
  pageSize?: number;
  sortBy?: SortBy;
  sortDirection?: SortDirection;
}

// Response types
export interface UserDto {
  id: string;
  email: string;
  name: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  currentPage: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export const directoryApi = {
  // Create a new directory
  createDirectory: async (data: CreateDirectorySchema): Promise<CreateDirectoryResponse> => {
    const response = await apiClient.post<CreateDirectoryResponse>("/directories", data);
    return response.data;
  },

  uploadDirectory: async (
    parentId: string | null,
    paths: string[],
  ): Promise<Record<string, string | null>> => {
    const response = await apiClient.post<Record<string, string | null>>(
      "/directories/create-subtree",
      {
        parentId,
        paths,
      },
    );
    return response.data;
  },

  // Get directory by ID
  getDirectory: async (directoryId: string): Promise<GetDirectoryResponse> => {
    const response = await apiClient.get<GetDirectoryResponse>("/directories", {
      params: { directoryId },
    });
    return response.data;
  },

  getRooSubDirectories: async (
    paginationParams: PaginationParams,
  ): Promise<PaginatedResponse<DirectorySummaryDto>> => {
    const response = await apiClient.get<PaginatedResponse<DirectorySummaryDto>>(
      "/directories/root",
      {
        params: {
          page: paginationParams.page,
          pageSize: paginationParams.pageSize,
          sortBy: paginationParams.SortBy,
          sortDirection: paginationParams.sortDirection,
        },
      },
    );
    return response.data;
  },
  getSubDirectories: async (
    directoryId: string,
    paginationParams: PaginationParams,
  ): Promise<PaginatedResponse<DirectorySummaryDto>> => {
    const response = await apiClient.get<PaginatedResponse<DirectorySummaryDto>>(
      "/directories/sub",
      {
        params: {
          directoryId,
          page: paginationParams.page,
          pageSize: paginationParams.pageSize,
          sortBy: paginationParams.SortBy,
          sortDirection: paginationParams.sortDirection,
        },
      },
    );
    return response.data;
  },

  getDirectoryPath: async (id: string): Promise<{ pathParts: { id: string; name: string }[] }> => {
    const response = await apiClient.get<{
      pathParts: { id: string; name: string }[];
    }>(`/directories/path/${id}`);

    return response.data;
  },

  searchDirectory: async (
    query: SearchDirectoryRequest,
  ): Promise<PaginatedResponse<DirectorySummaryDto>> => {
    const cleanParams = Object.fromEntries(
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      Object.entries(query).filter(([_, value]) => value != null),
    );

    const response = await apiClient.get<PaginatedResponse<DirectorySummaryDto>>(
      "/directories/search",
      {
        params: cleanParams,
      },
    );
    return response.data;
  },

  // Update directory
  updateDirectory: async (data: UpdateDirectorySchema): Promise<UpdateDirectoryResponse> => {
    const response = await apiClient.put<UpdateDirectoryResponse>("/directories", data);
    return response.data;
  },

  // Move directory
  moveDirectories: async (directoryIds: string[], destinationId: string | null): Promise<void> => {
    await apiClient.post("directories/move", {
      destinationId,
      directoryIds,
    });
  },

  copyDirectory: async (dirId: string, destinationId: string | null) => {
    await apiClient.post("directories/copy", {
      destinationId,
      directoryId: dirId,
    });
  },

  // Delete directory
  deleteDirectory: async (id: string, options: DeleteDirectorySchema): Promise<void> => {
    await apiClient.delete(`/directories/${id}`, {
      data: options,
    });
  },

  restoreDirectories: async (directoryIds: string[]) => {
    const result = await apiClient.post("directories/restore", {
      directoryIds,
    });

    return result.data;
  },
};
