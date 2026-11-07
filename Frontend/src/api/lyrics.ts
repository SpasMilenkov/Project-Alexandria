import type { LyricsProvider } from "@/enums/lyrics-provider";
import type { LyricsStatus } from "@/enums/lyrics-status";
import type { UploadLyricsSchema } from "@/schemas/lyrics";

import { api } from "./client";

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
  getLyrics: (lyricsId: string): Promise<LyricsDto> =>
    api.get<LyricsDto>(`/streaming/lyrics/${lyricsId}`),

  uploadLyrics: async (req: UploadLyricsSchema): Promise<void> => {
    await api.post("/streaming/lyrics", req);
  },

  deleteLyrics: async (lyricsId: string): Promise<void> => {
    await api.delete(`/streaming/lyrics/${lyricsId}`);
  },

  changeProvider: async (req: ChangeProviderRequest): Promise<void> => {
    await api.patch("/streaming/lyrics/change-provider", req);
  },

  requeueLyrics: async (jobId: string): Promise<void> => {
    await api.patch(`/streaming/lyrics/refetch/${jobId}`);
  },
};
