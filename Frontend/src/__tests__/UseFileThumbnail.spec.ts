import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import { defineComponent, nextTick, ref } from "vue";

import { useFileThumbnail } from "@/composables/useFileThumbnail";

const mountHost = (fileId = "file-123", versionId = "version-1") => {
  const fileIdRef = ref(fileId);
  const versionIdRef = ref(versionId);
  const wrapper = mount(
    defineComponent({
      setup() {
        return {
          ...useFileThumbnail(() => ({
            fileId: fileIdRef.value,
            versionId: versionIdRef.value,
          })),
        };
      },
      template: "<div />",
    }),
  );
  return { fileIdRef, versionIdRef, wrapper };
};

describe("useFileThumbnail", () => {
  it("builds the version-scoped thumbnail url", () => {
    const { wrapper } = mountHost();
    expect(wrapper.vm.thumbnailUrl).toBe("/api/files/file-123/versions/version-1/thumbnail");
  });

  it("tracks load and error flags", () => {
    const { wrapper } = mountHost();
    expect(wrapper.vm.thumbnailLoaded).toBe(false);
    expect(wrapper.vm.thumbnailErrored).toBe(false);
    wrapper.vm.onThumbnailLoad();
    expect(wrapper.vm.thumbnailLoaded).toBe(true);
    wrapper.vm.onThumbnailError();
    expect(wrapper.vm.thumbnailErrored).toBe(true);
  });

  it("resets flags when the file or version changes", async () => {
    const { versionIdRef, wrapper } = mountHost();
    wrapper.vm.onThumbnailLoad();
    expect(wrapper.vm.thumbnailLoaded).toBe(true);
    versionIdRef.value = "version-2";
    await nextTick();
    expect(wrapper.vm.thumbnailLoaded).toBe(false);
    expect(wrapper.vm.thumbnailUrl).toBe("/api/files/file-123/versions/version-2/thumbnail");
  });
});
