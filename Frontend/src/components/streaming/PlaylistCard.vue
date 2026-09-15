<template>
  <div
    class="group relative rounded-xl border border-black/[0.07] dark:border-white/[0.08] frosted-glass glass-surface overflow-hidden cursor-pointer transition-all hover:shadow-md hover:border-black/[0.12] dark:hover:border-white/[0.14]"
    @click="emit('open')"
  >
    <!-- Cover art -->
    <div class="relative aspect-square w-full bg-gray-100/80 dark:bg-gray-800/50 overflow-hidden">
      <img
        v-if="coverUrl && !coverErrored"
        :src="coverUrl"
        :alt="playlist.name"
        class="w-full h-full object-cover transition-transform duration-300 group-hover:scale-[1.03]"
        @error="coverErrored = true"
      />
      <div v-else class="w-full h-full">
        <PlaylistCover :playlist="playlist" />
      </div>

      <!-- Play overlay -->
      <div
        class="absolute inset-0 bg-black/20 opacity-0 group-hover:opacity-100 transition-opacity duration-200 pointer-events-none"
      />
      <button
        class="absolute bottom-2.5 right-2.5 w-9 h-9 rounded-full bg-white dark:bg-white/90 shadow-lg flex items-center justify-center text-gray-900 translate-y-1 opacity-0 group-hover:translate-y-0 group-hover:opacity-100 transition-all duration-200 hover:scale-105 active:scale-95 pointer-events-auto"
        :class="{ 'opacity-100 translate-y-0': isPlaying }"
        @click.stop="emit('play')"
      >
        <UIcon
          :name="isPlaying ? 'mdi:loading' : 'mdi:play'"
          class="w-4 h-4"
          :class="{ 'animate-spin': isPlaying, 'ml-0.5': !isPlaying }"
        />
      </button>
    </div>

    <!-- Body -->
    <div class="p-3 flex items-start justify-between gap-1.5">
      <div class="min-w-0 flex-1">
        <UBadge
          v-if="badgeLabel"
          :label="badgeLabel"
          color="neutral"
          variant="subtle"
          size="sm"
          class="mb-1.5"
        />
        <p class="font-medium text-sm text-gray-900 dark:text-white/90 truncate leading-tight">
          {{ playlist.name }}
        </p>
        <p
          v-if="playlist.description"
          class="text-xs text-gray-400 dark:text-white/35 truncate mt-0.5 leading-tight"
        >
          {{ playlist.description }}
        </p>
        <div class="flex items-center gap-2 mt-1.5">
          <span class="text-xs text-gray-400 dark:text-white/35">
            {{ playlist.itemCount }} {{ playlist.itemCount === 1 ? "track" : "tracks" }}
          </span>
          <span class="text-xs text-gray-300 dark:text-white/20">·</span>
          <span class="text-xs text-gray-400 dark:text-white/35">{{
            formatDate(playlist.updatedAt ?? playlist.createdAt)
          }}</span>
        </div>
      </div>

      <!-- Actions dropdown -->
      <UDropdownMenu :items="menuItems" :content="{ align: 'end' }" @click.stop>
        <UButton
          icon="mdi:dots-horizontal"
          color="neutral"
          variant="ghost"
          size="xs"
          class="opacity-0 group-hover:opacity-100 transition-opacity shrink-0 -mr-1"
          @click.stop
        />
      </UDropdownMenu>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";

import { type PlaylistResponse, playlistApi } from "@/api/playlist";
import PlaylistCover from "@/components/streaming/PlaylistCover.vue";
import { formatDate } from "@/utils/date-formatters";
import { playlistBadgeLabel } from "@/utils/playlist-display.utils";

const { playlist, isPlaying } = defineProps<{
  playlist: PlaylistResponse;
  isPlaying?: boolean;
}>();

const coverUrl = computed(() =>
  playlist.hasCover ? playlistApi.getPlaylistCoverUrl(playlist.id, playlist.updatedAt) : null,
);
const coverErrored = ref(false);

const badgeLabel = computed(() => playlistBadgeLabel(playlist));

// A fresh upload bumps updatedAt (new ?v= cache-buster), so drop any latched
// error then instead of hiding a cover that exists now.
watch(
  () => [playlist.id, playlist.updatedAt],
  () => {
    coverErrored.value = false;
  },
);

const emit = defineEmits<{
  open: [];
  edit: [];
  delete: [];
  play: [];
}>();

const menuItems = [
  [
    {
      label: "Open",
      icon: "lucide:arrow-up-right",
      onSelect: () => emit("open"),
    },
    {
      label: "Play",
      icon: "mdi:play",
      onSelect: () => emit("play"),
    },
    {
      label: "Edit",
      icon: "mdi:pencil",
      onSelect: () => emit("edit"),
    },
  ],
  [
    {
      label: "Delete",
      icon: "i-heroicons-trash",
      color: "error" as const,
      onSelect: () => emit("delete"),
    },
  ],
];
</script>
