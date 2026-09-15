<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useDebounceFn } from "@vueuse/core";
import { computed, watch } from "vue";

import { useSettingsSync } from "@/composables/useSettingsSync";
import { useSettingsStore } from "@/stores/settings";

const settingsStore = useSettingsStore();
const { saveBehavior } = useSettingsSync();

const autoPlaylistMinTracks = computed({
  get: () => settingsStore.autoPlaylistMinTracks,
  set: (value: number | undefined) => {
    if (value === undefined) return;
    settingsStore.setAutoPlaylistMinTracks(Math.round(value));
  },
});

const isOpen = computed({
  get: () => settingsStore.isAutoPlaylistsSectionOpen,
  set: (value: boolean) => settingsStore.setAutoPlaylistsSectionOpen(value),
});

const persistAutoPlaylists = useDebounceFn(async () => {
  await saveBehavior({
    skipDeleteConfirmation: settingsStore.skipDeleteConfirmation,
    toastLevel: settingsStore.toastLevel,
    allowAutoTagRegression: settingsStore.allowAutoTagRegression,
    allowAutomaticMetadataOverwrite: settingsStore.allowAutomaticMetadataOverwrite,
    autoPlaylistMinTracks: settingsStore.autoPlaylistMinTracks,
  });
}, 600);

watch(() => settingsStore.autoPlaylistMinTracks, persistAutoPlaylists);

const handleResetAutoPlaylists = () => {
  settingsStore.resetAutoPlaylistSettings();
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
          <Icon icon="mdi:playlist-music" class="w-5 h-5 text-muted" />
          <h2 class="text-lg font-semibold">Auto-playlists</h2>
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
              @click="handleResetAutoPlaylists"
            />
          </div>

          <!-- Minimum tracks -->
          <UFormField
            label="Minimum tracks per tag or genre playlist"
            description="Tag and genre groupings with fewer streamable tracks than this never become playlists. Artist and album playlists always materialize. Playlists that already exist keep syncing below the threshold."
            name="auto-playlist-min-tracks"
          >
            <UInputNumber
              v-model="autoPlaylistMinTracks"
              :min="1"
              :max="100"
              :step="1"
              class="w-32"
            />
          </UFormField>

          <p class="text-xs text-gray-500 dark:text-gray-400">
            Applies the next time a playlist would be created, by the background worker or the
            periodic sweep.
          </p>
        </div>
      </template>
    </UCollapsible>
  </UCard>
</template>
