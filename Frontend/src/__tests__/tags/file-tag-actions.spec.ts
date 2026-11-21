import { PiniaColada } from "@pinia/colada";
import { createPinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { type Ref, createApp, defineComponent, ref } from "vue";

import { useFileTagActions } from "@/composables/useFileTagActions";

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

const toast = { error: vi.fn(), info: vi.fn(), success: vi.fn() };
vi.mock("@/composables/useAppToast", () => ({ useAppToast: () => toast }));

type Actions = ReturnType<typeof useFileTagActions>;

const mountActions = (fileId: Ref<string>) => {
  const captured = {} as { actions: Actions };
  const app = createApp(
    defineComponent({
      setup() {
        captured.actions = useFileTagActions({ fileId, fileLabel: '"notes.md"' });
        return () => null;
      },
    }),
  );

  app.use(createPinia());
  app.use(PiniaColada);
  app.mount(document.createElement("div"));

  return { actions: captured.actions, app };
};

// The undo button the last success toast offered.
const undoFromLastToast = (): (() => void) => {
  const [, options] = toast.success.mock.calls.at(-1) ?? [];
  const action = options?.actions?.[0];

  if (!action) throw new Error("The toast offered no undo");

  return action.onClick;
};

describe("useFileTagActions", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAddTagsToFile.mockResolvedValue({ fileId: "file-1", message: "ok", tagsAdded: 1 });
    mockRemoveTagFromFile.mockResolvedValue(undefined);
  });

  it("adds a tag and reports success to the caller", async () => {
    const { actions, app } = mountActions(ref("file-1"));

    await expect(actions.addTag("tag-1")).resolves.toBe(true);
    expect(mockAddTagsToFile).toHaveBeenCalledWith("file-1", { tagIds: ["tag-1"] });
    expect(toast.error).not.toHaveBeenCalled();
    app.unmount();
  });

  it("names the file in the error toast when adding fails", async () => {
    mockAddTagsToFile.mockRejectedValueOnce(new Error("offline"));
    const { actions, app } = mountActions(ref("file-1"));

    await expect(actions.addTag("tag-1")).resolves.toBe(false);
    expect(toast.error).toHaveBeenCalledWith(
      'Couldn\'t add the tag to "notes.md"',
      expect.any(Error),
    );
    app.unmount();
  });

  it("offers an undo after removing that puts the tag back", async () => {
    const { actions, app } = mountActions(ref("file-1"));

    await expect(actions.removeTag("tag-1", "travel")).resolves.toBe(true);
    expect(mockRemoveTagFromFile).toHaveBeenCalledWith("file-1", "tag-1");
    expect(toast.success).toHaveBeenCalledWith('Removed "travel"', expect.any(Object));

    undoFromLastToast()();
    await vi.waitFor(() =>
      expect(mockAddTagsToFile).toHaveBeenCalledWith("file-1", { tagIds: ["tag-1"] }),
    );
    app.unmount();
  });

  it("restores to the file the tag came from, even after switching files", async () => {
    const fileId = ref("file-1");
    const { actions, app } = mountActions(fileId);

    await actions.removeTag("tag-1", "travel");
    fileId.value = "file-2";

    undoFromLastToast()();
    await vi.waitFor(() =>
      expect(mockAddTagsToFile).toHaveBeenCalledWith("file-1", { tagIds: ["tag-1"] }),
    );
    app.unmount();
  });

  it("reports a failed removal without offering an undo", async () => {
    mockRemoveTagFromFile.mockRejectedValueOnce(new Error("offline"));
    const { actions, app } = mountActions(ref("file-1"));

    await expect(actions.removeTag("tag-1", "travel")).resolves.toBe(false);
    expect(toast.error).toHaveBeenCalledWith('Couldn\'t remove "travel"', expect.any(Error));
    expect(toast.success).not.toHaveBeenCalled();
    app.unmount();
  });
});
