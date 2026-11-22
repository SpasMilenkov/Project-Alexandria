import { PiniaColada } from "@pinia/colada";
import { createPinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createApp, defineComponent, nextTick, ref } from "vue";

import type { FileContentSaveResult, NoteDraftSaveResult } from "@/api/notes";

import { ApiError } from "@/api/errors";
import {
  CHECKPOINT_INTERVAL_MS,
  type CurrentVersion,
  IDLE_SAVE_MS,
  type NoteOpening,
  useNoteSave,
} from "@/composables/useNoteSave";

const { mockSaveContent, mockSaveDraft } = vi.hoisted(() => ({
  mockSaveContent: vi.fn(),
  mockSaveDraft: vi.fn(),
}));

vi.mock("@/api/notes", () => ({
  notesApi: { saveContent: mockSaveContent, saveDraft: mockSaveDraft },
}));

type Saver = ReturnType<typeof useNoteSave>;

const CONFLICT = new ApiError("Conflict", { kind: "http", status: 409 });

const VERSION_SAVED: FileContentSaveResult = {
  created: false,
  directoryId: "folder-1",
  fileId: "file-1",
  fileName: "notes.md",
  mimeType: "text/markdown",
  size: 10,
  unchanged: false,
  versionId: "v2",
  versionNumber: 2,
};

const DRAFT_SAVED: NoteDraftSaveResult = {
  renamedTo: null,
  revision: "r1",
  savedAt: "2026-11-22T10:00:00Z",
};

const OPENING: NoteOpening = {
  draft: null,
  draftIsOutdated: false,
  versionId: "v1",
  versionText: "# Start",
};

const noCurrentVersion = () => Promise.resolve(null);

const mountSaver = (
  readCurrentVersion: () => Promise<CurrentVersion | null> = noCurrentVersion,
) => {
  const draft = ref("");
  const captured = {} as { saver: Saver };
  const app = createApp(
    defineComponent({
      setup() {
        captured.saver = useNoteSave({ draft, fileId: "file-1", readCurrentVersion });
        return () => null;
      },
    }),
  );

  app.use(createPinia());
  app.use(PiniaColada);
  app.mount(document.createElement("div"));

  return { app, draft, saver: captured.saver };
};

// The view loads the opening text into the editor before saving starts.
const open = (saver: Saver, draft: { value: string }, opening: NoteOpening) => {
  draft.value = opening.draft?.content ?? opening.versionText;
  saver.start(opening);
};

// A promise the test settles by hand, to hold a save in flight.
const deferred = <T>() => {
  const settle: { resolve: (value: T) => void } = { resolve: () => undefined };
  const promise = new Promise<T>((resolve) => {
    settle.resolve = resolve;
  });

  return { promise, resolve: (value: T) => settle.resolve(value) };
};

beforeEach(() => {
  vi.useFakeTimers();
  mockSaveContent.mockReset();
  mockSaveDraft.mockReset();
});

afterEach(() => {
  vi.useRealTimers();
});

