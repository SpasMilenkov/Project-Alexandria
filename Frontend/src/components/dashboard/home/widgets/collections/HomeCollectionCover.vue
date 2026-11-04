<script setup lang="ts">
import { computed, ref, watch } from "vue";

import { type PlaylistResponse, playlistApi } from "@/api/playlist";
import PlaylistCover from "@/components/streaming/PlaylistCover.vue";
import { useSettingsStore } from "@/stores/settings";

const { playlist } = defineProps<{ playlist: PlaylistResponse }>();

const settings = useSettingsStore();
const failed = ref(false);
const coverUrl = computed(() => playlistApi.getPlaylistCoverUrl(playlist.id, playlist.updatedAt));

watch(coverUrl, () => {
  failed.value = false;
});
</script>

<template>
  <div
    class="flex size-full items-center justify-center overflow-hidden rounded-lg border border-gray-200/70 bg-gray-100 shadow-md dark:border-gray-700/70 dark:bg-gray-800"
  >
    <UIcon
      v-if="!settings.thumbnailsEnabled"
      name="i-heroicons-musical-note"
      class="size-8 text-gray-500 dark:text-gray-500"
    />
    <img
      v-else-if="playlist.hasCover && !failed"
      :src="coverUrl"
      :alt="playlist.name"
      loading="lazy"
      decoding="async"
      class="size-full object-cover"
      @error="failed = true"
    />
    <PlaylistCover v-else :playlist="playlist" />
  </div>
</template>
