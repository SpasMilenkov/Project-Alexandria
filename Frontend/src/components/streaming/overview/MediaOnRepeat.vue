<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { TopSong } from "@/api/listening-rankings";

import { isAuthError } from "@/api/shuffle";
import { MEDIA_OVERVIEW_PAGE_SIZES, mediaOverviewOnRepeat } from "@/queries/media-overview";
import { usePlayerStore } from "@/stores/stream-player";
import { coverSeed } from "@/utils/generated-cover";
import {
  homeTopSongArtist,
  homeTopSongBar,
  homeTopSongPlays,
  homeTopSongTitle,
  homeTopSongsPeriod,
} from "@/utils/home-top-songs";

import MediaArtwork from "./MediaArtwork.vue";
import MediaEqualizer from "./MediaEqualizer.vue";
import MediaOverviewSection from "./MediaOverviewSection.vue";

const { userId, session } = defineProps<{ userId: string; session: string }>();

const player = usePlayerStore();
const period = { ...homeTopSongsPeriod(), limit: MEDIA_OVERVIEW_PAGE_SIZES.onRepeat };
const query = useQuery(() => mediaOverviewOnRepeat({ userId, session, ...period }));
const disabled = computed(() => player.playbackOwnerId !== userId || player.orderBusy);
const leadingCount = computed(() => query.data.value?.items[0]?.qualifiedPlayCount ?? 0);

const isActive = (song: TopSong) => player.activeFile?.fileId === song.file.fileId;

const play = (song: TopSong) => {
  if (disabled.value) return;

  if (isActive(song)) {
    void player.togglePlay();

    return;
  }

  void player.playNow([song.file]);
};

const label = (song: TopSong) => {
  const action = isActive(song) && player.isPlaying ? "Pause" : "Play";

  return `${action} ${homeTopSongTitle(song)}`;
};
</script>

<template>
  <MediaOverviewSection
    title="On repeat"
    description="Last 7 days"
    to="/stats"
    link-label="Your listening"
    :loading="query.status.value === 'pending' || isAuthError(query.error.value)"
    :failed="Boolean(query.error.value)"
    :empty="query.data.value?.items.length === 0"
    empty-seed="empty-on-repeat"
    empty-title="Nothing on repeat yet"
    empty-description="Your most-played songs of the last 7 days will rank here."
    @retry="query.refetch()"
  >
    <ol class="-mx-2 flex flex-col">
      <li v-for="(song, index) in query.data.value?.items" :key="song.file.fileId">
        <button
          type="button"
          :aria-label="label(song)"
          :disabled="disabled"
          class="flex min-h-16 w-full items-center gap-3 rounded-xl px-2 py-2 text-left transition-colors hover:bg-gray-500/10 focus-visible:outline-2 focus-visible:outline-primary disabled:cursor-wait"
          @click="play(song)"
        >
          <span
            class="flex w-7 shrink-0 justify-center text-xl font-semibold tabular-nums text-gray-500 dark:text-gray-500"
          >
            <MediaEqualizer
              v-if="isActive(song)"
              :playing="player.isPlaying"
              class="text-primary"
            />

            <span v-else>{{ index + 1 }}</span>
          </span>

          <MediaArtwork
            :file-id="song.file.fileId"
            :version-id="song.file.playbackVersionId ?? song.file.currentVersionId"
            :mime-type="song.file.mimeType"
            :seed="coverSeed(song.file)"
            class="size-11 shrink-0 rounded-lg"
          />

          <span class="min-w-0 flex-1">
            <span class="block truncate text-sm font-semibold text-gray-900 dark:text-gray-100">{{
              homeTopSongTitle(song)
            }}</span>

            <span class="mt-1 flex min-w-0 items-center gap-2">
              <span class="truncate text-xs text-gray-600 dark:text-gray-400">{{
                homeTopSongArtist(song)
              }}</span>

              <span
                class="h-[3px] min-w-10 flex-1 overflow-hidden rounded-full bg-gray-500/15"
                aria-hidden="true"
              >
                <span
                  class="block h-full rounded-full bg-gray-700 dark:bg-gray-300"
                  :style="{ width: `${homeTopSongBar(song, leadingCount)}%` }"
                />
              </span>
            </span>
          </span>

          <span class="shrink-0 text-xs tabular-nums text-gray-600 dark:text-gray-400">{{
            homeTopSongPlays(song)
          }}</span>
        </button>
      </li>
    </ol>
  </MediaOverviewSection>
</template>
