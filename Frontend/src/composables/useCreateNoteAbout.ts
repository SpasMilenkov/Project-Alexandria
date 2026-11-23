import { useRouter } from "vue-router";

import type { LinkTargetKind } from "@/enums/link-target-kind";

import { useAppToast } from "@/composables/useAppToast";
import { noteAboutRequest } from "@/features/notes/backlinks";
import { createNote } from "@/mutations/notes";

/** "New note about this": a note in the Notes folder that starts with a link to the item. */
export const useCreateNoteAbout = () => {
  const router = useRouter();
  const toast = useAppToast("files");

  const { mutateAsync, isLoading: isCreating } = createNote();

  const createAbout = async (target: { id: string; kind: LinkTargetKind; name: string }) => {
    if (isCreating.value) return;

    try {
      const created = await mutateAsync(noteAboutRequest(target));

      await router.push({ name: "note-editor", params: { fileId: created.fileId } });
    } catch (error) {
      toast.error("Couldn't create the note", error);
    }
  };

  return { createAbout, isCreating };
};
