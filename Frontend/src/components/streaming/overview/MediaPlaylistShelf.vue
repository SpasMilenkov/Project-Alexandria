<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";
import { RouterLink } from "vue-router";

import { isAuthError } from "@/api/shuffle";
import HomeCollectionCover from "@/components/dashboard/home/widgets/HomeCollectionCover.vue";
import { mediaOverviewPlaylists } from "@/queries/media-overview";
import { playlistBadgeLabel } from "@/utils/playlist-display.utils";

import MediaOverviewSection from "./MediaOverviewSection.vue";

const { userId, session, automatic } = defineProps<{
  userId: string;
  session: string;
  automatic: boolean;
}>();

const query = useQuery(() => mediaOverviewPlaylists({ userId, session, automatic }));
const title = computed(() => (automatic ? "Made from your library" : "Your playlists"));
const destination = computed(() => ({
  name: "playlists",
  query: { origin: automatic ? "auto" : "manual" },
}));
</script>

<template>
  <MediaOverviewSection
    :title="title"
    :description="
      automatic
        ? 'Automatic collections of artists, albums, and more.'
        : 'Your own selections, newest first.'
    "
    :to="destination"
    :loading="query.status.value === 'pending' || isAuthError(query.error.value)"
    :failed="Boolean(query.error.value)"
    :empty="query.data.value?.items.length === 0"
    empty-icon="i-mdi-playlist-music"
    :empty-title="automatic ? 'Your collections are taking shape' : 'Make room for a playlist'"
    :empty-description="
      automatic
        ? 'Automatic collections appear as your music library grows.'
        : 'Create a playlist to keep your favorite tracks together.'
    "
    @retry="query.refetch()"
  >
    <div
      :class="
        automatic
          ? 'grid grid-cols-2 gap-4 sm:grid-cols-3 xl:grid-cols-6'
          : 'divide-y divide-gray-200/70 dark:divide-gray-700/70'
      "
    >
      <RouterLink
        v-for="playlist in query.data.value?.items"
        :key="playlist.id"
        :to="{ name: 'playlist-details', params: { id: playlist.id } }"
        class="group min-w-0 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary"
        :class="automatic ? 'rounded-xl' : 'flex items-center gap-4 py-4'"
      >
        <div
          class="overflow-hidden rounded-xl"
          :class="automatic ? 'aspect-square' : 'size-16 shrink-0'"
        >
          <HomeCollectionCover :playlist="playlist" />
        </div>

        <div class="min-w-0" :class="{ 'mt-4': automatic }">
          <p
            class="truncate text-sm font-medium text-gray-900 group-hover:underline dark:text-gray-100"
            :title="playlist.name"
          >
            {{ playlist.name }}
          </p>
          <p class="mt-1 truncate text-xs text-gray-600 dark:text-gray-400">
            <span v-if="automatic">{{ playlistBadgeLabel(playlist) }} · </span>
            {{ playlist.itemCount }} {{ playlist.itemCount === 1 ? "track" : "tracks" }}
          </p>
        </div>
      </RouterLink>
    </div>
  </MediaOverviewSection>
</template>
