import type { SortBy } from "@/enums/SortBy";
import type { SortDirection } from "@/enums/SortDirection";
import type {
  CreateDirectorySchema,
  DeleteDirectorySchema,
  UpdateDirectorySchema,
} from "@/schemas/directory";
import type { PaginationParams } from "@/types/pagination-params";

import { api } from "./client";

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
  isStarred: boolean;
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
  isStarred: boolean;
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

export interface DirectoryFavoriteResponse {
  directoryId: string;
  isStarred: boolean;
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

// oxlint-disable-next-line sort-keys
export const directoryApi = {
  setFavorite: (directoryId: string, isStarred: boolean): Promise<DirectoryFavoriteResponse> =>
    api.patch<DirectoryFavoriteResponse>(`/directories/${directoryId}/favorite`, {
      isStarred,
    }),

  // Create a new directory
  createDirectory: (data: CreateDirectorySchema): Promise<CreateDirectoryResponse> =>
    api.post<CreateDirectoryResponse>("/directories", data),

  uploadDirectory: (
    parentId: string | undefined,
    paths: string[],
  ): Promise<Record<string, string | null>> =>
    api.post<Record<string, string | null>>("/directories/create-subtree", {
      parentId,
      paths,
    }),

  // Get directory by ID
  getDirectory: (directoryId: string): Promise<GetDirectoryResponse> =>
    api.get<GetDirectoryResponse>("/directories", {
      params: { directoryId },
    }),

  getRooSubDirectories: (
    paginationParams: PaginationParams,
  ): Promise<PaginatedResponse<DirectorySummaryDto>> =>
    api.get<PaginatedResponse<DirectorySummaryDto>>("/directories/root", {
      params: {
        page: paginationParams.page,
        pageSize: paginationParams.pageSize,
        sortBy: paginationParams.SortBy,
        sortDirection: paginationParams.sortDirection,
      },
    }),
  getSubDirectories: (
    directoryId: string,
    paginationParams: PaginationParams,
  ): Promise<PaginatedResponse<DirectorySummaryDto>> =>
    api.get<PaginatedResponse<DirectorySummaryDto>>("/directories/sub", {
      params: {
        directoryId,
        page: paginationParams.page,
        pageSize: paginationParams.pageSize,
        sortBy: paginationParams.SortBy,
        sortDirection: paginationParams.sortDirection,
      },
    }),

  getDirectoryPath: (id: string): Promise<{ pathParts: { id: string; name: string }[] }> =>
    api.get<{
      pathParts: { id: string; name: string }[];
    }>(`/directories/path/${id}`),

  searchDirectory: (
    query: SearchDirectoryRequest,
  ): Promise<PaginatedResponse<DirectorySummaryDto>> => {
    const cleanParams = Object.fromEntries(
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      Object.entries(query).filter(([_, value]) => value !== null),
    );

    return api.get<PaginatedResponse<DirectorySummaryDto>>("/directories/search", {
      params: cleanParams,
    });
  },

  // Update directory
  updateDirectory: (data: UpdateDirectorySchema): Promise<UpdateDirectoryResponse> =>
    api.patch<UpdateDirectoryResponse>(`/directories/${data.directoryId}`, data),

  // Move directory
  moveDirectories: async (directoryIds: string[], destinationId: string | null): Promise<void> => {
    await api.post("directories/move", {
      destinationId,
      directoryIds,
    });
  },

  copyDirectory: async (dirId: string, destinationId: string | null) => {
    await api.post("directories/copy", {
      destinationId,
      directoryId: dirId,
    });
  },

  // Delete directory
  deleteDirectory: async (id: string, options: DeleteDirectorySchema): Promise<void> => {
    await api.delete(`/directories/${id}`, {
      body: options,
    });
  },

  restoreDirectories: (directoryIds: string[]) =>
    api.post<number>("directories/restore", {
      directoryIds,
    }),
};