describe("useNoteSave drafts and checkpoints", () => {
  it("keeps pauses in a draft, naming the revision it last wrote, and makes no version", async () => {
    mockSaveDraft
      .mockResolvedValueOnce(DRAFT_SAVED)
      .mockResolvedValueOnce({ ...DRAFT_SAVED, revision: "r2" });
    const { app, draft, saver } = mountSaver();

    open(saver, draft, OPENING);
    draft.value = "# Start\n\nMore";
    await nextTick();
    expect(saver.isDirty.value).toBe(true);

    await vi.advanceTimersByTimeAsync(IDLE_SAVE_MS);
    expect(mockSaveDraft).toHaveBeenLastCalledWith({
      baseVersionId: "v1",
      content: "# Start\n\nMore",
      expectedRevision: null,
      fileId: "file-1",
    });
    expect(saver.isDirty.value).toBe(false);
    expect(saver.canCheckpoint.value).toBe(true);
    expect(saver.status.value).toBe("saved");

    draft.value = "# Start\n\nMore and more";
    await nextTick();
    await vi.advanceTimersByTimeAsync(IDLE_SAVE_MS);
    expect(mockSaveDraft).toHaveBeenLastCalledWith(
      expect.objectContaining({ baseVersionId: "v1", expectedRevision: "r1" }),
    );
    expect(mockSaveContent).not.toHaveBeenCalled();
    app.unmount();
  });

  it("doesn't write before the pause or when nothing changed", async () => {
    const { app, draft, saver } = mountSaver();

    open(saver, draft, OPENING);
    draft.value = "# Start\n\nMore";
    await nextTick();
    await vi.advanceTimersByTimeAsync(IDLE_SAVE_MS - 100);
    expect(mockSaveDraft).not.toHaveBeenCalled();

    // Undoing back to the stored text leaves nothing to save.
    draft.value = "# Start";
    await nextTick();
    await vi.advanceTimersByTimeAsync(IDLE_SAVE_MS);
    await expect(saver.saveDraft()).resolves.toBe(true);
    await expect(saver.checkpoint()).resolves.toBe(true);
    expect(mockSaveDraft).not.toHaveBeenCalled();
    expect(mockSaveContent).not.toHaveBeenCalled();
    app.unmount();
  });

  it("cuts a checkpoint that settles the draft, then builds on the new version", async () => {
    mockSaveDraft.mockResolvedValue(DRAFT_SAVED);
    mockSaveContent.mockResolvedValueOnce(VERSION_SAVED);
    const { app, draft, saver } = mountSaver();

    open(saver, draft, OPENING);
    draft.value = "# Start\n\nMore";
    await saver.saveDraft();
    await expect(saver.checkpoint()).resolves.toBe(true);

    expect(mockSaveContent).toHaveBeenLastCalledWith({
      content: "# Start\n\nMore",
      draftRevision: "r1",
      fileId: "file-1",
      parentVersionId: "v1",
    });
    expect(saver.baseVersionId.value).toBe("v2");
    expect(saver.draftRevision.value).toBeNull();
    expect(saver.canCheckpoint.value).toBe(false);

    draft.value = "# Start\n\nMore, after the version";
    await saver.saveDraft();
    expect(mockSaveDraft).toHaveBeenLastCalledWith(
      expect.objectContaining({ baseVersionId: "v2", expectedRevision: null }),
    );
    app.unmount();
  });

  it("opens with a stored draft and carries on from its revision", async () => {
    mockSaveDraft.mockResolvedValueOnce({ ...DRAFT_SAVED, revision: "r4" });
    const { app, draft, saver } = mountSaver();

    open(saver, draft, { ...OPENING, draft: { content: "# Start\n\nYesterday", revision: "r3" } });
    expect(saver.isDirty.value).toBe(false);
    expect(saver.canCheckpoint.value).toBe(true);

    draft.value = "# Start\n\nYesterday and today";
    await saver.saveDraft();
    expect(mockSaveDraft).toHaveBeenLastCalledWith(
      expect.objectContaining({ baseVersionId: "v1", expectedRevision: "r3" }),
    );
    app.unmount();
  });

  it("makes the autosave a version once a long session passes the checkpoint interval", async () => {
    mockSaveDraft.mockResolvedValue(DRAFT_SAVED);
    mockSaveContent.mockResolvedValueOnce(VERSION_SAVED);
    const { app, draft, saver } = mountSaver();

    open(saver, draft, OPENING);
    draft.value = "# Start\n\nEarly";
    await saver.saveDraft();
    expect(mockSaveContent).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(CHECKPOINT_INTERVAL_MS);
    draft.value = "# Start\n\nMuch later";
    await saver.saveDraft();
    expect(mockSaveContent).toHaveBeenCalledTimes(1);
    expect(mockSaveContent).toHaveBeenLastCalledWith(
      expect.objectContaining({ content: "# Start\n\nMuch later", draftRevision: "r1" }),
    );
    app.unmount();
  });

  it("sends edits made during a write in a follow-up write, never two at once", async () => {
    const first = deferred<NoteDraftSaveResult>();
    mockSaveDraft
      .mockReturnValueOnce(first.promise)
      .mockResolvedValueOnce({ ...DRAFT_SAVED, revision: "r2" });
    const { app, draft, saver } = mountSaver();

    open(saver, draft, { ...OPENING, versionText: "a" });
    draft.value = "ab";
    const firstWrite = saver.saveDraft();
    draft.value = "abc";
    const secondWrite = saver.saveDraft();
    // Flushes pending promise work, so only a request held back on purpose stays unsent.
    await vi.advanceTimersByTimeAsync(0);
    expect(mockSaveDraft).toHaveBeenCalledTimes(1);

    first.resolve(DRAFT_SAVED);
    await expect(firstWrite).resolves.toBe(true);
    await expect(secondWrite).resolves.toBe(true);
    expect(mockSaveDraft).toHaveBeenCalledTimes(2);
    expect(mockSaveDraft).toHaveBeenLastCalledWith(
      expect.objectContaining({ content: "abc", expectedRevision: "r1" }),
    );
    app.unmount();
  });
});

