import { useDebounceFn } from "@vueuse/core";
import { type MaybeRefOrGetter, type Ref, computed, ref, toValue, watch } from "vue";

import type { FileContentSaveResult } from "@/api/notes";

import { isApiError } from "@/api/errors";
import { survivesRoundTrip } from "@/features/notes/round-trip";
import { saveNoteContent } from "@/mutations/notes";
import { logger } from "@/utils/logger";

export type NoteSaveStatus = "idle" | "saving" | "saved" | "error" | "conflict";

// Long enough that a pause between sentences doesn't become a version, short enough that little
// is lost if the tab dies. Steady typing still saves at least this often.
export const IDLE_SAVE_MS = 4000;
export const MAX_SAVE_WAIT_MS = 60_000;

interface UseNoteSaveOptions {
  fileId: MaybeRefOrGetter<string>;
  draft: Ref<string>;
  // Called after each save that wrote a version; `count` starts at 1 for this editor session.
  onSaved?: (saved: FileContentSaveResult, count: number) => void;
}

// A 409 means the note moved on elsewhere, which only the person editing can settle. Anything
// else may pass on its own, so the next edit or a retry tries again.
const failureStatus = (error: unknown): NoteSaveStatus => {
  if (isApiError(error) && error.status === 409) {
    logger.warn("The note changed elsewhere since this edit started", error);

    return "conflict";
  }

  logger.error("Saving the note failed", error);

  return "error";
};

/**
 * Saves the editor's draft as new versions of the note, each made from the version before it.
 * One request runs at a time; edits made meanwhile go out in the next save. A conflict stops
 * autosaving until the caller resolves it.
 */
export const useNoteSave = ({ fileId, draft, onSaved }: UseNoteSaveOptions) => {
  const baseVersionId = ref<string | null>(null);
  const savedText = ref<string | null>(null);
  const status = ref<NoteSaveStatus>("idle");
  const lastError = ref<unknown>(null);
  const lastSavedAt = ref<Date | null>(null);

  const { mutateAsync } = saveNoteContent();

  let inFlight: Promise<boolean> | null = null;
  let savesMade = 0;
  // Bumped whenever another note (or another version of it) is opened, so a save that finishes
  // afterwards can't overwrite the new session's starting point.
  let session = 0;

  // Compared after normalising, so switching modes or undoing back to the start doesn't count.
  const isDirty = computed(() => {
    if (savedText.value === null) return false;

    return !survivesRoundTrip(savedText.value, draft.value);
  });

  const isSaving = computed(() => status.value === "saving");

  const begin = (versionId: string | null, text: string | null) => {
    session += 1;
    savesMade = 0;
    baseVersionId.value = versionId;
    savedText.value = text;
    status.value = "idle";
    lastError.value = null;
    lastSavedAt.value = null;
  };

  const start = (versionId: string, text: string) => begin(versionId, text);

  // Another note is opening: until it loads there is nothing to compare against or save.
  const reset = () => begin(null, null);

  const recordSaved = (saved: FileContentSaveResult, content: string) => {
    baseVersionId.value = saved.versionId;
    savedText.value = content;
    lastSavedAt.value = new Date();
    lastError.value = null;
    status.value = "saved";

    if (saved.unchanged) return;

    savesMade += 1;
    onSaved?.(saved, savesMade);
  };

  const send = async (parentVersionId: string): Promise<boolean> => {
    const content = draft.value;
    const startedIn = session;
    status.value = "saving";

    try {
      const saved = await mutateAsync({ content, fileId: toValue(fileId), parentVersionId });

      if (startedIn === session) recordSaved(saved, content);

      return true;
    } catch (error) {
      if (startedIn !== session) {
        logger.warn("A save for a note that is no longer open failed", error);

        return false;
      }

      lastError.value = error;
      status.value = failureStatus(error);

      return false;
    }
  };

  // A save already running finishes first, then whatever is still unsaved goes out.
  const save = async (): Promise<boolean> => {
    if (inFlight) {
      await inFlight;

      return save();
    }

    const parentVersionId = baseVersionId.value;

    if (status.value === "conflict" || !parentVersionId) return false;
    if (!isDirty.value) return true;

    inFlight = send(parentVersionId);

    try {
      return await inFlight;
    } finally {
      inFlight = null;
    }
  };

  const scheduleIdleSave = useDebounceFn(
    () => {
      void save();
    },
    IDLE_SAVE_MS,
    { maxWait: MAX_SAVE_WAIT_MS },
  );

  watch(draft, () => {
    if (isDirty.value && status.value !== "conflict") void scheduleIdleSave();
  });

  // Keeps this edit and saves it over whatever version is current now.
  const saveOver = (currentVersionId: string): Promise<boolean> => {
    baseVersionId.value = currentVersionId;
    status.value = "idle";

    return save();
  };

  return {
    baseVersionId,
    isDirty,
    isSaving,
    lastError,
    lastSavedAt,
    reset,
    save,
    saveOver,
    start,
    status,
  };
};
