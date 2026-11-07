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

const mountHost = (
  fileId = "file-123",
  versionId = "version-1",
  mimeType?: string | null,
  isEncrypted?: boolean,
  coverUpdatedAt?: string | null,
) => {
  const fileIdRef = ref(fileId);
  const versionIdRef = ref(versionId);
  const mimeTypeRef = ref(mimeType);
  const isEncryptedRef = ref(isEncrypted);
  const coverUpdatedAtRef = ref(coverUpdatedAt);
  const wrapper = mount(
    defineComponent({
      setup() {
        return {
          ...useFileThumbnail(() => ({
            coverUpdatedAt: coverUpdatedAtRef.value,
            fileId: fileIdRef.value,
            isEncrypted: isEncryptedRef.value,
            mimeType: mimeTypeRef.value,
            versionId: versionIdRef.value,
          })),
        };
      },
      template: "<div />",
    }),
  );
  return { coverUpdatedAtRef, fileIdRef, isEncryptedRef, mimeTypeRef, versionIdRef, wrapper };
};

const COVER_STAMP = "2026-11-07T12:00:00Z";
const STAMPED_URL = `/api/files/file-123/versions/version-1/thumbnail?v=${encodeURIComponent(COVER_STAMP)}`;

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

  it("gates thumbnail requests for encrypted versions", async () => {
    const { isEncryptedRef, wrapper } = mountHost("file-123", "version-1", "image/png", true);
    expect(wrapper.vm.canHaveThumbnail).toBe(false);
    isEncryptedRef.value = false;
    await nextTick();
    expect(wrapper.vm.canHaveThumbnail).toBe(true);
  });

  it("allows thumbnails when isEncrypted is omitted for backwards compatibility", () => {
    const { wrapper } = mountHost("file-123", "version-1", "image/png");
    expect(wrapper.vm.canHaveThumbnail).toBe(true);
  });

  it("adds the cover stamp to the version url", () => {
    const { wrapper } = mountHost("file-123", "version-1", "audio/mpeg", false, COVER_STAMP);
    expect(wrapper.vm.thumbnailUrl).toBe(STAMPED_URL);
  });

  it("keeps the cover stamp when retrying", () => {
    const { wrapper } = mountHost("file-123", "version-1", "audio/mpeg", false, COVER_STAMP);
    wrapper.vm.onThumbnailError();
    vi.advanceTimersByTime(2_000);
    expect(wrapper.vm.thumbnailUrl).toBe(`${STAMPED_URL}&retry=1`);
  });

  it("resets load state when the cover changes so the new image can show", async () => {
    const { coverUpdatedAtRef, wrapper } = mountHost("file-123", "version-1", "audio/mpeg", false);
    wrapper.vm.onThumbnailLoad();
    expect(wrapper.vm.thumbnailLoaded).toBe(true);
    coverUpdatedAtRef.value = COVER_STAMP;
    await nextTick();
    expect(wrapper.vm.thumbnailLoaded).toBe(false);
    expect(wrapper.vm.thumbnailUrl).toBe(STAMPED_URL);
  });

  it("recovers the retry budget when a removed cover is regenerating", async () => {
    const { coverUpdatedAtRef, wrapper } = mountHost(
      "file-123",
      "version-1",
      "audio/mpeg",
      false,
      COVER_STAMP,
    );
    wrapper.vm.onThumbnailError();
    vi.advanceTimersByTime(2_000);
    coverUpdatedAtRef.value = null;
    await nextTick();
    expect(wrapper.vm.thumbnailUrl).toBe("/api/files/file-123/versions/version-1/thumbnail");
    expect(wrapper.vm.thumbnailErrored).toBe(false);
  });

  it("treats a null and a missing stamp as the same cover", async () => {
    const { coverUpdatedAtRef, wrapper } = mountHost("file-123", "version-1", "audio/mpeg", false);
    wrapper.vm.onThumbnailLoad();
    coverUpdatedAtRef.value = null;
    await nextTick();
    expect(wrapper.vm.thumbnailLoaded).toBe(true);
  });
});
