<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useDebounceFn } from "@vueuse/core";
import { computed, watch } from "vue";

import { useSettingsSync } from "@/composables/useSettingsSync";
import { TOAST_LEVELS, type ToastLevel, useSettingsStore } from "@/stores/settings";

import SettingsChip from "./SettingsChip.vue";
import SettingsSection from "./SettingsSection.vue";

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
  <SettingsSection
    v-model:open="isOpen"
    icon="mdi:cog-outline"
    title="Behavior"
    description="Confirmation prompts and notifications"
    :modified="settingsStore.isBehaviorModified"
    @reset="handleResetBehavior"
  >
    <template #summary>
      <SettingsChip>
        {{ skipDeleteConfirmation ? "Delete prompts off" : "Confirms deletes" }}
      </SettingsChip>
      <SettingsChip>{{ toastChip }}</SettingsChip>
    </template>

    <div class="space-y-6">
      <div class="space-y-6">
        <!-- Skip delete confirmation -->
        <div class="flex items-start justify-between gap-4">
          <div class="flex flex-col gap-1">
            <span class="text-sm font-medium text-gray-900 dark:text-gray-100"
              >Skip delete confirmation</span
            >
            <span class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
              When enabled, non-empty directories will be deleted without prompting
            </span>
          </div>
          <USwitch v-model="skipDeleteConfirmation" size="lg" />
        </div>
        <div
          v-if="skipDeleteConfirmation"
          class="mt-3 flex items-start gap-2 rounded-xl border border-red-500/30 bg-red-500/10 p-3"
        >
          <Icon icon="mdi:alert-circle-outline" class="w-4 h-4 shrink-0 mt-0.5 text-error" />
          <span class="text-xs text-gray-700 dark:text-gray-300 leading-relaxed">
            Deleting a folder with files in it will no longer ask first. Disabling confirmations can
            lead to accidental data loss.
          </span>
        </div>

        <USeparator />

        <!-- Toast notification level -->
        <div class="flex flex-col gap-3">
          <div class="flex flex-col gap-1">
            <span class="text-sm font-medium text-gray-900 dark:text-gray-100"
              >Notification chatter</span
            >
            <span class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
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
                class="w-8 h-8 grid place-items-center rounded-lg border border-gray-200/70 dark:border-gray-700/70"
              >
                <Icon
                  :icon="level.icon"
                  class="w-4.5 h-4.5 shrink-0"
                  :class="
                    toastLevel === level.value ? 'text-primary' : 'text-gray-600 dark:text-gray-400'
                  "
                />
              </span>
              <span class="flex flex-col gap-0.5 min-w-0">
                <span class="text-sm font-medium leading-none text-gray-900 dark:text-gray-100">{{
                  level.label
                }}</span>
                <span class="text-xs text-gray-600 dark:text-gray-400">{{
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

      <p class="text-xs text-gray-600 dark:text-gray-400">
        Critical operations such as login, uploads and file restoration are always shown.
      </p>
    </div>
  </SettingsSection>
</template>
