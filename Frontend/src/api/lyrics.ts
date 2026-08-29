import type { LyricsProvider } from "@/enums/lyrics-provider";
import type { LyricsStatus } from "@/enums/lyrics-status";
import type { UploadLyricsSchema } from "@/schemas/lyrics";

import { apiClient } from "./client";

// Response types

export interface LyricsDto {
  id: string;
  status: LyricsStatus;
  provider: LyricsProvider;
  cached: boolean;
  confidenceScore: number | null;
  fetchedAt: string | null;
  playLyrics: string | null;
  syncedLyrics: string | null;
}

// Request types

export interface ChangeProviderRequest {
  lyricsId: string;
  provider: LyricsProvider;
}

export const lyricsApi = {
  getLyrics: async (lyricsId: string): Promise<LyricsDto> => {
    const result = await apiClient.get<LyricsDto>(`/streaming/lyrics/${lyricsId}`);
    return result.data;
  },

  uploadLyrics: async (req: UploadLyricsSchema): Promise<void> => {
    await apiClient.post("/streaming/lyrics", req);
  },

  deleteLyrics: async (lyricsId: string): Promise<void> => {
    await apiClient.delete(`/streaming/lyrics/${lyricsId}`);
  },

  changeProvider: async (req: ChangeProviderRequest): Promise<void> => {
    await apiClient.patch("/streaming/lyrics/change-provider", req);
  },

  requeueLyrics: async (jobId: string): Promise<void> => {
    await apiClient.patch(`/streaming/lyrics/refetch/${jobId}`);
  },
};
