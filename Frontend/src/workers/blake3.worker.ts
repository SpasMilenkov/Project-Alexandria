/// <reference lib="webworker" />

import { createBLAKE3 } from "hash-wasm";

export interface WorkerInMessage {
  file: Blob;
  id: string;
}

export type WorkerOutMessage =
  | { type: "progress"; id: string; percent: number }
  | { type: "done"; id: string; hash: string }
  | { type: "error"; id: string; message: string };

declare const self: DedicatedWorkerGlobalScope;

// The worker is reused across files, so the WASM instance is created once and reset per file.
let hasherReady: ReturnType<typeof createBLAKE3> | undefined = undefined;

self.onmessage = async (e: MessageEvent<WorkerInMessage>) => {
  const { file, id } = e.data;

  try {
    hasherReady ??= createBLAKE3();

    const hasher = await hasherReady;
    hasher.init();

    const CHUNK = 16 * 1024 * 1024;
    let offset = 0;

    while (offset < file.size) {
      const buf = await file.slice(offset, offset + CHUNK).arrayBuffer();
      hasher.update(new Uint8Array(buf));
      offset += CHUNK;

      self.postMessage({
        type: "progress",
        id,
        percent: Math.min(100, Math.round((offset / file.size) * 100)),
      } satisfies WorkerOutMessage);
    }

    self.postMessage({
      type: "done",
      id,
      hash: hasher.digest("hex"),
    } satisfies WorkerOutMessage);
  } catch (err: unknown) {
    // A failed WASM setup must not stay cached and fail every later file.
    hasherReady = undefined;

    self.postMessage({
      type: "error",
      id,
      message: err instanceof Error ? err.message : "Unknown hashing error",
    } satisfies WorkerOutMessage);
  }
};
