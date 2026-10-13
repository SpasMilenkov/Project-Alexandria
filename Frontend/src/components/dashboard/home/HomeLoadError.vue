<script setup lang="ts">
import { computed, ref, useId, watch } from "vue";

import { resetHomeSettings } from "@/mutations/home";
import { useAuthStore } from "@/stores/auth";
import { useHomeStore } from "@/stores/home";
import { glassModalContent } from "@/utils/modalUi";

defineProps<{ retrying: boolean; disabled: boolean }>();

const emit = defineEmits<{ retry: [] }>();
const titleId = useId();
const auth = useAuthStore();
const store = useHomeStore();
const mutation = resetHomeSettings();

const confirmOpen = ref(false);
const resetFailed = ref(false);
const resetting = computed(
  () => mutation.isLoading.value && mutation.variables.value?.epoch === store.ownerEpoch,
);

watch(
  () => store.ownerEpoch,
  () => {
    confirmOpen.value = false;
    resetFailed.value = false;
  },
);

const openConfirmation = () => {
  resetFailed.value = false;
  confirmOpen.value = true;
};

const resetDashboard = async () => {
  const ownerId = auth.user?.user.id;
  const epoch = store.ownerEpoch;

  if (!ownerId || resetting.value) return;

  resetFailed.value = false;

  try {
    await mutation.mutateAsync({ ownerId, epoch });

    if (store.ownerEpoch === epoch) confirmOpen.value = false;
  } catch {
    if (store.ownerEpoch === epoch) resetFailed.value = true;
  }
};
</script>

<template>
  <section
    class="frosted-glass glass-surface rounded-2xl border border-gray-200/70 p-6 sm:p-8 dark:border-gray-700/70"
    role="alert"
    :aria-labelledby="titleId"
  >
    <div class="flex flex-wrap items-start justify-between gap-4">
      <div class="flex min-w-0 items-center gap-4">
        <div
          class="flex size-12 shrink-0 items-center justify-center rounded-xl border border-gray-200/70 dark:border-gray-700/70"
        >
          <UIcon
            name="i-mdi-alert-circle-outline"
            class="size-6 text-gray-500 dark:text-gray-400"
          />
        </div>

        <h2 :id="titleId" class="text-xl font-semibold text-gray-900 dark:text-gray-100">
          Your dashboard couldn’t be loaded
        </h2>
      </div>

      <UButton
        color="error"
        variant="outline"
        icon="i-mdi-delete-outline"
        :disabled="disabled || resetting"
        @click="openConfirmation"
        >Reset dashboard</UButton
      >
    </div>

    <div class="mt-6 max-w-2xl space-y-4">
      <p class="text-sm leading-relaxed text-gray-600 dark:text-gray-400">
        We couldn’t read your saved widgets. Try loading them again. If the problem keeps happening,
        you can reset your dashboard and build a new layout.
      </p>

      <UButton
        color="primary"
        icon="i-mdi-refresh"
        :loading="retrying"
        :disabled="disabled || resetting || confirmOpen"
        @click="emit('retry')"
        >Try again</UButton
      >
    </div>
  </section>

  <UModal
    v-model:open="confirmOpen"
    title="Reset your dashboard?"
    description="Start over with an empty Home dashboard."
    :dismissible="!resetting"
    :close="!resetting"
    :ui="{ content: glassModalContent }"
  >
    <template #body>
      <div class="space-y-4 text-sm leading-relaxed text-gray-600 dark:text-gray-400">
        <p>
          This permanently removes every widget and its settings from both your desktop and mobile
          layouts, including any unsaved customization. This cannot be undone.
        </p>

        <p>Your files, media, and other account settings will be kept.</p>

        <UAlert
          v-if="resetFailed"
          color="error"
          variant="soft"
          title="Dashboard could not be reset"
          description="We couldn’t complete the reset. Check your connection and try again."
          role="alert"
        />
      </div>
    </template>

    <template #footer>
      <div class="flex w-full flex-wrap justify-end gap-2">
        <UButton
          color="neutral"
          variant="outline"
          :disabled="resetting"
          @click="confirmOpen = false"
          >Cancel</UButton
        >

        <UButton
          color="error"
          variant="outline"
          :loading="resetting"
          :disabled="disabled"
          @click="resetDashboard"
          >Reset dashboard</UButton
        >
      </div>
    </template>
  </UModal>
</template>
