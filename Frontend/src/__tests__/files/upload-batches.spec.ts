import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { fileApi } from "@/api/file";

// The batchers are module singletons and remember when they last sent, so each test gets a
// fresh copy and its first send isn't held back by the previous test.
const loadBatches = async () => {
  vi.resetModules();

  const { ApiError } = await import("@/api/errors");
  const { fileApi: api } = await import("@/api/file");
  const batches = await import("@/features/upload/upload-batches");

  return { ApiError, api, ...batches };
};

const request = {
  contentLength: 5,
  contentType: "text/plain",
  hash: "a".repeat(64),
};

const sendArgs = (signal: AbortSignal, onInitialized = () => {}) => ({
  blob: new Blob(["hello"]),
  contentType: "text/plain",
  onInitialized,
  onProgress: () => {},
  request,
  signal,
});

const initResult = (uploadId: string) => ({
  results: [{ error: null, uploadId, uploadUrl: `https://example.test/${uploadId}` }],
});

describe("sendUpload", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("returns the upload ID after sending the bytes to the signed URL", async () => {
    const { api, sendUpload } = await loadBatches();

    vi.spyOn(api, "initializeUploadBatch").mockResolvedValue(initResult("first"));
    const put = vi.spyOn(api, "uploadToS3").mockResolvedValue();

    const onInitialized = vi.fn();
    const uploadId = sendUpload(sendArgs(new AbortController().signal, onInitialized));

    await vi.advanceTimersByTimeAsync(50);

    await expect(uploadId).resolves.toBe("first");
    expect(onInitialized).toHaveBeenCalledOnce();
    expect(put.mock.calls[0][0]).toBe("https://example.test/first");
  });

  it("requests a new URL once when the PUT is rejected with a 403", async () => {
    const { ApiError, api, sendUpload } = await loadBatches();

    const init = vi.spyOn(api, "initializeUploadBatch");
    init.mockResolvedValueOnce(initResult("expired"));
    init.mockResolvedValueOnce(initResult("renewed"));

    const put = vi.spyOn(api, "uploadToS3");
    put.mockRejectedValueOnce(new ApiError("expired", { kind: "http", status: 403 }));
    put.mockResolvedValueOnce();

    const uploadId = sendUpload(sendArgs(new AbortController().signal));

    await vi.advanceTimersByTimeAsync(50 + 1000);

    await expect(uploadId).resolves.toBe("renewed");
    expect(put.mock.calls.map((call) => call[0])).toEqual([
      "https://example.test/expired",
      "https://example.test/renewed",
    ]);
  });

  it("fails when the renewed URL is rejected too", async () => {
    const { ApiError, api, sendUpload } = await loadBatches();

    vi.spyOn(api, "initializeUploadBatch")
      .mockResolvedValueOnce(initResult("one"))
      .mockResolvedValueOnce(initResult("two"));

    const put = vi
      .spyOn(api, "uploadToS3")
      .mockRejectedValue(new ApiError("denied", { kind: "http", status: 403 }));

    const assertion = expect(
      sendUpload(sendArgs(new AbortController().signal)),
    ).rejects.toMatchObject({ status: 403 });

    await vi.advanceTimersByTimeAsync(50 + 1000);
    await assertion;

    expect(put).toHaveBeenCalledTimes(2);
  });

  it("does not request a new URL for other PUT failures", async () => {
    const { ApiError, api, sendUpload } = await loadBatches();

    const init = vi.spyOn(api, "initializeUploadBatch").mockResolvedValue(initResult("one"));
    vi.spyOn(api, "uploadToS3").mockRejectedValue(
      new ApiError("server", { kind: "http", status: 500 }),
    );

    const assertion = expect(
      sendUpload(sendArgs(new AbortController().signal)),
    ).rejects.toMatchObject({ status: 500 });

    await vi.advanceTimersByTimeAsync(5000);
    await assertion;

    expect(init).toHaveBeenCalledOnce();
  });

  it("rejects with the server's message when the file is refused", async () => {
    const { UploadItemError, api, sendUpload } = await loadBatches();

    vi.spyOn(api, "initializeUploadBatch").mockResolvedValue({
      results: [{ error: "Destination gone", uploadId: null, uploadUrl: null }],
    });

    const result = sendUpload(sendArgs(new AbortController().signal));
    const assertion = expect(result).rejects.toThrow(UploadItemError);

    await vi.advanceTimersByTimeAsync(50);
    await assertion;
    await expect(result).rejects.toThrow("Destination gone");
  });

  it("runs at most four PUTs at once and starts the next as one finishes", async () => {
    const { api, sendUpload } = await loadBatches();

    vi.spyOn(api, "initializeUploadBatch").mockImplementation((items) =>
      Promise.resolve({
        results: items.map((_, index) => ({
          error: null,
          uploadId: `id-${index}`,
          uploadUrl: `https://example.test/${index}`,
        })),
      }),
    );

    const finishers: (() => void)[] = [];

    const put = vi.spyOn(api, "uploadToS3").mockImplementation(
      () =>
        new Promise((resolve) => {
          finishers.push(resolve);
        }),
    );

    const uploads = Array.from({ length: 6 }, () =>
      sendUpload(sendArgs(new AbortController().signal)),
    );

    await vi.advanceTimersByTimeAsync(50);
    expect(put).toHaveBeenCalledTimes(4);

    finishers[0]();
    await vi.advanceTimersByTimeAsync(0);
    expect(put).toHaveBeenCalledTimes(5);

    finishers[1]();
    await vi.advanceTimersByTimeAsync(0);
    expect(put).toHaveBeenCalledTimes(6);

    for (const finish of finishers.slice(2)) finish();
    await Promise.all(uploads);
  });

  it("gives up its place when cancelled while waiting for a PUT slot", async () => {
    const { api, sendUpload } = await loadBatches();

    vi.spyOn(api, "initializeUploadBatch").mockImplementation((items) =>
      Promise.resolve({
        results: items.map((_, index) => ({
          error: null,
          uploadId: `id-${index}`,
          uploadUrl: `https://example.test/${index}`,
        })),
      }),
    );

    const finishers: (() => void)[] = [];

    const put = vi.spyOn(api, "uploadToS3").mockImplementation(
      () =>
        new Promise((resolve) => {
          finishers.push(resolve);
        }),
    );

    const running = Array.from({ length: 4 }, () =>
      sendUpload(sendArgs(new AbortController().signal)),
    );

    const controller = new AbortController();
    const waiting = sendUpload(sendArgs(controller.signal));
    const assertion = expect(waiting).rejects.toMatchObject({ name: "AbortError" });

    await vi.advanceTimersByTimeAsync(50);

    controller.abort();
    await assertion;

    for (const finish of finishers) finish();
    await Promise.all(running);

    expect(put).toHaveBeenCalledTimes(4);
  });
});

