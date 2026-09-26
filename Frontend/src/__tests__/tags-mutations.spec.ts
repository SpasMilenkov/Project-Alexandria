import { PiniaColada, useQueryCache } from "@pinia/colada";
import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { createApp, defineComponent } from "vue";

import { FILES_QUERY_KEYS } from "@/queries/files";
import { TAGS_QUERY_KEYS } from "@/queries/tags";

const { mockAddTagsToFile, mockRemoveTagFromFile } = vi.hoisted(() => ({
  mockAddTagsToFile: vi.fn(),
  mockRemoveTagFromFile: vi.fn(),
}));

vi.mock("@/api/tag", () => ({
  tagApi: {
    addTagsToFile: mockAddTagsToFile,
    removeTagFromFile: mockRemoveTagFromFile,
  },
}));

const TAGS_FOR_FILE_KEY = ["tags", "tags-for-file", "file-1"];
const GET_FILE_KEY = ["files", "file", "file-1"];

const expectTagInvalidations = (invalidate: ReturnType<typeof vi.fn>) => {
  const keys = invalidate.mock.calls.map(([arg]) => arg);
  expect(keys).toContainEqual({ exact: true, key: TAGS_QUERY_KEYS.getTagsForFile("file-1") });
  expect(keys).toContainEqual({ exact: true, key: FILES_QUERY_KEYS.getFile("file-1") });
  expect(TAGS_QUERY_KEYS.getTagsForFile("file-1")).toEqual(TAGS_FOR_FILE_KEY);
  expect(FILES_QUERY_KEYS.getFile("file-1")).toEqual(GET_FILE_KEY);
};

const expectNoBroadInvalidations = (invalidate: ReturnType<typeof vi.fn>) => {
  const keys = invalidate.mock.calls.map(([arg]) => arg);
  const broad = keys.some((arg) => {
    const key = (arg as { key?: unknown }).key;
    if (!Array.isArray(key)) return false;
    if ((arg as { exact?: boolean }).exact) return false;
    return true;
  });
  expect(broad).toBe(false);
};

describe("tag file mutations", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    mockAddTagsToFile.mockReset();
    mockRemoveTagFromFile.mockReset();
    mockAddTagsToFile.mockResolvedValue({ fileId: "file-1", message: "ok", tagsAdded: 1 });
    mockRemoveTagFromFile.mockResolvedValue(undefined);
  });

  const mountMutations = async () => {
    const { addTagToFile, removeTagFromFile } = await import("@/mutations/tags");
    const captured = {} as {
      add: ReturnType<typeof addTagToFile>;
      invalidate: ReturnType<typeof vi.fn>;
      remove: ReturnType<typeof removeTagFromFile>;
    };
    const pinia = createPinia();
    const app = createApp(
      defineComponent({
        setup() {
          captured.add = addTagToFile();
          captured.remove = removeTagFromFile();
          const cache = useQueryCache();
          captured.invalidate = vi
            .spyOn(cache, "invalidateQueries")
            .mockImplementation(() => {});
          return () => null;
        },
      }),
    );
    app.use(pinia);
    app.use(PiniaColada);
    app.mount(document.createElement("div"));
    return { add: captured.add, app, invalidate: captured.invalidate, remove: captured.remove };
  };

  it("removeTagFromFile refreshes drawer, detail, and listing state", async () => {
    const { app, invalidate, remove } = await mountMutations();
    await remove.mutateAsync({ fileId: "file-1", tagId: "tag-9" });

    expectTagInvalidations(invalidate);
    app.unmount();
  });

  it("addTagToFile refreshes drawer, detail, and listing state", async () => {
    const { add, app, invalidate } = await mountMutations();

    await add.mutateAsync({ data: { tagIds: ["tag-9"] }, fileId: "file-1" });

    expectTagInvalidations(invalidate);
    app.unmount();
  });

  it("addTagToFile falls back to request vars when the result lacks a file id", async () => {
    mockAddTagsToFile.mockResolvedValueOnce({});
    const { app, invalidate, add } = await mountMutations();

    await add.mutateAsync({ data: { tagIds: ["tag-9"] }, fileId: "file-1" });

    expectTagInvalidations(invalidate);
    app.unmount();
  });

  it("tag changes leave listings, preview, and versions queries alone", async () => {
    const { app, invalidate, remove } = await mountMutations();
    await remove.mutateAsync({ fileId: "file-1", tagId: "tag-9" });

    expectNoBroadInvalidations(invalidate);
    const keys = invalidate.mock.calls.map(([arg]) => arg);
    const touchesPreviewOrVersions = keys.some((arg) => {
      const key = (arg as { key?: unknown }).key;
      if (!Array.isArray(key)) return false;
      return (
        key.includes("preview") ||
        key.includes("preview-by-version") ||
        key.includes("versions-for-file") ||
        key.includes("version-signed-url")
      );
    });
    expect(touchesPreviewOrVersions).toBe(false);
    expect(invalidate).toHaveBeenCalledTimes(2);
    app.unmount();
  });
});
