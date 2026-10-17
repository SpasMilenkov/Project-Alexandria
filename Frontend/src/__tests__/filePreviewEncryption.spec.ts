import { mount } from "@vue/test-utils";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";

import type { PreviewResultDto } from "@/api/file";

import FilePreview from "@/components/dashboard/file-system/FilePreview.vue";

const mockUseQuery = vi.fn();
vi.mock("@pinia/colada", () => ({
  defineQueryOptions: (fn: unknown) => fn,
  useQuery: (...args: unknown[]) => mockUseQuery(...args),
}));

interface PreviewQueryOptions {
  enabled?: boolean;
  key?: unknown[];
}

const previewQueryOptions = (): PreviewQueryOptions => {
  const getter = mockUseQuery.mock.calls[0]?.[0] as (() => PreviewQueryOptions) | undefined;
  if (!getter) throw new Error("useQuery was not called");

  return getter();
};

const mountPreview = (isEncrypted?: boolean) =>
  mount(FilePreview, {
    props: {
      currentVersionId: "v1",
      fileId: "f1",
      fileName: "f1.png",
      isEncrypted,
      mimeType: "image/png",
    },
  });

describe("FilePreview encryption gate", () => {
  beforeEach(() => {
    mockUseQuery.mockReset();
    mockUseQuery.mockReturnValue({
      data: ref<PreviewResultDto | null>(null),
      isLoading: ref(false),
    });
  });

  it("disables the preview query for encrypted files", () => {
    mountPreview(true);
    const options = previewQueryOptions();
    expect(options.enabled).toBe(false);
    expect(options.key).toContain("preview");
  });

  it("keeps the preview query enabled for plain files", () => {
    mountPreview();
    expect(previewQueryOptions().enabled).toBe(true);
  });
});
