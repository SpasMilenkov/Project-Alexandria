<template>
  <div class="flex flex-1 items-center justify-center">
    <div class="w-full max-w-4xl">
      <div
        class="overflow-hidden rounded-2xl border border-gray-200/60 bg-white/50 shadow-lg shadow-black/5 backdrop-blur-sm dark:border-gray-700/50 dark:bg-white/4"
      >
        <div class="flex flex-col lg:flex-row lg:min-h-120">
          <!-- LEFT: placeholder pane -->
          <div
            class="flex flex-col justify-between p-8 lg:w-[42%] lg:border-r lg:border-gray-200/60 lg:dark:border-gray-700/50"
          >
            <!-- Header -->
            <div>
              <div class="mb-7 flex items-start gap-4">
                <div
                  class="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-primary/10"
                >
                  <Icon icon="mdi:account-circle-outline" class="h-5 w-5 text-primary" />
                </div>
                <div>
                  <h1 class="text-xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">
                    Your profile
                  </h1>
                  <p class="mt-1 text-sm leading-relaxed text-gray-500 dark:text-gray-400">
                    Personalize your account so others can identify you easily.
                  </p>
                </div>
              </div>

              <!-- Avatar placeholder -->
              <div class="mb-6 flex flex-col items-center text-center py-4">
                <div
                  class="mb-4 flex h-20 w-20 items-center justify-center rounded-full border-2 border-dashed border-gray-300/70 bg-white/60 dark:border-gray-600/60 dark:bg-white/5"
                >
                  <Icon
                    icon="mdi:account-outline"
                    class="h-10 w-10 text-gray-300 dark:text-gray-600"
                  />
                </div>
                <p class="text-sm font-medium text-gray-600 dark:text-gray-400">
                  Profile setup coming soon
                </p>
                <p class="mt-1.5 max-w-xs text-xs leading-relaxed text-gray-400 dark:text-gray-500">
                  Avatar, display name, and preferences will be available in a future update.
                </p>
              </div>

              <!-- Upcoming features -->
              <div class="grid grid-colrs-1 sm:grid-cols-2 gap-2">
                <div
                  v-for="feature in UPCOMING_FEATURES"
                  :key="feature.label"
                  class="flex items-start gap-2.5 rounded-lg border border-gray-200/60 bg-gray-50/60 px-3 py-3 dark:border-gray-700/40 dark:bg-white/2"
                >
                  <Icon
                    :icon="feature.icon"
                    class="mt-0.5 h-4 w-4 shrink-0 text-gray-400 dark:text-gray-500"
                  />
                  <p class="text-sm font-medium text-gray-600 dark:text-gray-400">
                    {{ feature.label }}
                  </p>
                </div>
              </div>
            </div>

            <!-- Server error + CTA -->
            <div class="mt-8 flex flex-col gap-4">
              <Transition
                enter-active-class="transition-all duration-200 ease-out"
                enter-from-class="opacity-0 -translate-y-1"
                leave-active-class="transition-all duration-150 ease-in"
                leave-to-class="opacity-0"
              >
                <div
                  v-if="serverError"
                  class="flex items-start gap-3 rounded-xl border border-red-200/70 bg-red-50/70 px-4 py-3.5 dark:border-red-900/50 dark:bg-red-950/30"
                >
                  <Icon
                    icon="mdi:alert-circle-outline"
                    class="mt-0.5 h-4 w-4 shrink-0 text-red-500"
                  />
                  <p class="text-sm text-red-600 dark:text-red-400">{{ serverError }}</p>
                </div>
              </Transition>

              <UButton
                color="primary"
                variant="solid"
                size="lg"
                block
                trailing-icon="i-mdi-arrow-right"
                @click="handleContinue"
              >
                Continue
              </UButton>
            </div>
          </div>

          <!-- RIGHT: context pane -->
          <div
            class="flex flex-col justify-center gap-8 bg-gray-50/40 p-8 dark:bg-white/2 lg:flex-1"
          >
            <div>
              <p
                class="mb-4 text-xs font-semibold uppercase tracking-widest text-gray-400 dark:text-gray-500"
              >
                Where your profile appears
              </p>
              <div class="space-y-3">
                <div
                  v-for="context in PROFILE_CONTEXTS"
                  :key="context.label"
                  class="flex items-start gap-4 rounded-xl border border-gray-200/60 bg-white/50 px-4 py-4 dark:border-gray-700/40 dark:bg-white/[0.03]"
                >
                  <div
                    class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-gray-100/80 dark:bg-white/5"
                  >
                    <Icon :icon="context.icon" class="h-4 w-4 text-gray-500 dark:text-gray-400" />
                  </div>
                  <div>
                    <p class="text-sm font-medium text-gray-700 dark:text-gray-300">
                      {{ context.label }}
                    </p>
                    <p class="mt-0.5 text-xs leading-relaxed text-gray-400 dark:text-gray-500">
                      {{ context.desc }}
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, watch } from "vue";
import { useRouter } from "vue-router";
import { Icon } from "@iconify/vue";
import { setupProfile } from "@/mutations/user";
import { useOnboardingGuard } from "@/composables/useOnboardingGuard";
import { OnboardingStep } from "@/enums";

useOnboardingGuard(OnboardingStep.CompleteProfile);

const router = useRouter();
const { mutate, error: mutationError, status } = setupProfile();

watch(status, (s) => {
  if (s === "success") router.push({ name: "tour" });
});

const serverError = computed(() => {
  if (!mutationError.value) return null;
  return mutationError.value instanceof Error
    ? mutationError.value.message
    : "Something went wrong. Please try again.";
});

const UPCOMING_FEATURES = [
  { icon: "mdi:image-outline", label: "Profile avatar" },
  { icon: "mdi:tune-variant", label: "Preferences" },
  { icon: "mdi:bell-outline", label: "Notifications" },
  { icon: "mdi:shield-account-outline", label: "Privacy" },
] as const;

const PROFILE_CONTEXTS = [
  {
    desc: "Files you upload are attributed to your profile so teammates know who added them.",
    icon: "mdi:folder-open-outline",
    label: "File Explorer",
  },
  {
    desc: "Every action in the log — uploads, edits, deletions — is tied to your display name.",
    icon: "mdi:history",
    label: "Access History",
  },
  {
    desc: "When files are shared with you, others see your name and avatar in the permission list.",
    icon: "mdi:account-group-outline",
    label: "Shared access",
  },
  {
    desc: "Tags you create show your name as the author so teams can coordinate ownership.",
    icon: "mdi:tag-outline",
    label: "Tags & Categories",
  },
] as const;

const handleContinue = () => {
  mutate();
};
</script>
