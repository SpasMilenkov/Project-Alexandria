import { ref } from "vue";

import { useAppToast } from "@/composables/useAppToast";
import { usernameFieldError } from "@/features/account/profile-errors";
import { useUpdateProfile } from "@/mutations/user";
import { updateProfileSchema } from "@/schemas/user";

export type UsernameSaveResult = "invalid" | "saved" | "unchanged";

// Draft, validation and save for the signed-in user's username, shared by My account and onboarding.
export const useUsernameEditor = (currentName: () => string) => {
  const appToast = useAppToast("account");
  const { mutateAsync: updateProfile, isLoading: isSaving } = useUpdateProfile();

  const draft = ref(currentName());
  const error = ref<string | null>(null);

  const reset = () => {
    draft.value = currentName();
    error.value = null;
  };

  const clearError = () => {
    error.value = null;
  };

  const save = async (): Promise<UsernameSaveResult> => {
    const parsed = updateProfileSchema.safeParse({ userName: draft.value });
    if (!parsed.success) {
      error.value = parsed.error.issues[0]?.message ?? "Enter a valid username.";
      return "invalid";
    }

    if (parsed.data.userName === currentName()) return "unchanged";

    try {
      await updateProfile(parsed.data);
      return "saved";
    } catch (err) {
      const fieldError = usernameFieldError(err);
      if (fieldError) {
        error.value = fieldError;
        return "invalid";
      }

      appToast.error("Couldn't change your username", err);
      return "invalid";
    }
  };

  return { clearError, draft, error, isSaving, reset, save };
};