describe("finalizeUpload", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  const finalizeItem = (uploadId: string) => ({
    fileName: `${uploadId}.txt`,
    isEncrypted: false,
    uploadId,
  });

  it("isolates a failed file from the rest of its batch", async () => {
    const { UploadItemError, api, finalizeUpload } = await loadBatches();

    vi.spyOn(api, "finalizeUploadBatch").mockResolvedValue({
      results: [
        { error: null, success: true, uploadId: "good" },
        { error: "The file failed verification.", success: false, uploadId: "bad" },
      ],
    });

    const { signal } = new AbortController();

    const outcomes = Promise.allSettled([
      finalizeUpload(finalizeItem("good"), 1, signal),
      finalizeUpload(finalizeItem("bad"), 1, signal),
    ]);

    await vi.advanceTimersByTimeAsync(50);

    const [good, bad] = await outcomes;

    expect(good.status).toBe("fulfilled");
    expect(bad).toMatchObject({ status: "rejected" });
    expect((bad as PromiseRejectedResult).reason).toBeInstanceOf(UploadItemError);
    expect((bad as PromiseRejectedResult).reason.message).toBe("The file failed verification.");
  });

  it("splits finalize batches by total bytes", async () => {
    const { api, finalizeUpload } = await loadBatches();
    const gib = 1024 ** 3;

    const finalize = vi.spyOn(api, "finalizeUploadBatch").mockImplementation((items) =>
      Promise.resolve({
        results: items.map(({ uploadId }) => ({ error: null, success: true, uploadId })),
      }),
    );

    const { signal } = new AbortController();

    const uploads = [
      finalizeUpload(finalizeItem("a"), 0.6 * gib, signal),
      finalizeUpload(finalizeItem("b"), 0.6 * gib, signal),
    ];

    await vi.advanceTimersByTimeAsync(1000);
    await Promise.all(uploads);

    expect(finalize.mock.calls.map((call) => call[0].map((item) => item.uploadId))).toEqual([
      ["a"],
      ["b"],
    ]);
  });
});

describe("uploadToS3", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("rejects with the HTTP status so an expired URL can be recognised", async () => {
    let onLoad: (() => void) | undefined = undefined;

    const FakeXhr = function FakeXhr() {
      return {
        status: 403,
        upload: { addEventListener: vi.fn() },
        abort: vi.fn(),
        open: vi.fn(),
        send: () => onLoad?.(),
        setRequestHeader: vi.fn(),
        addEventListener(name: string, handler: () => void) {
          if (name === "load") onLoad = handler;
        },
      };
    };

    vi.stubGlobal("XMLHttpRequest", FakeXhr);

    await expect(
      fileApi.uploadToS3("https://example.test/upload", new Blob(["x"]), "text/plain"),
    ).rejects.toMatchObject({ kind: "http", status: 403 });
  });
});
