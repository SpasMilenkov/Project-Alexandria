<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useDebounceFn } from "@vueuse/core";
import { computed, watch } from "vue";

import { useSettingsSync } from "@/composables/useSettingsSync";
import { TOAST_LEVELS, type ToastLevel, useSettingsStore } from "@/stores/settings";

const settingsStore = useSettingsStore();
const { saveBehavior } = useSettingsSync();

const skipDeleteConfirmation = computed({
  get: () => settingsStore.skipDeleteConfirmation,
  set: (value: boolean) => settingsStore.setSkipDeleteConfirmation(value),
});

const toastLevel = computed({
  get: () => settingsStore.toastLevel,
  set: (value: ToastLevel) => settingsStore.setToastLevel(value),
});

const toastChip = computed(() => {
  if (toastLevel.value === "errors-only") {
    return "Errors only";
  }
  if (toastLevel.value === "silent") {
    return "Silent";
  }
  return "All notifications";
});

const isOpen = computed({
  get: () => settingsStore.isBehaviorSectionOpen,
  set: (value: boolean) => settingsStore.setBehaviorSectionOpen(value),
});

const persistBehavior = useDebounceFn(async () => {
  await saveBehavior({
    skipDeleteConfirmation: settingsStore.skipDeleteConfirmation,
    toastLevel: settingsStore.toastLevel,
    allowAutoTagRegression: settingsStore.allowAutoTagRegression,
    allowAutomaticMetadataOverwrite: settingsStore.allowAutomaticMetadataOverwrite,
    autoPlaylistMinTracks: settingsStore.autoPlaylistMinTracks,
  });
}, 600);

watch(() => [settingsStore.skipDeleteConfirmation, settingsStore.toastLevel], persistBehavior);

const handleResetBehavior = () => {
  settingsStore.resetBehaviorSettings();
  // Reset fires the watcher which debounces the save — no manual call needed
};
</script>

<template>
  <UCard class="overflow-hidden frosted-glass glass-surface" :ui="{ body: 'p-2 sm:p-2' }">
    <UCollapsible v-model:open="isOpen">
      <UButton
        variant="ghost"
        color="neutral"
        block
        class="justify-between"
        :trailing-icon="isOpen ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'"
      >
        <div class="flex items-center gap-2 min-w-0">
          <Icon icon="mdi:cog-outline" class="w-5 h-5 text-muted shrink-0" />
          <h2 class="text-lg font-semibold shrink-0">Behavior</h2>
          <span class="hidden md:block text-xs text-gray-500 dark:text-gray-400 truncate"
            >Confirmation prompts and notifications</span
          >
          <span v-if="!isOpen" class="flex flex-wrap items-center gap-1.5 ml-1">
            <span
              class="inline-flex items-center h-6 px-2.5 rounded-full bg-gray-100 dark:bg-neutral-800 border border-gray-200 dark:border-gray-700 text-xs text-gray-600 dark:text-gray-400"
            >
              {{ skipDeleteConfirmation ? "Delete prompts off" : "Confirms deletes" }}
            </span>
            <span
              class="inline-flex items-center h-6 px-2.5 rounded-full bg-gray-100 dark:bg-neutral-800 border border-gray-200 dark:border-gray-700 text-xs text-gray-600 dark:text-gray-400"
            >
              {{ toastChip }}
            </span>
          </span>
        </div>
      </UButton>

      <template #content>
        <div class="pt-4 space-y-6">
          <!-- Reset -->
          <div class="flex items-center justify-end">
            <UButton
              label="Reset"
              color="error"
              variant="outline"
              size="xs"
              :disabled="!settingsStore.isBehaviorModified"
              @click="handleResetBehavior"
            />
          </div>

          <div class="space-y-6 px-2">
            <!-- Skip delete confirmation -->
            <div class="flex items-start justify-between gap-4">
              <div class="flex flex-col gap-1">
                <span class="text-sm font-medium">Skip delete confirmation</span>
                <span class="text-xs text-gray-500 dark:text-gray-400 leading-relaxed">
                  When enabled, non-empty directories will be deleted without prompting
                </span>
              </div>
              <USwitch v-model="skipDeleteConfirmation" size="lg" />
            </div>
            <div
              v-if="skipDeleteConfirmation"
              class="mt-3 flex items-start gap-2 rounded-xl border border-red-500/30 bg-red-500/10 p-3"
            >
              <Icon icon="mdi:alert-circle-outline" class="w-4 h-4 shrink-0 mt-0.5 text-red-500" />
              <span class="text-xs text-gray-700 dark:text-gray-300 leading-relaxed">
                Deleting a folder with files in it will no longer ask first. Disabling confirmations
                can lead to accidental data loss.
              </span>
            </div>

            <USeparator />

            <!-- Toast notification level -->
            <div class="flex flex-col gap-3">
              <div class="flex flex-col gap-1">
                <span class="text-sm font-medium">Notification chatter</span>
                <span class="text-xs text-gray-500 dark:text-gray-400 leading-relaxed">
                  How much do you want to hear from us?
                </span>
              </div>

              <div class="grid gap-2.5 sm:grid-cols-3">
                <button
                  v-for="level in TOAST_LEVELS"
                  :key="level.value"
                  type="button"
                  :aria-pressed="toastLevel === level.value"
                  class="relative flex flex-col items-start gap-2.5 p-3.5 rounded-xl border text-left transition-all cursor-pointer"
                  :class="
                    toastLevel === level.value
                      ? 'border-primary ring-1 ring-primary bg-primary/10'
                      : 'border-gray-200 dark:border-gray-700 hover:border-gray-400 dark:hover:border-gray-500'
                  "
                  @click="toastLevel = level.value"
                >
                  <span
                    class="w-8 h-8 grid place-items-center rounded-lg bg-gray-100 dark:bg-neutral-800"
                  >
                    <Icon
                      :icon="level.icon"
                      class="w-4.5 h-4.5 shrink-0"
                      :class="
                        toastLevel === level.value
                          ? 'text-primary'
                          : 'text-gray-500 dark:text-gray-400'
                      "
                    />
                  </span>
                  <span class="flex flex-col gap-0.5 min-w-0">
                    <span class="text-sm font-medium leading-none">{{ level.label }}</span>
                    <span class="text-xs text-gray-500 dark:text-gray-400">{{
                      level.description
                    }}</span>
                  </span>
                  <!-- mdi:check-circle stays filled — it marks an active/selected state, which is correct -->
                  <Icon
                    v-if="toastLevel === level.value"
                    icon="mdi:check-circle"
                    class="absolute top-3 right-3 w-4 h-4 text-primary shrink-0"
                  />
                </button>
              </div>
            </div>
          </div>

          <p class="text-xs text-gray-500 dark:text-gray-400">
            Critical operations such as login, uploads and file restoration are always shown.
          </p>
        </div>
      </template>
    </UCollapsible>
  </UCard>
</template>
