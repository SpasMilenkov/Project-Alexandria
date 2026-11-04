<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";
import { RouterLink } from "vue-router";

import type { PlaylistResponse } from "@/api/playlist";

import { isAuthError } from "@/api/shuffle";
import HomeCollectionCover from "@/components/dashboard/home/widgets/collections/HomeCollectionCover.vue";
import { autoGroupKindLabel } from "@/features/playlists/display";
import { mediaOverviewPlaylists } from "@/queries/media-overview";

import MediaOverviewSection from "./MediaOverviewSection.vue";
import MediaShelfScroller from "./MediaShelfScroller.vue";

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

const trackCount = (playlist: PlaylistResponse) =>
  `${playlist.itemCount.toLocaleString()} ${playlist.itemCount === 1 ? "track" : "tracks"}`;

const collectionMeta = (playlist: PlaylistResponse) =>
  `${autoGroupKindLabel(playlist.autoGroupKind) ?? "Collection"} · ${trackCount(playlist)}`;

const detailsRoute = (playlist: PlaylistResponse) => ({
  name: "playlist-details",
  params: { id: playlist.id },
});
</script>

<template>
  <MediaOverviewSection
    :variant="automatic ? 'shelf' : 'card'"
    :title="title"
    :description="automatic ? 'Artists, albums and genres, gathered automatically' : 'Newest first'"
    :to="destination"
    :loading="query.status.value === 'pending' || isAuthError(query.error.value)"
    :failed="Boolean(query.error.value)"
    :empty="query.data.value?.items.length === 0"
    :empty-seed="automatic ? 'empty-collections' : 'empty-playlists'"
    :empty-title="automatic ? 'Your collections are taking shape' : 'No playlists yet'"
    :empty-description="
      automatic
        ? 'Collections appear as your music library grows.'
        : 'Gather favorite tracks into playlists of your own.'
    "
    @retry="query.refetch()"
  >
    <template #empty-action>
      <UButton
        v-if="!automatic"
        to="/streaming/playlists"
        color="neutral"
        variant="outline"
        class="mt-2 min-h-11"
        >New playlist</UButton
      >
    </template>

    <MediaShelfScroller v-if="automatic">
      <RouterLink
        v-for="playlist in query.data.value?.items"
        :key="playlist.id"
        :to="detailsRoute(playlist)"
        class="group w-[clamp(184px,10.4vw,256px)] min-w-0 shrink-0 snap-start rounded-2xl focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary"
      >
        <span
          class="block aspect-square overflow-hidden rounded-2xl shadow-md transition-transform duration-200 group-hover:-translate-y-1 motion-reduce:transition-none motion-reduce:group-hover:translate-y-0"
        >
          <HomeCollectionCover :playlist="playlist" />
        </span>

        <span
          class="mt-3 block truncate text-sm font-semibold text-gray-900 group-hover:underline dark:text-gray-100"
          :title="playlist.name"
          >{{ playlist.name }}</span
        >

        <span class="mt-0.5 block truncate text-sm text-gray-600 dark:text-gray-400">{{
          collectionMeta(playlist)
        }}</span>
      </RouterLink>
    </MediaShelfScroller>

    <div v-else class="-mx-2 flex flex-col">
      <RouterLink
        v-for="playlist in query.data.value?.items"
        :key="playlist.id"
        :to="detailsRoute(playlist)"
        class="group flex min-h-16 items-center gap-4 rounded-xl px-2 py-2 transition-colors hover:bg-gray-500/10 focus-visible:outline-2 focus-visible:outline-primary"
      >
        <span class="size-12 shrink-0 overflow-hidden rounded-lg">
          <HomeCollectionCover :playlist="playlist" />
        </span>

        <span class="min-w-0">
          <span
            class="block truncate text-sm font-semibold text-gray-900 group-hover:underline dark:text-gray-100"
            :title="playlist.name"
            >{{ playlist.name }}</span
          >

          <span class="mt-0.5 block truncate text-xs text-gray-600 dark:text-gray-400">{{
            trackCount(playlist)
          }}</span>
        </span>
      </RouterLink>
    </div>
  </MediaOverviewSection>
</template>
