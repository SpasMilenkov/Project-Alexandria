import { ref } from "vue";

import { useAppToast } from "@/composables/useAppToast";
import {
  AVATAR_MAX_GIF_BYTES,
  AVATAR_MAX_INPUT_BYTES,
  isGifFile,
} from "@/features/account/avatar-crop";

export const AVATAR_ACCEPTED_TYPES = ["image/jpeg", "image/png", "image/webp", "image/gif"];

// Holds the picture chosen in a hidden file input until the crop modal is done with it.
export const useAvatarPicker = () => {
  const appToast = useAppToast("account");
  const pickedFile = ref<File | null>(null);

  const handleFileChange = (event: Event) => {
    const input = event.target as HTMLInputElement;
    const [file] = input.files ?? [];

    // Clearing lets the same file be picked again after a cancel.
    input.value = "";
    if (!file) return;

    if (!AVATAR_ACCEPTED_TYPES.includes(file.type)) {
      appToast.warning(
        "That file isn't a supported picture",
        "Choose a JPEG, PNG, WebP or GIF image.",
      );
      return;
    }

    // GIFs upload as they are, so they meet the stored-size limit up front.
    if (isGifFile(file) && file.size > AVATAR_MAX_GIF_BYTES) {
      appToast.warning("That GIF is too large", "Choose an animated picture under 4 MB.");
      return;
    }

    if (file.size > AVATAR_MAX_INPUT_BYTES) {
      appToast.warning("That picture is too large", "Choose an image under 25 MB.");
      return;
    }

    pickedFile.value = file;
  };

  const clearPicked = () => {
    pickedFile.value = null;
  };

  return {
    accept: AVATAR_ACCEPTED_TYPES.join(","),
    clearPicked,
    handleFileChange,
    pickedFile,
  };
};