describe("useNoteSave conflicts and failures", () => {
  it("stops on a conflict until the edit is kept over the latest version and draft", async () => {
    mockSaveDraft.mockRejectedValueOnce(CONFLICT);
    mockSaveContent.mockResolvedValueOnce({ ...VERSION_SAVED, versionId: "v6" });
    const { app, draft, saver } = mountSaver(() =>
      Promise.resolve({ text: "theirs", versionId: "v5" }),
    );

    open(saver, draft, { ...OPENING, versionText: "a" });
    draft.value = "mine";
    await expect(saver.saveDraft()).resolves.toBe(false);
    expect(saver.status.value).toBe("conflict");

    draft.value = "mine, still typing";
    await nextTick();
    await vi.advanceTimersByTimeAsync(IDLE_SAVE_MS);
    expect(mockSaveDraft).toHaveBeenCalledTimes(1);

    await expect(saver.saveOver("v5", "their-draft")).resolves.toBe(true);
    expect(mockSaveContent).toHaveBeenLastCalledWith(
      expect.objectContaining({
        content: "mine, still typing",
        draftRevision: "their-draft",
        parentVersionId: "v5",
      }),
    );
    expect(saver.status.value).toBe("saved");
    app.unmount();
  });

  it("carries on silently when the new version holds exactly the text this editor stored", async () => {
    mockSaveDraft
      .mockResolvedValueOnce(DRAFT_SAVED)
      .mockRejectedValueOnce(CONFLICT)
      .mockResolvedValueOnce({ ...DRAFT_SAVED, revision: "r7" });
    const { app, draft, saver } = mountSaver(() =>
      Promise.resolve({ text: "# Start\n\nIdle", versionId: "v2" }),
    );

    open(saver, draft, OPENING);
    draft.value = "# Start\n\nIdle";
    await saver.saveDraft();

    // The idle sweep made v2 from that draft while the tab sat open.
    draft.value = "# Start\n\nIdle, then back";
    await expect(saver.saveDraft()).resolves.toBe(true);

    expect(saver.status.value).toBe("saved");
    expect(saver.baseVersionId.value).toBe("v2");
    expect(mockSaveDraft).toHaveBeenLastCalledWith(
      expect.objectContaining({ baseVersionId: "v2", expectedRevision: null }),
    );
    app.unmount();
  });

  it("opens an outdated draft as a conflict", () => {
    const { app, draft, saver } = mountSaver();

    open(saver, draft, {
      ...OPENING,
      draft: { content: "old work", revision: "r1" },
      draftIsOutdated: true,
    });

    expect(saver.status.value).toBe("conflict");
    expect(saver.isDirty.value).toBe(false);
    expect(saver.draftRevision.value).toBe("r1");
    app.unmount();
  });

  it("reports other failures as errors that the next write retries", async () => {
    mockSaveDraft
      .mockRejectedValueOnce(new ApiError("Down", { kind: "network" }))
      .mockResolvedValueOnce(DRAFT_SAVED);
    const { app, draft, saver } = mountSaver();

    open(saver, draft, { ...OPENING, versionText: "a" });
    draft.value = "b";
    await expect(saver.saveDraft()).resolves.toBe(false);
    expect(saver.status.value).toBe("error");
    expect(saver.isDirty.value).toBe(true);

    await expect(saver.saveDraft()).resolves.toBe(true);
    expect(mockSaveDraft).toHaveBeenLastCalledWith(
      expect.objectContaining({ baseVersionId: "v1", expectedRevision: null }),
    );
    app.unmount();
  });

  it("ignores a write that finishes after another note was opened", async () => {
    const late = deferred<NoteDraftSaveResult>();
    mockSaveDraft.mockReturnValueOnce(late.promise);
    const { app, draft, saver } = mountSaver();

    open(saver, draft, { ...OPENING, versionText: "a" });
    draft.value = "b";
    const pending = saver.saveDraft();

    saver.reset();
    open(saver, draft, { ...OPENING, versionId: "other-v1", versionText: "" });

    late.resolve(DRAFT_SAVED);
    await pending;
    expect(saver.baseVersionId.value).toBe("other-v1");
    expect(saver.draftRevision.value).toBeNull();
    expect(saver.status.value).toBe("idle");
    app.unmount();
  });
});
