import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";

import { useFileStore } from "@/stores/file";

const mockDownloadFile = vi.fn();
const mockSearchFiles = vi.fn();
vi.mock("@/api/file", () => ({
  fileApi: {
    downloadFile: (...args: unknown[]) => mockDownloadFile(...args),
    searchFiles: (...args: unknown[]) => mockSearchFiles(...args),
  },
}));

describe("useFileStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    mockDownloadFile.mockReset();
    mockSearchFiles.mockReset();
  });

  it("starts with default values", () => {
    const store = useFileStore();
    expect(store.currentFile).toBeNull();
    expect(store.downloadProgress).toBe(0);
    expect(store.selectedFiles).toHaveLength(0);
    expect(store.modificationOriginDirId).toBeNull();
    expect(store.isUploading).toBe(false);
    expect(store.isDownloading).toBe(false);
    expect(store.error).toBeNull();
    expect(store.hasCurrentFile).toBe(false);
    expect(store.isProcessing).toBe(false);
  });

  it("hasCurrentFile is true when currentFile is set", () => {
    const store = useFileStore();
    expect(store.hasCurrentFile).toBe(false);
    store.currentFile = { id: "f1", name: "test.txt", hasPreview: false, previewGeneratedAt: null, updatedAt: null, updatedBy: null };
    expect(store.hasCurrentFile).toBe(true);
  });

  it("isProcessing is true when uploading or downloading", () => {
    const store = useFileStore();
    expect(store.isProcessing).toBe(false);
    store.isUploading = true;
    expect(store.isProcessing).toBe(true);
    store.isUploading = false;
    store.isDownloading = true;
    expect(store.isProcessing).toBe(true);
  });

  describe("downloadFile", () => {
    it("calls the API and triggers a download with forced download", async () => {
      const mockUrl = { presignedUrl: "https://example.com/file" };
      mockDownloadFile.mockResolvedValueOnce(mockUrl);

      const store = useFileStore();
      const result = await store.downloadFile({ id: "f1", fileName: "doc.txt", forceDownload: true });

      expect(result.success).toBe(true);
      expect(mockDownloadFile).toHaveBeenCalledWith("f1");
    });

    it("sets isDownloading during the call", async () => {
      mockDownloadFile.mockImplementationOnce(
        () => new Promise((resolve) => setTimeout(() => { resolve({ presignedUrl: `https://example.com/file` }); }, 50)),
      );

      const store = useFileStore();
      const promise = store.downloadFile({ id: "f1", forceDownload: true });
      expect(store.isDownloading).toBe(true);
      await promise;
      expect(store.isDownloading).toBe(false);
    });

    it("handles API errors gracefully", async () => {
      mockDownloadFile.mockRejectedValueOnce(new Error("File not found"));

      const store = useFileStore();
      const result = await store.downloadFile({ id: "missing" });

      expect(result.success).toBe(false);
      expect(result.error).toBe("File not found");
      expect(store.error).toBe("File not found");
    });

    it("handles Axios errors with response data", async () => {
      const axiosError = {
        isAxiosError: true,
        response: { data: { message: "Forbidden" } },
      };
      mockDownloadFile.mockRejectedValueOnce(axiosError);

      const store = useFileStore();
      const result = await store.downloadFile({ id: "f1" });

      expect(result.error).toBe("Forbidden");
    });
  });

  describe("searchFiles", () => {
    it("returns results on success", async () => {
      mockSearchFiles.mockResolvedValueOnce({ items: [{ fileId: "f1" }] });

      const store = useFileStore();
      const result = await store.searchFiles({ nameContains: "test" } as any);

      expect(result.success).toBe(true);
      expect(result.data?.items).toHaveLength(1);
    });

    it("handles errors", async () => {
      mockSearchFiles.mockRejectedValueOnce(new Error("Search failed"));

      const store = useFileStore();
      const result = await store.searchFiles({ nameContains: "test" } as any);

      expect(result.success).toBe(false);
      expect(store.error).toBe("Search failed");
    });
  });

  it("clearError resets the error", () => {
    const store = useFileStore();
    store.error = "Error";
    store.clearError();
    expect(store.error).toBeNull();
  });
});
