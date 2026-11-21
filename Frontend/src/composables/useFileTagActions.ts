import { type MaybeRefOrGetter, toValue } from "vue";

import { useAppToast } from "@/composables/useAppToast";
import { quoted } from "@/features/toast/format";
import { addTagToFile, removeTagFromFile } from "@/mutations/tags";

interface FileTagActionOptions {
  fileId: MaybeRefOrGetter<string>;
  // How toasts name the file, e.g. its quoted name.
  fileLabel?: MaybeRefOrGetter<string>;
}

/**
 * Adding and removing one file's tags, with the toasts every tag editor shows. Both mutations
 * refresh the file's tag list themselves, so callers only render it.
 */
export const useFileTagActions = ({ fileId, fileLabel = "this file" }: FileTagActionOptions) => {
  const tagsToast = useAppToast("tags");
  const { mutateAsync: addTags } = addTagToFile();
  const { mutateAsync: removeTagFrom } = removeTagFromFile();

  /** True when the tag was added; a failure has already been reported. */
  const addTag = async (tagId: string): Promise<boolean> => {
    try {
      await addTags({ data: { tagIds: [tagId] }, fileId: toValue(fileId) });
      return true;
    } catch (error) {
      tagsToast.error(`Couldn't add the tag to ${toValue(fileLabel)}`, error);
      return false;
    }
  };

  // Undo targets the file the tag was removed from, even if the editor has moved on since.
  const restoreTag = async (targetFileId: string, tagId: string, tagLabel: string) => {
    try {
      await addTags({ data: { tagIds: [tagId] }, fileId: targetFileId });
    } catch (error) {
      tagsToast.error(`Couldn't restore ${tagLabel}`, error);
    }
  };

  /** True when the tag was removed; success offers an undo, a failure has been reported. */
  const removeTag = async (tagId: string, tagName?: string): Promise<boolean> => {
    const targetFileId = toValue(fileId);
    const tagLabel = tagName ? quoted(tagName) : "the tag";

    try {
      await removeTagFrom({ fileId: targetFileId, tagId });
    } catch (error) {
      tagsToast.error(`Couldn't remove ${tagLabel}`, error);
      return false;
    }

    tagsToast.success(`Removed ${tagLabel}`, {
      actions: [
        {
          icon: "i-lucide-undo-2",
          label: "Undo",
          onClick: () => {
            void restoreTag(targetFileId, tagId, tagLabel);
          },
        },
      ],
    });

    return true;
  };

  return { addTag, removeTag };
};
