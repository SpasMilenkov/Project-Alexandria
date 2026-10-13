import type { MediaFileDto } from "@/api/streaming";

import { apiClient } from "./client";

export interface TopSongsQuery {
  from: string;
  to: string;
  limit: number;
}

export interface TopSong {
  file: MediaFileDto;
  qualifiedPlayCount: number;
  totalListenedSeconds: number;
}

export interface TopSongsResponse {
  from: string;
  to: string;
  items: TopSong[];
}

export interface JumpBackInQuery {
  limit: number;
  inactiveDays?: number;
}

export interface JumpBackInSong extends TopSong {
  lastPlayedAt: string;
}

export interface JumpBackInResponse {
  inactiveDays: number;
  inactiveBefore: string;
  items: JumpBackInSong[];
}

export const listeningRankingsApi = {
  getTopSongs: async (params: TopSongsQuery): Promise<TopSongsResponse> => {
    const result = await apiClient.get<TopSongsResponse>("/stats/top-songs", { params });

    return result.data;
  },

  getJumpBackIn: async (params: JumpBackInQuery): Promise<JumpBackInResponse> => {
    const result = await apiClient.get<JumpBackInResponse>("/stats/jump-back-in", { params });

    return result.data;
  },
};
