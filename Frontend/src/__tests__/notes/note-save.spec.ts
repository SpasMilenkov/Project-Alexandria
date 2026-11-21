import { PiniaColada } from "@pinia/colada";
import { createPinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createApp, defineComponent, nextTick, ref } from "vue";

import type { FileContentSaveResult } from "@/api/notes";

import { ApiError } from "@/api/errors";
import { IDLE_SAVE_MS, useNoteSave } from "@/composables/useNoteSave";

const { mockSaveContent } = vi.hoisted(() => ({ mockSaveContent: vi.fn() }));

vi.mock("@/api/notes", () => ({ notesApi: { saveContent: mockSaveContent } }));

type Saver = ReturnType<typeof useNoteSave>;

const savedAs = (versionId: string, unchanged = false): FileContentSaveResult => ({
  created: false,
  directoryId: "folder-1",
  fileId: "file-1",
  fileName: "notes.md",
  mimeType: "text/markdown",
  size: 10,
  unchanged,
  versionId,
  versionNumber: 2,
});

const mountSaver = (onSaved?: (saved: FileContentSaveResult, count: number) => void) => {
  const draft = ref("");
  const captured = {} as { saver: Saver };
  const app = createApp(
    defineComponent({
      setup() {
        captured.saver = useNoteSave({ draft, fileId: "file-1", onSaved });
        return () => null;
      },
    }),
  );

  app.use(createPinia());
  app.use(PiniaColada);
  app.mount(document.createElement("div"));

  return { app, draft, saver: captured.saver };
};

// A promise the test settles by hand, to hold a save in flight.
const deferred = <T>() => {
  const settle: { resolve: (value: T) => void } = { resolve: () => undefined };
  const promise = new Promise<T>((resolve) => {
    settle.resolve = resolve;
  });

  return { promise, resolve: (value: T) => settle.resolve(value) };
};

