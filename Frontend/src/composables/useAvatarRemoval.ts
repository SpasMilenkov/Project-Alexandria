import { ref } from "vue";

import { useAppToast } from "@/composables/useAppToast";
import { useRemoveAvatar } from "@/mutations/user";

// Confirm-then-remove flow for the signed-in user's picture; pairs with ConfirmModal's close event.
export const useAvatarRemoval = () => {
  const appToast = useAppToast("account");
  const { mutateAsync: removeAvatar, isLoading: isRemoving } = useRemoveAvatar();

  const confirming = ref(false);

  const handleConfirmClose = async (confirmed: boolean) => {
    if (!confirmed) {
      confirming.value = false;
      return;
    }

    try {
      await removeAvatar();
      appToast.success("Profile picture removed");
    } catch (err) {
      appToast.error("Couldn't remove your picture", err);
    } finally {
      confirming.value = false;
    }
  };

  return { confirming, handleConfirmClose, isRemoving };
};
