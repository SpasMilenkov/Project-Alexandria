import { useQuery } from "@pinia/colada";
import { type MaybeRefOrGetter, computed, toValue } from "vue";
import { useRouter } from "vue-router";

import { useAppToast } from "@/composables/useAppToast";
import { newNoteFolderFor } from "@/features/notes/new-note";
import { createNote } from "@/mutations/notes";
import { getNotesFolder } from "@/queries/notes";

interface UseCreateNoteOptions {
  // The folder path being browsed, root first. Inside Notes the note goes there.
  path?: MaybeRefOrGetter<readonly { id: string }[] | undefined>;
}

/** "New note": creates an empty note and opens it in the editor. */
export const useCreateNote = ({ path }: UseCreateNoteOptions = {}) => {
  const router = useRouter();
  const toast = useAppToast("files");

  const { data: notesFolder } = useQuery(getNotesFolder);
  const { mutateAsync, isLoading: isCreating } = createNote();

  const targetFolderId = computed(() => newNoteFolderFor(toValue(path), notesFolder.value?.id));

  const createAndOpen = async () => {
    if (isCreating.value) return;

    try {
      const created = await mutateAsync({ directoryId: targetFolderId.value ?? null });

      await router.push({ name: "note-editor", params: { fileId: created.fileId } });
    } catch (error) {
      toast.error("Couldn't create the note", error);
    }
  };

  return { createAndOpen, isCreating, notesFolder };
};
