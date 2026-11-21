import { type MaybeRefOrGetter, toValue } from "vue";

import { useAppToast } from "@/composables/useAppToast";
import {
  AUTO_TAGGING_ENABLED,
  describeAutoTagError,
  isAutoTaggingOffMessage,
} from "@/features/tags/auto-tag";
import { autoTagFile } from "@/mutations/files";

interface AutoTagOptions {
  fileId: MaybeRefOrGetter<string | null | undefined>;
  // How toasts name the file, e.g. its quoted name or track title.
  fileLabel: MaybeRefOrGetter<string>;
}

/** Queues background tag derivation for one file and reports the outcome as toasts. */
export const useAutoTag = ({ fileId, fileLabel }: AutoTagOptions) => {
  const tagsToast = useAppToast("tags");
  const { mutateAsync, isLoading: isAutoTagging } = autoTagFile();

  const autoTag = async () => {
    const id = toValue(fileId);

    if (isAutoTagging.value || !id) return;

    const label = toValue(fileLabel);

    try {
      const result = await mutateAsync(id);

      if (result.queued) {
        tagsToast.success(
          `Auto-tagging ${label}`,
          "Tags appear here once the background analysis finishes.",
        );
        return;
      }

      tagsToast.info(`${label} is already auto-tagged`, result.message);
    } catch (error) {
      const message = describeAutoTagError(error);

      if (isAutoTaggingOffMessage(message)) {
        tagsToast.info("Auto-tagging is turned off", message);
        return;
      }

      tagsToast.error(`Couldn't auto-tag ${label}`, message, { retry: autoTag });
    }
  };

  return { autoTag, isAutoTagging, isAutoTaggingEnabled: AUTO_TAGGING_ENABLED };
};
