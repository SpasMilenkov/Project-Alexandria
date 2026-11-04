<script setup lang="ts">
import { computed, ref, watch } from "vue";

import type { TopSong } from "@/api/listening-rankings";
import type { HomeWidget } from "@/types/home";

import { isAuthError } from "@/api/shuffle";
import HomeWidgetDataState from "@/components/dashboard/home/HomeWidgetDataState.vue";
import { useHomeWidgetPreview, useHomeWidgetQuery } from "@/composables/useHomeWidgetPreview";
import { useHomeWidgetRows } from "@/composables/useHomeWidgetRows";
import { homeTopSongs } from "@/queries/home-top-songs";
import { useAuthStore } from "@/stores/auth";
import { useHomeStore } from "@/stores/home";
import { usePlayerStore } from "@/stores/stream-player";
import {
  homeTopSongArtist,
  homeTopSongBar,
  homeTopSongPlays,
  homeTopSongTitle,
  homeTopSongsPeriod,
} from "@/utils/home-top-songs";

import HomeTopSongArtwork from "./HomeTopSongArtwork.vue";

defineProps<{ widget: HomeWidget }>();

const home = useHomeStore();
const auth = useAuthStore();
const player = useHomeWidgetPreview()?.player ?? usePlayerStore();
const streamingEnabled = import.meta.env.VITE_STREAMING_ENABLED === "true";

const ownerMatches = computed(() => Boolean(home.ownerId) && auth.user?.user.id === home.ownerId);

const period = ref(homeTopSongsPeriod());
const rows = ref<HTMLElement | null>(null);
const capacity = useHomeWidgetRows(rows, () => 56);

watch(
  () => home.ownerEpoch,
  () => {
    period.value = homeTopSongsPeriod();
  },
);

const query = useHomeWidgetQuery(
  () =>
    homeTopSongs({
      userId: home.ownerId ?? "",
      epoch: home.ownerEpoch,
      enabled: streamingEnabled && ownerMatches.value,
      ...period.value,
    }),
  (preview) => ({
    userId: home.ownerId ?? "",
    epoch: home.ownerEpoch,
    enabled: streamingEnabled && ownerMatches.value,
    ...period.value,
    value: { ...period.value, items: preview.topSongs },
  }),
);

const data = computed(() => {
  const response = query.data.value;

  if (
    !ownerMatches.value ||
    response?.userId !== home.ownerId ||
    response.epoch !== home.ownerEpoch ||
    response.from !== period.value.from ||
    response.to !== period.value.to
  )
    return null;

  return response.value.items;
});

const visibleRows = computed(
  () =>
    data.value?.slice(0, capacity.value).map((song, index) => ({
      song,
      rank: index + 1,
      title: homeTopSongTitle(song),
      artist: homeTopSongArtist(song),
      plays: homeTopSongPlays(song),
      bar: homeTopSongBar(song, data.value?.[0]?.qualifiedPlayCount ?? 0),
    })) ?? [],
);

const authPending = computed(() => isAuthError(query.error.value));

const playbackDisabled = computed(
  () =>
    !streamingEnabled ||
    home.isEditing ||
    !ownerMatches.value ||
    player.playbackOwnerId !== home.ownerId ||
    player.orderBusy,
);

const refresh = () => {
  if (!ownerMatches.value || !streamingEnabled || home.isEditing) return;

  const next = homeTopSongsPeriod();

  if (next.from !== period.value.from || next.to !== period.value.to) {
    period.value = next;

    return;
  }

  void query.refresh();
};

const play = (song: TopSong) => {
  if (playbackDisabled.value || !data.value?.includes(song)) return;

  void player.playNow([song.file]);
};
</script>

<template>
  <div
    class="top-songs-widget flex min-h-0 min-w-0 flex-1 flex-col gap-2 text-gray-900 dark:text-gray-100"
    :class="{ 'pointer-events-none': home.isEditing }"
    data-home-top-songs
  >
    <p class="shrink-0 text-xs text-gray-600 dark:text-gray-400">Last 7 days · UTC</p>

    <div
      v-if="!streamingEnabled"
      class="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 text-center"
    >
      <UIcon
        name="i-mdi-music-note-outline"
        class="size-8 text-gray-400 dark:text-gray-600"
        aria-hidden="true"
      />
      <p class="text-sm font-medium">Streaming is unavailable</p>
    </div>

    <HomeWidgetDataState
      v-else
      :loading="authPending || (!data && !query.error.value)"
      :failed="!data && !authPending && Boolean(query.error.value)"
      @retry="refresh"
    >
      <div
        v-if="data && !data.length"
        class="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 text-center"
      >
        <UIcon
          name="i-mdi-music-note-outline"
          class="top-songs-empty-icon size-8 text-gray-400 dark:text-gray-600"
          aria-hidden="true"
        />
        <p class="text-sm font-medium">No songs played yet</p>
        <p class="top-songs-description text-xs text-gray-600 dark:text-gray-400">
          Songs you listen to will appear here.
        </p>

        <UButton
          v-if="streamingEnabled"
          to="/streaming/music"
          color="neutral"
          variant="outline"
          size="xs"
          :disabled="home.isEditing"
          >Browse music</UButton
        >
      </div>

      <div
        v-else-if="data"
        ref="rows"
        class="song-rows flex min-h-0 flex-1 flex-col overflow-hidden"
      >
        <button
          v-for="row in visibleRows"
          :key="row.song.file.fileId"
          type="button"
          class="song-row flex h-14 shrink-0 flex-col justify-center gap-1 rounded-lg text-left focus-visible:outline-2 focus-visible:outline-primary focus-visible:outline-offset-[-2px] enabled:hover:bg-gray-100/60 dark:enabled:hover:bg-gray-800/60"
          :disabled="playbackDisabled"
          :aria-label="`Play ${row.title} by ${row.artist}, ranked ${row.rank}, ${row.plays}`"
          :title="`${row.title} · ${row.artist} · ${row.plays}`"
          @click="play(row.song)"
        >
          <span class="flex min-w-0 items-center gap-2">
            <span
              class="song-rank w-4 shrink-0 text-xs tabular-nums text-gray-500 dark:text-gray-500"
              >{{ row.rank }}</span
            >
            <HomeTopSongArtwork class="song-artwork" :file="row.song.file" />

            <span class="flex min-w-0 flex-1 flex-col">
              <span class="truncate text-xs font-medium">{{ row.title }}</span>
              <span class="truncate text-xs text-gray-600 dark:text-gray-400">{{
                row.artist
              }}</span>
            </span>

            <span class="shrink-0 text-xs font-medium tabular-nums">{{ row.plays }}</span>
          </span>

          <span
            class="h-1 w-full overflow-hidden rounded-full bg-gray-200 dark:bg-gray-700"
            aria-hidden="true"
          >
            <span
              class="block h-full rounded-full bg-gray-500 dark:bg-gray-400"
              :style="{ width: `${row.bar}%` }"
            />
          </span>
        </button>
      </div>
    </HomeWidgetDataState>
  </div>
</template>

<style scoped>
@container home-widget (max-width: 220px) {
  .song-artwork,
  .song-rank {
    display: none;
  }
}

@container home-widget (max-height: 200px) {
  .top-songs-empty-icon,
  .top-songs-description {
    display: none;
  }
}
</style>