describe("useNoteSave", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    mockSaveContent.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("saves after a pause, each save made from the version before it", async () => {
    mockSaveContent.mockResolvedValueOnce(savedAs("v2")).mockResolvedValueOnce(savedAs("v3"));
    const { app, draft, saver } = mountSaver();

    saver.start("v1", "# Start");
    draft.value = "# Start\n\nMore";
    await nextTick();
    expect(saver.isDirty.value).toBe(true);

    await vi.advanceTimersByTimeAsync(IDLE_SAVE_MS);
    expect(mockSaveContent).toHaveBeenLastCalledWith({
      content: "# Start\n\nMore",
      fileId: "file-1",
      parentVersionId: "v1",
    });
    expect(saver.isDirty.value).toBe(false);
    expect(saver.status.value).toBe("saved");

    draft.value = "# Start\n\nMore and more";
    await nextTick();
    await vi.advanceTimersByTimeAsync(IDLE_SAVE_MS);
    expect(mockSaveContent).toHaveBeenLastCalledWith(expect.objectContaining({ parentVersionId: "v2" }));
    app.unmount();
  });

  it("doesn't save before the pause or when nothing changed", async () => {
    const { app, draft, saver } = mountSaver();

    saver.start("v1", "# Start");
    draft.value = "# Start\n\nMore";
    await nextTick();
    await vi.advanceTimersByTimeAsync(IDLE_SAVE_MS - 100);
    expect(mockSaveContent).not.toHaveBeenCalled();

    // Undoing back to the stored text leaves nothing to save.
    draft.value = "# Start";
    await nextTick();
    await vi.advanceTimersByTimeAsync(IDLE_SAVE_MS);
    expect(mockSaveContent).not.toHaveBeenCalled();
    await expect(saver.save()).resolves.toBe(true);
    expect(mockSaveContent).not.toHaveBeenCalled();
    app.unmount();
  });

  it("sends edits made during a save in a follow-up save, never two at once", async () => {
    const first = deferred<FileContentSaveResult>();
    mockSaveContent.mockReturnValueOnce(first.promise).mockResolvedValueOnce(savedAs("v3"));
    const { app, draft, saver } = mountSaver();

    saver.start("v1", "a");
    draft.value = "ab";
    const firstSave = saver.save();
    draft.value = "abc";
    const secondSave = saver.save();
    // Flushes pending promise work, so only a request held back on purpose stays unsent.
    await vi.advanceTimersByTimeAsync(0);
    expect(mockSaveContent).toHaveBeenCalledTimes(1);

    first.resolve(savedAs("v2"));
    await expect(firstSave).resolves.toBe(true);
    await expect(secondSave).resolves.toBe(true);
    expect(mockSaveContent).toHaveBeenCalledTimes(2);
    expect(mockSaveContent).toHaveBeenLastCalledWith(
      expect.objectContaining({ content: "abc", parentVersionId: "v2" }),
    );
    app.unmount();
  });

  it("stops on a conflict until the edit is kept over the latest version", async () => {
    mockSaveContent
      .mockRejectedValueOnce(new ApiError("Conflict", { kind: "http", status: 409 }))
      .mockResolvedValueOnce(savedAs("v6"));
    const { app, draft, saver } = mountSaver();

    saver.start("v1", "a");
    draft.value = "mine";
    await expect(saver.save()).resolves.toBe(false);
    expect(saver.status.value).toBe("conflict");

    draft.value = "mine, still typing";
    await nextTick();
    await vi.advanceTimersByTimeAsync(IDLE_SAVE_MS);
    expect(mockSaveContent).toHaveBeenCalledTimes(1);

    await expect(saver.saveOver("v5")).resolves.toBe(true);
    expect(mockSaveContent).toHaveBeenLastCalledWith(
      expect.objectContaining({ content: "mine, still typing", parentVersionId: "v5" }),
    );
    expect(saver.status.value).toBe("saved");
    app.unmount();
  });

  it("reports other failures as errors that the next save retries", async () => {
    mockSaveContent
      .mockRejectedValueOnce(new ApiError("Down", { kind: "network" }))
      .mockResolvedValueOnce(savedAs("v2"));
    const { app, draft, saver } = mountSaver();

    saver.start("v1", "a");
    draft.value = "b";
    await expect(saver.save()).resolves.toBe(false);
    expect(saver.status.value).toBe("error");
    expect(saver.isDirty.value).toBe(true);

    await expect(saver.save()).resolves.toBe(true);
    expect(mockSaveContent).toHaveBeenLastCalledWith(expect.objectContaining({ parentVersionId: "v1" }));
    app.unmount();
  });

  it("ignores a save that finishes after another note was opened", async () => {
    const late = deferred<FileContentSaveResult>();
    mockSaveContent.mockReturnValueOnce(late.promise);
    const { app, draft, saver } = mountSaver();

    saver.start("v1", "a");
    draft.value = "b";
    const pending = saver.save();

    saver.reset();
    draft.value = "";
    saver.start("other-v1", "");

    late.resolve(savedAs("v2"));
    await pending;
    expect(saver.baseVersionId.value).toBe("other-v1");
    expect(saver.status.value).toBe("idle");
    app.unmount();
  });

  it("tells the caller about saves that wrote a version, counting from one", async () => {
    const onSaved = vi.fn();
    mockSaveContent.mockResolvedValueOnce(savedAs("v1", true)).mockResolvedValueOnce(savedAs("v2"));
    const { app, draft, saver } = mountSaver(onSaved);

    saver.start("v1", "a");
    draft.value = "a\r\n";
    // Line endings normalise away, so this one isn't even sent.
    await saver.save();
    draft.value = "b";
    await saver.save();
    draft.value = "c";
    await saver.save();

    expect(onSaved).toHaveBeenCalledTimes(1);
    expect(onSaved).toHaveBeenCalledWith(expect.objectContaining({ versionId: "v2" }), 1);
    app.unmount();
  });
});
