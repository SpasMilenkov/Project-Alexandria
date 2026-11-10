<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useDebounceFn } from "@vueuse/core";
import { computed, watch } from "vue";

import { useSettingsSync } from "@/composables/useSettingsSync";
import { useSettingsStore } from "@/stores/settings";
import { formatCount } from "@/utils/count";

import SettingsChip from "./SettingsChip.vue";
import SettingsSection from "./SettingsSection.vue";

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

// Illustrative groupings only, so the threshold effect is visible at a glance.
const sampleGroupings = [
  { count: 24, name: "Jazz" },
  { count: 12, name: "Lo-fi" },
  { count: 8, name: "Synthwave" },
  { count: 5, name: "Ambient" },
  { count: 3, name: "Field recordings" },
];

const includedSamples = computed(
  () =>
    sampleGroupings.filter((group) => group.count >= settingsStore.autoPlaylistMinTracks).length,
);

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
  <SettingsSection
    v-model:open="isOpen"
    icon="mdi:playlist-music"
    title="Auto-playlists"
    description="When tag and genre playlists get created"
    :modified="settingsStore.isAutoPlaylistsModified"
    @reset="handleResetAutoPlaylists"
  >
    <template #summary>
      <SettingsChip>Minimum {{ formatCount(settingsStore.autoPlaylistMinTracks, "track") }}</SettingsChip>
    </template>

    <div class="space-y-6">
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
          class="settings-number-input w-32"
        />
      </UFormField>

      <div class="flex flex-col gap-1">
        <span class="text-sm font-medium text-gray-900 dark:text-gray-100">What this means</span>
        <span class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
          Sample groupings and whether each would become a playlist.
        </span>
      </div>
      <div class="flex flex-wrap gap-2" aria-label="Sample groupings">
        <span
          v-for="group in sampleGroupings"
          :key="group.name"
          class="inline-flex items-center gap-1.5 h-8 px-3 rounded-full border text-xs font-medium"
          :class="
            group.count >= settingsStore.autoPlaylistMinTracks
              ? 'border-primary/50 bg-primary/10 text-gray-900 dark:text-gray-100'
              : 'border-dashed border-gray-200 dark:border-gray-700 text-gray-600 dark:text-gray-400'
          "
        >
          <Icon
            :icon="group.count >= settingsStore.autoPlaylistMinTracks ? 'mdi:check' : 'mdi:minus'"
            class="w-3.5 h-3.5 shrink-0"
            :class="
              group.count >= settingsStore.autoPlaylistMinTracks
                ? 'text-green-600 dark:text-green-500'
                : ''
            "
          />
          {{ group.name }}
          <span class="font-normal text-gray-600 dark:text-gray-400">{{ formatCount(group.count, "track") }}</span>
        </span>
      </div>
      <p class="text-xs text-gray-600 dark:text-gray-400 -mt-3">
        {{ includedSamples }} of {{ sampleGroupings.length }} sample groupings would become
        playlists.
      </p>

      <p class="text-xs text-gray-600 dark:text-gray-400">
        Applies the next time a playlist would be created, by the background worker or the periodic
        sweep.
      </p>
    </div>
  </SettingsSection>
</template>

<style scoped>
@media (max-width: 767px) {
  .settings-number-input :deep(input) {
    font-size: 16px;
  }
}
</style>
