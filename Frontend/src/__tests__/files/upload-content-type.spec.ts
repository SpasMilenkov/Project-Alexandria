import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { fileApi } from "@/api/file";
import {
  FALLBACK_CONTENT_TYPE,
  type FileUploadStatus,
  useDirectoryUpload,
} from "@/composables/useDirectoryUpload";

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

const readSource = (relativePath: string) =>
  readFileSync(fileURLToPath(new URL(relativePath, import.meta.url)), "utf8");

const makeStatus = (file: File): FileUploadStatus => ({
  abortController: new AbortController(),
  file,
  progress: 0,
  relativePath: file.name,
  status: "pending",
});

describe("upload content type", () => {
  beforeEach(() => {
    vi.stubGlobal("Worker", FakeWorker);
    vi.spyOn(fileApi, "initializeUpload").mockResolvedValue({
      uploadId: "upload-id",
      uploadUrl: "https://example.test/upload",
    });
    vi.spyOn(fileApi, "uploadToS3").mockResolvedValue();
    vi.spyOn(fileApi, "finalizeUpload").mockResolvedValue({ success: true });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("V01 sends octet-stream to initialize and the same value to the PUT for an untyped file", async () => {
    const { uploadSingleFile } = useDirectoryUpload();
    const file = new File(["hello"], "hello.bin");

    await uploadSingleFile(makeStatus(file));

    expect(fileApi.initializeUpload).toHaveBeenCalledWith(
      expect.objectContaining({ contentType: FALLBACK_CONTENT_TYPE }),
    );
    expect(fileApi.uploadToS3).toHaveBeenCalledWith(
      "https://example.test/upload",
      file,
      FALLBACK_CONTENT_TYPE,
      expect.any(Function),
      expect.any(AbortSignal),
    );
  });

  it("V02 sends the browser type to initialize and the same value to the PUT", async () => {
    const { uploadSingleFile } = useDirectoryUpload();
    const file = new File(["png"], "photo.png", { type: "image/png" });

    await uploadSingleFile(makeStatus(file));

    expect(fileApi.initializeUpload).toHaveBeenCalledWith(
      expect.objectContaining({ contentType: "image/png" }),
    );
    expect(fileApi.uploadToS3).toHaveBeenCalledWith(
      "https://example.test/upload",
      file,
      "image/png",
      expect.any(Function),
      expect.any(AbortSignal),
    );
  });
});

describe("presigned PUT content type", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("V03 sets the Content-Type header from the explicit argument, not file.type", async () => {
    const setRequestHeader = vi.fn();
    let loadHandler: (() => void) | undefined = undefined;

    const FakeXhr = function FakeXhr() {
      return {
        status: 200,
        upload: { addEventListener: vi.fn() },
        abort: vi.fn(),
        open: vi.fn(),
        send: () => loadHandler?.(),
        setRequestHeader,
        addEventListener(name: string, handler: () => void) {
          if (name === "load") loadHandler = handler;
        },
      };
    };

    vi.stubGlobal("XMLHttpRequest", FakeXhr);

    const file = new File(["png"], "photo.png", { type: "image/png" });

    await fileApi.uploadToS3("https://example.test/upload", file, "application/octet-stream");

    expect(setRequestHeader).toHaveBeenCalledWith("Content-Type", "application/octet-stream");
  });
});

describe("archive extraction", () => {
  it("V04 builds extracted files without a MIME type", () => {
    const source = readSource(
      "../components/dashboard/file-system/Modals/ArchiveUploadModal.vue",
    );

    expect(source).toContain("new File([blob], fileName)");
    expect(source).not.toContain("mimeFromExtension");
  });

  it("V05 no longer contains the extension MIME table", () => {
    const source = readSource(
      "../components/dashboard/file-system/Modals/ArchiveUploadModal.vue",
    );

    expect(source).not.toContain("EXTENSION_MIME");
  });
});
