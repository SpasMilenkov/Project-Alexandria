<script setup lang="ts">
import { useDebounceFn } from "@vueuse/core";
import { computed, watch } from "vue";

import { useSettingsSync } from "@/composables/useSettingsSync";
import { useSettingsStore } from "@/stores/settings";

import SettingsChip from "./SettingsChip.vue";
import SettingsSection from "./SettingsSection.vue";

const settingsStore = useSettingsStore();
const { saveBehavior } = useSettingsSync();

const allowAutoTagRegression = computed({
  get: () => settingsStore.allowAutoTagRegression,
  set: (value: boolean) => settingsStore.setAllowAutoTagRegression(value),
});

const allowAutomaticMetadataOverwrite = computed({
  get: () => settingsStore.allowAutomaticMetadataOverwrite,
  set: (value: boolean) => settingsStore.setAllowAutomaticMetadataOverwrite(value),
});

const isOpen = computed({
  get: () => settingsStore.isAutoTaggingSectionOpen,
  set: (value: boolean) => settingsStore.setAutoTaggingSectionOpen(value),
});

const persistAutoTagging = useDebounceFn(async () => {
  await saveBehavior({
    skipDeleteConfirmation: settingsStore.skipDeleteConfirmation,
    toastLevel: settingsStore.toastLevel,
    allowAutoTagRegression: settingsStore.allowAutoTagRegression,
    allowAutomaticMetadataOverwrite: settingsStore.allowAutomaticMetadataOverwrite,
    autoPlaylistMinTracks: settingsStore.autoPlaylistMinTracks,
  });
}, 600);

watch(() => settingsStore.allowAutoTagRegression, persistAutoTagging);
watch(() => settingsStore.allowAutomaticMetadataOverwrite, persistAutoTagging);

const handleResetAutoTagging = () => {
  settingsStore.resetAutoTaggingSettings();
};
</script>

<template>
  <SettingsSection
    v-model:open="isOpen"
    icon="mdi:tag-multiple"
    title="Auto-tagging"
    description="What happens when tagging runs again"
    :modified="settingsStore.isAutoTaggingModified"
    @reset="handleResetAutoTagging"
  >
    <template #summary>
      <SettingsChip>
        {{ allowAutoTagRegression ? "Latest verdict wins" : "Keeps highest confidence" }}
      </SettingsChip>
      <SettingsChip>
        {{ allowAutomaticMetadataOverwrite ? "Overwrites metadata" : "Protects edits" }}
      </SettingsChip>
    </template>

    <div class="space-y-6">
      <!-- Allow confidence regression -->
      <div class="flex items-start justify-between gap-4">
        <div class="flex flex-col gap-1">
          <span class="text-sm font-medium text-gray-900 dark:text-gray-100"
            >Allow confidence regression</span
          >
          <span class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
            When off (default), re-running auto-tagging never lowers the confidence of a tag already
            on a file — the higher verdict is kept. When on, the latest verdict is always stored.
          </span>
          <span class="text-xs text-gray-600 dark:text-gray-400">
            {{
              allowAutoTagRegression
                ? "Right now: the latest verdict is always stored."
                : "Right now: the higher verdict is kept."
            }}
          </span>
        </div>
        <USwitch v-model="allowAutoTagRegression" size="lg" />
      </div>

      <!-- Allow automatic metadata overwrite -->
      <div class="flex items-start justify-between gap-4">
        <div class="flex flex-col gap-1">
          <span class="text-sm font-medium text-gray-900 dark:text-gray-100"
            >Allow automatic metadata overwrite</span
          >
          <span class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
            When off (default), automatic processing never replaces title, artist, album, year or
            genre values already on a file, so your corrections survive re-runs. When on, the latest
            automatic output always wins.
          </span>
          <span class="text-xs text-gray-600 dark:text-gray-400">
            {{
              allowAutomaticMetadataOverwrite
                ? "Right now: the latest automatic output always wins."
                : "Right now: your corrections survive re-runs."
            }}
          </span>
        </div>
        <USwitch v-model="allowAutomaticMetadataOverwrite" size="lg" />
      </div>

      <p class="text-xs text-gray-600 dark:text-gray-400">
        Auto-tagged genres and moods are derived from audio analysis. Your manually added or removed
        tags are never affected.
      </p>
    </div>
  </SettingsSection>
</template>
