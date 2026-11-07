import { isApiError } from "@/api/errors";
import {
  type FinalizeFileUploadRequest,
  type InitializeFileUploadRequest,
  type InitializeFileUploadResponse,
  fileApi,
} from "@/api/file";
import { logger } from "@/utils/logger";

import { createMicroBatcher } from "./micro-batcher";
import { runWindow } from "./run-window";

const GIB = 1024 ** 3;

const MAX_BATCH_ITEMS = 100;
const LINGER_MS = 50;
const RETRY_DELAYS_MS = [1000, 3000, 8000];

// One send per second on each batcher stays well inside the 200 requests per minute nginx
// allows, and leaves room for the rest of the app.
const MIN_SEND_INTERVAL_MS = 1000;

const FINALIZE_MAX_BYTES = GIB;
const PUT_CONCURRENCY = 4;
const WINDOW_MAX_FILES = 200;
const WINDOW_MAX_BYTES = GIB;

/** The server rejected this one file. The rest of its batch is unaffected. */
export class UploadItemError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "UploadItemError";
  }
}

const initBatcher = createMicroBatcher<InitializeFileUploadRequest, InitializeFileUploadResponse>({
  lingerMs: LINGER_MS,
  maxInFlight: 1,
  maxItems: MAX_BATCH_ITEMS,
  maxWeight: Number.POSITIVE_INFINITY,
  minIntervalMs: MIN_SEND_INTERVAL_MS,
  retryDelaysMs: RETRY_DELAYS_MS,
  send: async (items) => {
    const { results } = await fileApi.initializeUploadBatch(items);

    return results.map(({ error, uploadId, uploadUrl }) => {
      if (uploadId && uploadUrl) return { uploadId, uploadUrl };

      return new UploadItemError(error ?? "The upload could not be started");
    });
  },
});

// Two in flight so verifying one large file doesn't hold up the small files behind it.
const finalizeBatcher = createMicroBatcher<FinalizeFileUploadRequest, true>({
  lingerMs: LINGER_MS,
  maxInFlight: 2,
  maxItems: MAX_BATCH_ITEMS,
  maxWeight: FINALIZE_MAX_BYTES,
  minIntervalMs: MIN_SEND_INTERVAL_MS,
  retryDelaysMs: RETRY_DELAYS_MS,
  send: async (items) => {
    const { results } = await fileApi.finalizeUploadBatch(items);

    return results.map(({ error, success }) => {
      if (success) return true;

      return new UploadItemError(error ?? "The upload could not be saved");
    });
  },
});

let activePuts = 0;
const waitingPuts: (() => void)[] = [];

const releasePutSlot = () => {
  activePuts -= 1;
  waitingPuts.shift()?.();
};

const acquirePutSlot = (signal: AbortSignal) =>
  new Promise<void>((resolve, reject) => {
    signal.throwIfAborted();

    if (activePuts < PUT_CONCURRENCY) {
      activePuts += 1;
      resolve();
      return;
    }

    const onAbort = () => {
      waitingPuts.splice(waitingPuts.indexOf(grant), 1);
      reject(signal.reason);
    };

    const grant = () => {
      signal.removeEventListener("abort", onAbort);
      activePuts += 1;
      resolve();
    };

    signal.addEventListener("abort", onAbort, { once: true });
    waitingPuts.push(grant);
  });

interface PutRequest {
  blob: Blob;
  contentType: string;
  onProgress: (percent: number) => void;
  signal: AbortSignal;
  uploadUrl: string;
}

const putWithSlot = async ({ blob, contentType, onProgress, signal, uploadUrl }: PutRequest) => {
  await acquirePutSlot(signal);

  try {
    await fileApi.uploadToS3(uploadUrl, blob, contentType, onProgress, signal);
  } finally {
    releasePutSlot();
  }
};

export interface SendUploadRequest {
  blob: Blob;
  contentType: string;
  onInitialized: () => void;
  onProgress: (percent: number) => void;
  request: InitializeFileUploadRequest;
  signal: AbortSignal;
}

/**
 * Starts the upload and sends the bytes, returning the upload ID to finalize. Signed URLs wait
 * for a free slot and can expire on a slow link, so a rejected PUT asks for a new URL once.
 */
export const sendUpload = async ({
  blob,
  contentType,
  onInitialized,
  onProgress,
  request,
  signal,
}: SendUploadRequest): Promise<string> => {
  const put = (uploadUrl: string) =>
    putWithSlot({ blob, contentType, onProgress, signal, uploadUrl });

  const { uploadId, uploadUrl } = await initBatcher.submit(request, { signal });

  onInitialized();

  try {
    await put(uploadUrl);

    return uploadId;
  } catch (err) {
    if (!isApiError(err) || err.status !== 403) throw err;

    logger.warn("A signed upload URL was rejected, requesting a new one", err);
  }

  const renewed = await initBatcher.submit(request, { signal });

  await put(renewed.uploadUrl);

  return renewed.uploadId;
};

export const finalizeUpload = (
  request: FinalizeFileUploadRequest,
  size: number,
  signal: AbortSignal,
): Promise<true> => finalizeBatcher.submit(request, { signal, weight: size });

export const runUploadWindow = <T>(
  items: readonly T[],
  run: (item: T) => Promise<unknown>,
  sizeOf: (item: T) => number,
  shouldStop?: () => boolean,
): Promise<void> =>
  runWindow(items, run, {
    alwaysAdmit: PUT_CONCURRENCY,
    maxBytes: WINDOW_MAX_BYTES,
    maxItems: WINDOW_MAX_FILES,
    shouldStop,
    sizeOf,
  });

// Encrypting holds each file's ciphertext in memory until its upload finishes.
export const runEncryptedUploadWindow = <T>(
  items: readonly T[],
  run: (item: T) => Promise<unknown>,
  sizeOf: (item: T) => number,
): Promise<void> =>
  runWindow(items, run, {
    alwaysAdmit: PUT_CONCURRENCY,
    maxBytes: 0,
    maxItems: PUT_CONCURRENCY,
    sizeOf,
  });
