import { useDebounceFn } from "@vueuse/core";
import { type MaybeRefOrGetter, type Ref, computed, ref, toValue, watch } from "vue";

import { isApiError } from "@/api/errors";
import { survivesRoundTrip } from "@/features/notes/round-trip";
import { saveNoteContent, saveNoteDraft } from "@/mutations/notes";
import { logger } from "@/utils/logger";

export type NoteSaveStatus = "idle" | "saving" | "saved" | "error" | "conflict";

// Drafts are cheap (one row, no background work), so a pause of a few sentences is enough to
// keep one, and steady typing still stores at least this often.
export const IDLE_SAVE_MS = 10_000;
export const MAX_SAVE_WAIT_MS = 30_000;

// A long session still leaves a trail of versions to go back to, one per stretch this long.
export const CHECKPOINT_INTERVAL_MS = 10 * 60_000;

export interface NoteOpening {
  // The version the editor builds on, and its text.
  versionId: string;
  versionText: string;
  // The stored draft the editor opened with, if any.
  draft: { revision: string; content: string } | null;
  // The stored draft was made from an older version, so the editor can't carry on from it until
  // someone keeps or discards it.
  draftIsOutdated: boolean;
}

export interface CurrentVersion {
  versionId: string;
  text: string;
}

interface UseNoteSaveOptions {
  fileId: MaybeRefOrGetter<string>;
  draft: Ref<string>;
  // The file's current version and its text, used to tell a version made from this editor's own
  // draft (the idle sweep) apart from a real conflict.
  readCurrentVersion: () => Promise<CurrentVersion | null>;
}

const isConflict = (error: unknown) => isApiError(error) && error.status === 409;

/**
 * Keeps the editor's text on the server. Pauses in typing write a draft; checkpoints (leaving
 * the note, Ctrl+S, every ten minutes of editing) turn it into a version. One request runs at a
 * time; edits made meanwhile go out in the next one. A conflict stops saving until the caller
 * settles it.
 */
