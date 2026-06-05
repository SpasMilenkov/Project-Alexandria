import type { PaginatedResponse } from "./directory";
import type { StreamingRepresentationResponse } from "./streaming";

import { apiClient } from "./client";

// Response types

export interface PlaylistRepresentationResponse extends Pick<
  StreamingRepresentationResponse,
  "id" | "codec" | "width" | "height" | "status"
> {
  bitrateKbps?: number;
}

export interface PlaylistItemResponse {
  id: string;
  position: number;
  transpilationJobId: string;
  fileName: string;
  mimeType: string;
  segmentPrefix?: string;
  representations: PlaylistRepresentationResponse[];
  createdAt: string;
}

export interface PlaylistResponse {
  id: string;
  name: string;
  description?: string;
  hasCover: boolean;
  ambientTheme?: string;
  itemCount: number;
  createdAt: string;
  updatedAt?: string;
}

export interface PlaylistDetailResponse {
  id: string;
  name: string;
  description?: string;
  hasCover: boolean;
  ambientTheme?: string;
  items: PlaylistItemResponse[];
  createdAt: string;
  updatedAt?: string;
}

export interface CoverUploadRequest {
  playlistId: string;
  mimeType: string;
  fileSize: number;
}

// Request types

export interface CreatePlaylistRequest {
  name: string;
  description?: string;
  /**
   * Pre-resolved cover URL. When omitted and initialTranspilationJobIds is non-empty,
   * the backend falls back to a random preview from those files.
   */
  hasCover: boolean;
  ambientTheme?: string;
  initialTranspilationJobIds?: string[];
}

export interface UpdatePlaylistRequest {
  name?: string;
  description?: string;
  hasCover?: boolean;
  ambientTheme?: string;
}

export interface AddPlaylistItemRequest {
  transpilationJobId: string;
}

export interface ReorderPlaylistItemsRequest {
  orderedItemIds: string[];
}

// API

export const playlistApi = {
  create: async (req: CreatePlaylistRequest): Promise<PlaylistResponse> => {
    const result = await apiClient.post<PlaylistResponse>("/playlists", req);
    return result.data;
  },

  getById: async (id: string): Promise<PlaylistDetailResponse> => {
    const result = await apiClient.get<PlaylistDetailResponse>(`/playlists/${id}`);
    return result.data;
  },

  getAll: async (page: number, pageSize: number): Promise<PaginatedResponse<PlaylistResponse>> => {
    const result = await apiClient.get<PaginatedResponse<PlaylistResponse>>("/playlists", {
      params: { page, pageSize },
    });
    return result.data;
  },

  getCoverUploadUrl: async (req: CoverUploadRequest) => {
    const result = await apiClient.post("/streaming/playlists/cover", req);

    return result.data;
  },

  getPlaylistCover: async (playlistId: string): Promise<string> => {
    const result = await apiClient.get<string>(`/streaming/playlists/cover/${playlistId}`);
    return result.data;
  },

  update: async (id: string, req: UpdatePlaylistRequest): Promise<PlaylistResponse> => {
    const result = await apiClient.patch<PlaylistResponse>(`/playlists/${id}`, req);
    return result.data;
  },

  delete: async (id: string): Promise<void> => {
    await apiClient.delete(`/playlists/${id}`);
  },

  addItem: async (
    playlistId: string,
    req: AddPlaylistItemRequest,
  ): Promise<PlaylistItemResponse> => {
    const result = await apiClient.post<PlaylistItemResponse>(
      `/playlists/${playlistId}/items`,
      req,
    );
    return result.data;
  },

  removeItem: async (playlistId: string, itemId: string): Promise<void> => {
    await apiClient.delete(`/playlists/${playlistId}/items/${itemId}`);
  },

  reorderItems: async (playlistId: string, req: ReorderPlaylistItemsRequest): Promise<void> => {
    await apiClient.put(`/playlists/${playlistId}/items/order`, req);
  },
};
