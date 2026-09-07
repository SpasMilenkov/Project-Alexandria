import { mount } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, nextTick, ref } from "vue";

import { attemptRefresh } from "@/api/client";
import { useFileThumbnail } from "@/composables/useFileThumbnail";

vi.mock("@/api/client", () => ({
  attemptRefresh: vi.fn(),
}));

const mockedAttemptRefresh = vi.mocked(attemptRefresh);

const RETRY_DELAYS = [2_000, 5_000, 10_000, 30_000, 60_000];

const mountHost = (fileId = "file-123", versionId = "version-1", mimeType?: string | null) => {
  const fileIdRef = ref(fileId);
  const versionIdRef = ref(versionId);
  const mimeTypeRef = ref(mimeType);
  const wrapper = mount(
    defineComponent({
      setup() {
        return {
          ...useFileThumbnail(() => ({
            fileId: fileIdRef.value,
            mimeType: mimeTypeRef.value,
            versionId: versionIdRef.value,
          })),
        };
      },
      template: "<div />",
    }),
  );
  return { fileIdRef, mimeTypeRef, versionIdRef, wrapper };
};

describe("useFileThumbnail", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
    mockedAttemptRefresh.mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("builds the version-scoped thumbnail url", () => {
    const { wrapper } = mountHost();
    expect(wrapper.vm.thumbnailUrl).toBe("/api/files/file-123/versions/version-1/thumbnail");
  });

  it("tracks load flags", () => {
    const { wrapper } = mountHost();
    expect(wrapper.vm.thumbnailLoaded).toBe(false);
    expect(wrapper.vm.thumbnailErrored).toBe(false);
    wrapper.vm.onThumbnailLoad();
    expect(wrapper.vm.thumbnailLoaded).toBe(true);
  });

  it("heals the first error with one shared refresh and retries with backoff", () => {
    const { wrapper } = mountHost();
    wrapper.vm.onThumbnailError();
    expect(mockedAttemptRefresh).toHaveBeenCalledTimes(1);
    expect(wrapper.vm.thumbnailErrored).toBe(false);
    expect(wrapper.vm.thumbnailUrl).toBe("/api/files/file-123/versions/version-1/thumbnail");
    vi.advanceTimersByTime(2_000);
    expect(wrapper.vm.thumbnailUrl).toBe(
      "/api/files/file-123/versions/version-1/thumbnail?retry=1",
    );
    expect(wrapper.vm.thumbnailErrored).toBe(false);
  });

  it("ignores duplicate errors while a retry is already scheduled", () => {
    const { wrapper } = mountHost();
    wrapper.vm.onThumbnailError();
    wrapper.vm.onThumbnailError();
    vi.advanceTimersByTime(2_000);
    expect(wrapper.vm.thumbnailUrl).toBe(
      "/api/files/file-123/versions/version-1/thumbnail?retry=1",
    );
    expect(mockedAttemptRefresh).toHaveBeenCalledTimes(1);
  });

  it("retries through the budget then latches the icon", () => {
    const { wrapper } = mountHost();
    for (const delay of RETRY_DELAYS) {
      wrapper.vm.onThumbnailError();
      vi.advanceTimersByTime(delay);
    }
    expect(wrapper.vm.thumbnailErrored).toBe(false);
    wrapper.vm.onThumbnailError();
    expect(wrapper.vm.thumbnailErrored).toBe(true);
    expect(mockedAttemptRefresh).toHaveBeenCalledTimes(1);
  });

  it("keeps retrying when the refresh fails", () => {
    mockedAttemptRefresh.mockRejectedValueOnce(new Error("refresh failed"));
    const { wrapper } = mountHost();
    wrapper.vm.onThumbnailError();
    vi.advanceTimersByTime(2_000);
    expect(wrapper.vm.thumbnailUrl).toBe(
      "/api/files/file-123/versions/version-1/thumbnail?retry=1",
    );
    expect(wrapper.vm.thumbnailErrored).toBe(false);
  });

  it("keeps loaded state on a mimeType-only change (src is unchanged)", async () => {
    const { mimeTypeRef, wrapper } = mountHost("file-123", "version-1", "image/png");
    wrapper.vm.onThumbnailLoad();
    expect(wrapper.vm.thumbnailLoaded).toBe(true);
    mimeTypeRef.value = "image/jpeg";
    await nextTick();
    expect(wrapper.vm.thumbnailLoaded).toBe(true);
    expect(wrapper.vm.thumbnailUrl).toBe("/api/files/file-123/versions/version-1/thumbnail");
  });

  it("drops a pending retry when the file or version changes", async () => {
    const { versionIdRef, wrapper } = mountHost();
    wrapper.vm.onThumbnailError();
    versionIdRef.value = "version-2";
    await nextTick();
    vi.advanceTimersByTime(60_000);
    expect(wrapper.vm.thumbnailLoaded).toBe(false);
    expect(wrapper.vm.thumbnailErrored).toBe(false);
    expect(wrapper.vm.thumbnailUrl).toBe("/api/files/file-123/versions/version-2/thumbnail");
  });

  it("allows thumbnails when mimeType is omitted for backwards compatibility", () => {
    const { wrapper } = mountHost();
    expect(wrapper.vm.canHaveThumbnail).toBe(true);
  });

  it("guards thumbnail requests by mimeType", async () => {
    const { mimeTypeRef, wrapper } = mountHost("file-123", "version-1", "application/json");
    expect(wrapper.vm.canHaveThumbnail).toBe(false);
    expect(wrapper.vm.thumbnailUrl).toBe("/api/files/file-123/versions/version-1/thumbnail");
    mimeTypeRef.value = "image/png";
    await nextTick();
    expect(wrapper.vm.canHaveThumbnail).toBe(true);
  });
});
