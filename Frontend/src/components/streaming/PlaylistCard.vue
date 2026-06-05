<template>
  <div
    class="group relative rounded-xl border border-black/[0.07] dark:border-white/[0.08] bg-white/70 dark:bg-white/[0.04] backdrop-blur-sm overflow-hidden cursor-pointer transition-all hover:shadow-md hover:border-black/[0.12] dark:hover:border-white/[0.14]"
    @click="emit('open')"
  >
    <!-- Cover art -->
    <div class="relative aspect-square w-full bg-gray-100/80 dark:bg-gray-800/50 overflow-hidden">
      <img
        v-if="!isCoverUrlLoading && coverUrl"
        :src="coverUrl"
        :alt="playlist.name"
        class="w-full h-full object-cover transition-transform duration-300 group-hover:scale-[1.03]"
      />
      <div v-else class="w-full h-full flex flex-col items-center justify-center gap-2">
        <UIcon name="mdi:playlist-music" class="w-10 h-10 text-gray-300 dark:text-white/20" />
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
            formatDate(props.playlist.updatedAt ?? props.playlist.createdAt)
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
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { PlaylistResponse } from "@/api/playlist";

import { getPlaylistCover } from "@/queries/playlist";
import { formatDate } from "@/utils/date-formatters";

const props = defineProps<{
  playlist: PlaylistResponse;
  isPlaying?: boolean;
}>();

const { data: coverUrl, isLoading: isCoverUrlLoading } = useQuery({
  ...getPlaylistCover(props.playlist.id),
  enabled: computed(() => props.playlist.hasCover),
});

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
