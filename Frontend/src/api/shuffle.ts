import type { MediaFileDto } from "./streaming";

import { api } from "./client";
import { ApiError, isApiError } from "./errors";

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

/** Single range size shared by sequential pages and shuffle reads. */
export const RANGE_SIZE = SHUFFLE_BATCH_LIMIT;

export const httpStatus = (err: unknown): number | null => {
  if (!isApiError(err)) return null;

  return err.status ?? null;
};

export const isAuthError = (err: unknown): boolean => {
  const status = httpStatus(err);
  return status === 401 || status === 403;
};

/** Builds the client error the 404 recovery paths match on. */
export const sourceUnavailable = (): unknown =>
  new ApiError("The source is no longer available", { kind: "http", status: 404 });

/** Names which field a 409 response blames: requestId, anchorFileId, or null. */
export const conflictField = (err: unknown): string | null => {
  if (!isApiError(err)) return null;

  const errors = (err.data as { errors?: Record<string, string[]> } | undefined)?.errors;
  if (!errors || typeof errors !== "object") return null;
  const keys = Object.keys(errors);
  if (keys.some((key) => key.toLowerCase().includes("requestid"))) return "requestId";
  if (keys.some((key) => key.toLowerCase().includes("anchorfileid"))) return "anchorFileId";
  return null;
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

  createSession: (req: CreateShuffleSessionRequest): Promise<ShuffleSessionResponse> =>
    api.post<ShuffleSessionResponse>("/streaming/shuffle-sessions", req),

  getSession: (
    sessionId: string,
    offset: number,
    limit: number = SHUFFLE_BATCH_LIMIT,
  ): Promise<ShuffleSessionResponse> =>
    api.get<ShuffleSessionResponse>(`/streaming/shuffle-sessions/${sessionId}`, {
      params: { offset, limit },
    }),

  deleteSession: async (sessionId: string): Promise<void> => {
    await api.delete(`/streaming/shuffle-sessions/${sessionId}`);
  },
};
