import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { FinalizeFileUploadRequest, InitializeFileUploadRequest } from "@/api/file";

const FakeWorker = function FakeWorker() {
  return {
    onmessage: null as ((event: MessageEvent) => void) | null,
    onerror: null as ((event: ErrorEvent) => void) | null,
    postMessage(message: { id: string }) {
      queueMicrotask(() => {
        this.onmessage?.({
          data: { hash: "0".repeat(64), id: message.id, type: "done" },
        } as MessageEvent);
      });
    },
    terminate() {},
  };
};

// The batchers are module singletons that remember when they last sent, so each test loads
// fresh copies. Sends are timestamped to check the pacing that protects the rate limit.
const loadPipeline = async () => {
  vi.resetModules();

  const { fileApi } = await import("@/api/file");
  const { useDirectoryUpload } = await import("@/composables/useDirectoryUpload");

  const initSends: number[] = [];
  const finalizeSends: number[] = [];
  let nextUploadId = 0;

  const init = vi.spyOn(fileApi, "initializeUploadBatch");

  init.mockImplementation((items: InitializeFileUploadRequest[]) => {
    initSends.push(Date.now());

    return Promise.resolve({
      results: items.map(() => {
        nextUploadId += 1;

        return {
          error: null,
          uploadId: `upload-${nextUploadId}`,
          uploadUrl: `https://example.test/${nextUploadId}`,
        };
      }),
    });
  });

  const finalize = vi.spyOn(fileApi, "finalizeUploadBatch");

  finalize.mockImplementation((items: FinalizeFileUploadRequest[]) => {
    finalizeSends.push(Date.now());

    return Promise.resolve({
      results: items.map(({ uploadId }) => ({ error: null, success: true, uploadId })),
    });
  });

  const put = vi.spyOn(fileApi, "uploadToS3").mockResolvedValue();

  return { finalize, finalizeSends, initSends, put, useDirectoryUpload };
};

const entries = (count: number) =>
  Array.from({ length: count }, (_, index) => ({
    file: new File(["hello"], `file-${index}.txt`, { type: "text/plain" }),
    relativePath: `folder/file-${index}.txt`,
  }));

const runToCompletion = async (work: Promise<void>) => {
  let finished = false;

  void work.finally(() => {
    finished = true;
  });

  const advance = async (): Promise<void> => {
    if (finished) return;

    await vi.advanceTimersByTimeAsync(100);
    await advance();
  };

  await advance();
};

const shortestGap = (times: number[]) =>
  times.slice(1).reduce((gap, time, index) => Math.min(gap, time - times[index]), Infinity);

describe("directory upload pipeline", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal("Worker", FakeWorker);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("uploads 500 files in a handful of paced requests", async () => {
    const { finalizeSends, initSends, put, useDirectoryUpload } = await loadPipeline();
    const { fileStatuses, startUploadPipeline, successfulFiles } = useDirectoryUpload();

    await runToCompletion(startUploadPipeline(entries(500), {}));

    expect(successfulFiles.value).toBe(500);
    expect(fileStatuses.value.every((status) => status.status === "success")).toBe(true);
    expect(put).toHaveBeenCalledTimes(500);

    // One call per file would be 1000 requests here, which is what tripped the rate limit.
    expect(initSends.length + finalizeSends.length).toBeLessThanOrEqual(40);
    expect(shortestGap(initSends)).toBeGreaterThanOrEqual(1000);
    expect(shortestGap(finalizeSends)).toBeGreaterThanOrEqual(1000);
  });

  it("keeps uploading the rest when one file is refused", async () => {
    const { finalize, useDirectoryUpload } = await loadPipeline();
    const { failedFiles, startUploadPipeline, successfulFiles } = useDirectoryUpload();

    finalize.mockImplementation((items: FinalizeFileUploadRequest[]) =>
      Promise.resolve({
        results: items.map(({ fileName, uploadId }) => {
          if (fileName === "file-3.txt") {
            return { error: "The file failed verification.", success: false, uploadId };
          }

          return { error: null, success: true, uploadId };
        }),
      }),
    );

    await runToCompletion(startUploadPipeline(entries(10), {}));

    expect(successfulFiles.value).toBe(9);
    expect(failedFiles.value).toHaveLength(1);
    expect(failedFiles.value[0].relativePath).toBe("folder/file-3.txt");
    expect(failedFiles.value[0].error).toBe("The file failed verification.");
  });

  it("retries only the failed file", async () => {
    const { finalize, put, useDirectoryUpload } = await loadPipeline();
    const { failedFiles, retryFailedUploads, startUploadPipeline, successfulFiles } =
      useDirectoryUpload();

    finalize.mockImplementationOnce((items: FinalizeFileUploadRequest[]) =>
      Promise.resolve({
        results: items.map(({ fileName, uploadId }) => {
          if (fileName === "file-1.txt") {
            return { error: "The file failed verification.", success: false, uploadId };
          }

          return { error: null, success: true, uploadId };
        }),
      }),
    );

    await runToCompletion(startUploadPipeline(entries(3), {}));

    expect(failedFiles.value).toHaveLength(1);
    expect(put).toHaveBeenCalledTimes(3);

    await runToCompletion(retryFailedUploads());

    expect(successfulFiles.value).toBe(3);
    expect(failedFiles.value).toHaveLength(0);
    expect(put).toHaveBeenCalledTimes(4);
  });

  it("marks a whole batch failed when the server stays unavailable, and the batch can be retried", async () => {
    const { finalize, useDirectoryUpload } = await loadPipeline();
    const { ApiError } = await import("@/api/errors");
    const { failedFiles, retryFailedUploads, startUploadPipeline, successfulFiles } =
      useDirectoryUpload();

    const unavailable = new ApiError("unavailable", { kind: "http", status: 503 });

    finalize.mockRejectedValueOnce(unavailable);
    finalize.mockRejectedValueOnce(unavailable);
    finalize.mockRejectedValueOnce(unavailable);
    finalize.mockRejectedValueOnce(unavailable);

    await runToCompletion(startUploadPipeline(entries(2), {}));

    expect(failedFiles.value).toHaveLength(2);

    await runToCompletion(retryFailedUploads());

    expect(successfulFiles.value).toBe(2);
  });
});