export const useNoteSave = ({ fileId, draft, readCurrentVersion }: UseNoteSaveOptions) => {
  const baseVersionId = ref<string | null>(null);
  // Null when unknown, after keeping an edit over a version this editor never read.
  const versionText = ref<string | null>(null);
  // What the server holds for this editor: the draft's text, or the version's when there is none.
  const storedText = ref<string | null>(null);
  const draftRevision = ref<string | null>(null);
  const status = ref<NoteSaveStatus>("idle");
  const lastError = ref<unknown>(null);
  const lastSavedAt = ref<Date | null>(null);

  const { mutateAsync: writeDraft } = saveNoteDraft();
  const { mutateAsync: writeVersion } = saveNoteContent();

  let inFlight: Promise<boolean> | null = null;
  let lastCheckpointAt = 0;
  // Bumped whenever another note (or another version of it) is opened, so a request that
  // finishes afterwards can't overwrite the new session's starting point.
  let session = 0;

  // Compared after normalising, so switching modes or undoing back to the start doesn't count.
  const isDirty = computed(() => {
    if (storedText.value === null) return false;

    return !survivesRoundTrip(storedText.value, draft.value);
  });

  const hasUnversionedChanges = computed(() => {
    if (baseVersionId.value === null) return false;
    if (versionText.value === null) return true;

    return !survivesRoundTrip(versionText.value, draft.value);
  });

  const canCheckpoint = computed(
    () =>
      status.value !== "conflict" && (hasUnversionedChanges.value || draftRevision.value !== null),
  );

  const isSaving = computed(() => status.value === "saving");

  // Null while another note is opening: until it loads there is nothing to compare or save.
  const start = (opening: NoteOpening | null) => {
    session += 1;
    lastCheckpointAt = Date.now();
    baseVersionId.value = opening?.versionId ?? null;
    versionText.value = opening?.versionText ?? null;
    storedText.value = opening?.draft?.content ?? opening?.versionText ?? null;
    draftRevision.value = opening?.draft?.revision ?? null;
    lastError.value = null;
    lastSavedAt.value = null;
    status.value = opening?.draftIsOutdated ? "conflict" : "idle";
  };

  const recordStored = (content: string) => {
    storedText.value = content;
    lastSavedAt.value = new Date();
    lastError.value = null;
    status.value = "saved";
  };

  // A 409 means the note moved on elsewhere, which only the person editing can settle, unless
  // the new version holds exactly the text this editor last stored: then the idle sweep made it
  // from this editor's draft, nothing was lost, and the editor carries on from it. Anything else
  // may pass on its own, so the next edit or a retry tries again.
  const failed = async (
    error: unknown,
    startedIn: number,
    retry: () => Promise<boolean>,
  ): Promise<boolean> => {
    if (startedIn !== session) {
      logger.warn("A save for a note that is no longer open failed", error);

      return false;
    }

    if (isConflict(error)) {
      const current = await readCurrentVersion().catch((readError: unknown) => {
        logger.warn("Couldn't read the note's current version after a conflict", readError);

        return null;
      });

      const isOwnText =
        current !== null &&
        current.versionId !== baseVersionId.value &&
        storedText.value !== null &&
        survivesRoundTrip(current.text, storedText.value);

      if (startedIn === session && isOwnText) {
        baseVersionId.value = current.versionId;
        versionText.value = current.text;
        draftRevision.value = null;
        lastCheckpointAt = Date.now();

        return retry();
      }

      logger.warn("The note changed elsewhere since this edit started", error);
      lastError.value = error;
      status.value = "conflict";

      return false;
    }

    logger.error("Saving the note failed", error);
    lastError.value = error;
    status.value = "error";

    return false;
  };

  const cutCheckpoint = async (): Promise<boolean> => {
    const parentVersionId = baseVersionId.value;

    if (status.value === "conflict" || !parentVersionId) return false;
    if (!hasUnversionedChanges.value && draftRevision.value === null) return true;

    const content = draft.value;
    const startedIn = session;
    status.value = "saving";

    try {
      const saved = await writeVersion({
        content,
        draftRevision: draftRevision.value,
        fileId: toValue(fileId),
        parentVersionId,
      });

      if (startedIn !== session) return true;

      baseVersionId.value = saved.versionId;
      versionText.value = content;
      draftRevision.value = null;
      lastCheckpointAt = Date.now();
      recordStored(content);

      return true;
    } catch (error) {
      return failed(error, startedIn, cutCheckpoint);
    }
  };

  const storeDraft = async (): Promise<boolean> => {
    const base = baseVersionId.value;

    if (status.value === "conflict" || !base) return false;
    if (!isDirty.value) return true;

    if (hasUnversionedChanges.value && Date.now() - lastCheckpointAt >= CHECKPOINT_INTERVAL_MS) {
      return cutCheckpoint();
    }

    const content = draft.value;
    const startedIn = session;
    status.value = "saving";

    try {
      const saved = await writeDraft({
        baseVersionId: base,
        content,
        expectedRevision: draftRevision.value,
        fileId: toValue(fileId),
      });

      if (startedIn !== session) return true;

      draftRevision.value = saved.revision;
      recordStored(content);

      return true;
    } catch (error) {
      return failed(error, startedIn, storeDraft);
    }
  };

  // A request already running finishes first, then this one decides afresh what to send.
  const exclusive = async (task: () => Promise<boolean>): Promise<boolean> => {
    if (inFlight) {
      await inFlight;

      return exclusive(task);
    }

    inFlight = task();

    try {
      return await inFlight;
    } finally {
      inFlight = null;
    }
  };

  const saveDraft = () => exclusive(storeDraft);

  // Every edit restarts the wait; the draft write itself skips text that is already stored.
  watch(
    draft,
    useDebounceFn(() => void saveDraft(), IDLE_SAVE_MS, { maxWait: MAX_SAVE_WAIT_MS }),
  );

  return {
    baseVersionId,
    canCheckpoint,
    // Makes a version of whatever the editor holds now.
    checkpoint: () => exclusive(cutCheckpoint),
    draftRevision,
    isDirty,
    isSaving,
    lastError,
    lastSavedAt,
    reset: () => start(null),
    saveDraft,
    // Keeps this edit as the next version after `currentVersionId`, replacing the draft stored
    // at `currentDraftRevision`, whoever wrote it.
    saveOver: (currentVersionId: string, currentDraftRevision: string | null) => {
      baseVersionId.value = currentVersionId;
      versionText.value = null;
      draftRevision.value = currentDraftRevision;
      status.value = "idle";

      return exclusive(cutCheckpoint);
    },
    start,
    status,
  };
};
