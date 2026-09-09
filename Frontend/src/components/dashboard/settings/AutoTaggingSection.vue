<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useDebounceFn } from "@vueuse/core";
import { computed, watch } from "vue";

import { useSettingsSync } from "@/composables/useSettingsSync";
import { useSettingsStore } from "@/stores/settings";

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
  });
}, 600);

watch(() => settingsStore.allowAutoTagRegression, persistAutoTagging);
watch(() => settingsStore.allowAutomaticMetadataOverwrite, persistAutoTagging);

const handleResetAutoTagging = () => {
  settingsStore.resetAutoTaggingSettings();
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
        <div class="flex items-center gap-2">
          <Icon icon="mdi:tag-multiple" class="w-5 h-5 text-muted" />
          <h2 class="text-lg font-semibold">Auto-tagging</h2>
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
              @click="handleResetAutoTagging"
            />
          </div>

          <!-- Allow confidence regression -->
          <div class="flex items-start justify-between gap-4">
            <div class="flex flex-col gap-1">
              <span class="text-sm font-medium">Allow confidence regression</span>
              <span class="text-xs text-gray-500 dark:text-gray-400 leading-relaxed">
                When off (default), re-running auto-tagging never lowers the confidence of a tag
                already on a file — the higher verdict is kept. When on, the latest verdict is
                always stored.
              </span>
            </div>
            <USwitch v-model="allowAutoTagRegression" size="lg" />
          </div>

          <!-- Allow automatic metadata overwrite -->
          <div class="flex items-start justify-between gap-4">
            <div class="flex flex-col gap-1">
              <span class="text-sm font-medium">Allow automatic metadata overwrite</span>
              <span class="text-xs text-gray-500 dark:text-gray-400 leading-relaxed">
                When off (default), automatic processing never replaces title, artist, album, year
                or genre values already on a file, so your corrections survive re-runs. When on, the
                latest automatic output always wins.
              </span>
            </div>
            <USwitch v-model="allowAutomaticMetadataOverwrite" size="lg" />
          </div>

          <p class="text-xs text-gray-500 dark:text-gray-400">
            Auto-tagged genres and moods are derived from audio analysis. Your manually added or
            removed tags are never affected.
          </p>
        </div>
      </template>
    </UCollapsible>
  </UCard>
</template>
