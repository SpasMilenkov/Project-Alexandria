import type { MediaFileDto } from "./streaming";

import { apiClient } from "./client";

export interface PlaybackSource {
  isVideo: boolean;
  playlistId: string | null;
}

export interface ShuffleEntry {
  position: number;
  file: MediaFileDto;
}

export interface ShuffleSessionResponse {
  sessionId: string;
  source: PlaybackSource;
  algorithmVersion: number;
  createdAt: string;
  expiresAt: string;
  totalCount: number;
  anchorPosition: number | null;
  offset: number;
  scannedCount: number;
  nextOffset: number | null;
  items: ShuffleEntry[];
}

export interface CreateShuffleSessionRequest {
  requestId: string;
  source: PlaybackSource;
  anchorFileId?: string | null;
  anchorPlaylistItemId?: string | null;
  avoidFirstFileId?: string | null;
  limit?: number;
}

export const SHUFFLE_BATCH_LIMIT = 50;

export const isAuthError = (err: unknown): boolean => {
  const status = (err as { response?: { status?: number } })?.response?.status;
  return status === 401 || status === 403;
};

export const httpStatus = (err: unknown): number | null => {
  const status = (err as { response?: { status?: number } })?.response?.status;
  return typeof status === "number" ? status : null;
};

const newRequestId = (): string => {
  const secureCrypto = globalThis.crypto as Crypto | undefined;
  if (secureCrypto && typeof secureCrypto.randomUUID === "function")
    return secureCrypto.randomUUID();
  const bytes = new Uint8Array(16);
  if (secureCrypto) {
    secureCrypto.getRandomValues(bytes);
  } else {
    for (let i = 0; i < bytes.length; i++) bytes[i] = Math.floor(Math.random() * 256);
  }
  bytes[6] = ((bytes[6] ?? 0) & 0x0f) | 0x40;
  bytes[8] = ((bytes[8] ?? 0) & 0x3f) | 0x80;
  const hex = [...bytes].map((value) => value.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
};

export const shuffleApi = {
  newRequestId,

  createSession: async (req: CreateShuffleSessionRequest): Promise<ShuffleSessionResponse> => {
    const result = await apiClient.post<ShuffleSessionResponse>("/streaming/shuffle-sessions", req);
    return result.data;
  },

  getSession: async (
    sessionId: string,
    offset: number,
    limit: number = SHUFFLE_BATCH_LIMIT,
  ): Promise<ShuffleSessionResponse> => {
    const result = await apiClient.get<ShuffleSessionResponse>(
      `/streaming/shuffle-sessions/${sessionId}`,
      { params: { offset, limit } },
    );
    return result.data;
  },

  deleteSession: async (sessionId: string): Promise<void> => {
    await apiClient.delete(`/streaming/shuffle-sessions/${sessionId}`);
  },
};
