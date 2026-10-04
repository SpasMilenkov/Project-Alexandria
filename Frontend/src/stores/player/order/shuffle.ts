import {
  RANGE_SIZE,
  type ShuffleSessionResponse,
  conflictField,
  httpStatus,
  isAuthError,
  shuffleApi,
  sourceUnavailable,
} from "@/api/shuffle";
import { type SourceAnchor, type SourceDescriptor, sameSource } from "@/utils/player-source";

import type { Order, RangeBufferHooks, RangeResult } from "../types";

import { createRangeBuffer } from "./range-buffer";

export interface ShuffleSession {
  sessionId: string;
  expiresAt: string;
  total: number;
}

export interface OpenedShuffle {
  order: Order;
  session: ShuffleSession;
  anchored: boolean;
  created: boolean;
}

export type OpenShuffleOptions = {
  ref: SourceDescriptor;
  sessionId?: string | null;
  requestId?: string;
  flushHistory: () => Promise<void>;
  hooks?: RangeBufferHooks;
} & (
  | { anchor: SourceAnchor; avoidFirstFileId?: never }
  | { avoidFirstFileId: string | null; anchor?: never }
  | { anchor?: never; avoidFirstFileId?: never }
);

export class ShuffleUnavailable extends Error {}

// A retry replays the original request id. Ids minted after a 409 are kept per
// original id too, so a lost fallback response is replayed instead of leaked.
const derivedIds = new Map<string, string>();
const MAX_DERIVED_IDS = 32;

const derivedId = (key: string): string => {
  const known = derivedIds.get(key);
  if (known) return known;
  const fresh = shuffleApi.newRequestId();
  derivedIds.set(key, fresh);
  if (derivedIds.size > MAX_DERIVED_IDS) {
    const oldest = derivedIds.keys().next().value;
    if (oldest !== undefined) derivedIds.delete(oldest);
  }
  return fresh;
};

const toRange = (response: ShuffleSessionResponse): RangeResult => ({
  offset: response.offset,
  scannedCount: response.scannedCount,
  total: response.totalCount,
  items: response.items,
});

const createOrder = (
  ref: SourceDescriptor,
  sessionId: string,
  hooks: RangeBufferHooks = {},
): Order =>
  createRangeBuffer(async (start) => {
    const response = await shuffleApi.getSession(sessionId, start, RANGE_SIZE);
    if (!sameSource(response.source, ref)) throw sourceUnavailable();
    hooks.onSession?.(response.sessionId, response.expiresAt, response.totalCount);
    return toRange(response);
  }, hooks);

const readSession = async (
  ref: SourceDescriptor,
  sessionId: string,
  hooks: RangeBufferHooks = {},
): Promise<OpenedShuffle> => {
  const response = await shuffleApi.getSession(sessionId, 0, RANGE_SIZE);
  if (!sameSource(response.source, ref)) throw sourceUnavailable();
  const order = createOrder(ref, sessionId, hooks);
  order.seed(toRange(response));
  return {
    order,
    session: { sessionId, expiresAt: response.expiresAt, total: response.totalCount },
    anchored: false,
    created: false,
  };
};

const requestBody = (
  ref: SourceDescriptor,
  anchor: SourceAnchor | null,
  avoidFirstFileId: string | null,
  requestId: string,
) => ({
  requestId,
  source: ref,
  anchorFileId: anchor?.fileId ?? null,
  anchorPlaylistItemId: anchor?.playlistItemId ?? null,
  avoidFirstFileId,
  limit: RANGE_SIZE,
});

const adopt = (
  ref: SourceDescriptor,
  response: ShuffleSessionResponse,
  anchored: boolean,
  hooks: RangeBufferHooks = {},
): OpenedShuffle => {
  const order = createOrder(ref, response.sessionId, hooks);
  order.seed(toRange(response));
  return {
    order,
    session: {
      sessionId: response.sessionId,
      expiresAt: response.expiresAt,
      total: response.totalCount,
    },
    anchored,
    created: true,
  };
};

export const openShuffle = async (options: OpenShuffleOptions): Promise<OpenedShuffle> => {
  await options.flushHistory();
  // A stored session that cannot be read is the caller's call (X5). Falling
  // through to a create here would lose the anchor and skip the notice.
  if (options.sessionId) return readSession(options.ref, options.sessionId, options.hooks);
  const anchor = options.anchor ?? null;
  const avoid = options.avoidFirstFileId ?? null;
  const firstId = options.requestId ?? shuffleApi.newRequestId();
  try {
    const response = await shuffleApi.createSession(
      requestBody(options.ref, anchor, avoid, firstId),
    );
    return adopt(options.ref, response, Boolean(anchor), options.hooks);
  } catch (err: unknown) {
    if (httpStatus(err) !== 409) throw err;
    const field = conflictField(err);
    if (field === "requestId") {
      const key = `${firstId}#retry`;
      const response = await shuffleApi.createSession(
        requestBody(options.ref, anchor, avoid, derivedId(key)),
      );
      derivedIds.delete(key);
      return adopt(options.ref, response, Boolean(anchor), options.hooks);
    }
    if (anchor && field === "anchorFileId") {
      const key = `${firstId}#fallback`;
      const response = await shuffleApi.createSession(
        requestBody(options.ref, null, avoid, derivedId(key)),
      );
      derivedIds.delete(key);
      return adopt(options.ref, response, false, options.hooks);
    }
    throw err;
  }
};

export const releaseShuffleSession = async (sessionId: string | null): Promise<void> => {
  if (!sessionId) return;
  try {
    await shuffleApi.deleteSession(sessionId);
  } catch (err: unknown) {
    if (!isAuthError(err) && httpStatus(err) !== 404) throw err;
  }
};

export const isSessionGone = (err: unknown): boolean => httpStatus(err) === 404;
