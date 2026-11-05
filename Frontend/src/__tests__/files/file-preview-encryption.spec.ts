import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";

import type { PreviewResultDto } from "@/api/file";

import FilePreview from "@/components/dashboard/file-system/FilePreview.vue";

const mockUseQuery = vi.fn();
const mockGetQueryData = vi.fn();
vi.mock("@pinia/colada", () => ({
  defineQueryOptions: (fn: unknown) => fn,
  useQuery: (...args: unknown[]) => mockUseQuery(...args),
  useQueryCache: () => ({ getQueryData: mockGetQueryData }),
}));
vi.mock("@/composables/useFileDownload", () => ({
  useFileDownload: () => ({ downloadVersion: vi.fn() }),
}));

interface PreviewQueryOptions {
  enabled?: boolean;
  key?: unknown[];
  staleTime?: number;
}

const previewQueryOptions = (): PreviewQueryOptions => {
  const getter = mockUseQuery.mock.calls[0]?.[0] as (() => PreviewQueryOptions) | undefined;
  if (!getter) throw new Error("useQuery was not called");

  return getter();
};

const mountPreview = (isEncrypted?: boolean, mimeType = "image/png") =>
  mount(FilePreview, {
    props: {
      currentVersionId: "v1",
      fileId: "f1",
      fileName: mimeType === "text/plain" ? "f1.txt" : "f1.png",
      isEncrypted,
      mimeType,
    },
  });

describe("FilePreview encryption gate", () => {
  beforeEach(() => {
    mockUseQuery.mockReset();
    mockGetQueryData.mockReset();
    mockUseQuery.mockReturnValue({
      data: ref<PreviewResultDto | null>(null),
      isLoading: ref(false),
    });
  });

  it("disables the preview query for encrypted files", () => {
    mountPreview(true);
    const options = previewQueryOptions();
    expect(options.enabled).toBe(false);
    expect(options.key).toEqual(["files", "preview-by-version", "f1", "v1"]);
  });

  it("keeps the preview query enabled for plain files", () => {
    mountPreview();
    expect(previewQueryOptions().enabled).toBe(true);
  });

  it("caches synchronous text previews by version indefinitely", () => {
    mountPreview(false, "text/plain");
    const options = previewQueryOptions();
    expect(options.key).toEqual(["files", "preview-by-version", "f1", "v1"]);
    expect(options.staleTime).toBe(Infinity);
  });

  it("keeps a retry window for asynchronously generated previews", () => {
    mountPreview(false, "image/png");
    expect(previewQueryOptions().staleTime).toBe(30_000);
  });

  it("does not keep an empty text preview for good", () => {
    mockGetQueryData.mockReturnValue({ archivePreview: null, previewUrl: null, textPreview: null });
    mountPreview(false, "text/plain");
    expect(previewQueryOptions().staleTime).toBe(30_000);
  });

  it("caches a text preview with content indefinitely", () => {
    mockGetQueryData.mockReturnValue({ archivePreview: null, previewUrl: null, textPreview: {} });
    mountPreview(false, "text/plain");
    expect(previewQueryOptions().staleTime).toBe(Infinity);
  });

  it("reads the cached entry for the version being previewed", () => {
    mountPreview(false, "text/plain");
    previewQueryOptions();
    expect(mockGetQueryData).toHaveBeenCalledWith(["files", "preview-by-version", "f1", "v1"]);
  });
});
