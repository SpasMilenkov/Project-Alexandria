<template>
  <div class="flex flex-1 items-center justify-center">
    <div class="w-full max-w-4xl">
      <div
        class="overflow-hidden rounded-2xl border border-gray-200/60 bg-white shadow-lg shadow-black/5 dark:border-gray-700/50 dark:bg-neutral-900"
      >
        <div class="flex flex-col lg:flex-row lg:min-h-120">
          <!-- LEFT: profile form -->
          <form
            class="flex flex-col justify-between gap-8 p-4 md:p-8 lg:w-[52%] lg:border-r lg:border-gray-200/60 lg:dark:border-gray-700/50"
            novalidate
            @submit.prevent="handleContinue"
          >
            <div class="flex flex-col gap-6">
              <div class="flex items-start gap-4">
                <div
                  class="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-primary/10"
                >
                  <Icon icon="mdi:account-circle-outline" class="h-5 w-5 text-primary" />
                </div>
                <div>
                  <h1 class="text-xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">
                    Set up your profile
                  </h1>
                  <p class="mt-1 text-sm leading-relaxed text-gray-600 dark:text-gray-400">
                    Choose how you appear in Alexandria. Both are optional and you can change them
                    later from My account.
                  </p>
                </div>
              </div>

              <OnboardingAvatarPicker :name="previewName" :avatar-url="avatarUrl" />

              <OnboardingUsernameField
                v-model="draftName"
                :error="nameError"
                :disabled="isBusy"
                @edit="clearNameError"
              />
            </div>

            <div class="flex flex-col gap-4">
              <Transition
                enter-active-class="transition-all duration-200 ease-out"
                enter-from-class="opacity-0 -translate-y-1"
                leave-active-class="transition-all duration-150 ease-in"
                leave-to-class="opacity-0"
              >
                <UAlert
                  v-if="serverError"
                  color="error"
                  variant="subtle"
                  icon="i-lucide-alert-circle"
                  :title="serverError"
                />
              </Transition>

              <UButton
                type="submit"
                color="primary"
                variant="solid"
                size="lg"
                block
                trailing-icon="i-mdi-arrow-right"
                :loading="isBusy"
              >
                Continue
              </UButton>
            </div>
          </form>

          <!-- RIGHT: live preview -->
          <div class="flex flex-col justify-center bg-neutral-50 p-4 md:p-8 lg:flex-1 dark:bg-neutral-800">
            <OnboardingProfilePreview :name="previewName" :email="email" :avatar-url="avatarUrl" />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";

import OnboardingAvatarPicker from "@/components/onboarding/OnboardingAvatarPicker.vue";
import OnboardingProfilePreview from "@/components/onboarding/OnboardingProfilePreview.vue";
import OnboardingUsernameField from "@/components/onboarding/OnboardingUsernameField.vue";
import { useOnboardingGuard } from "@/composables/useOnboardingGuard";
import { useUsernameEditor } from "@/composables/useUsernameEditor";
import { OnboardingStep } from "@/enums";
import { setupProfile } from "@/mutations/user";
import { useAuthStore } from "@/stores/auth";
import { logger } from "@/utils/logger";

useOnboardingGuard(OnboardingStep.CompleteProfile);

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();

const currentName = computed(() => authStore.user?.user.name ?? "");
const email = computed(() => authStore.user?.user.email ?? "");
const avatarUrl = computed(() => authStore.user?.user.avatarUrl ?? null);

const {
  clearError: clearNameError,
  draft: draftName,
  error: nameError,
  isSaving: isSavingName,
  save: saveName,
} = useUsernameEditor(() => currentName.value);

const { mutateAsync: finishStep, error: stepError, isLoading: isFinishing } = setupProfile();

const isBusy = computed(() => isSavingName.value || isFinishing.value);

// The preview follows the field as you type, falling back to the saved name while it is blank.
const previewName = computed(() => draftName.value.trim() || currentName.value);

const serverError = computed(() => {
  if (!stepError.value) return null;
  if (stepError.value instanceof Error) return stepError.value.message;

  return "Something went wrong. Please try again.";
});

const handleContinue = async () => {
  if (isBusy.value) return;

  // Renaming is safe to repeat, so a retry after a failed step just saves the same name again.
  const result = await saveName();
  if (result === "invalid") return;

  try {
    await finishStep();
    await router.replace({ name: "tour", query: route.query });
  } catch (err) {
    // The alert above already shows this to the user.
    logger.warn("Finishing the profile step failed:", err);
  }
};
</script>
