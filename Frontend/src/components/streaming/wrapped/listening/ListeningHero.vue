<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { useIntervalFn, useNow } from "@vueuse/core";
import { computed, ref } from "vue";
import { RouterLink } from "vue-router";

import type { TopSong } from "@/api/listening-rankings";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import MediaArtwork from "@/components/streaming/overview/MediaArtwork.vue";
import { useTheme } from "@/composables/useTheme";
import { coverInk, heroTint } from "@/features/media/cover-color";
import { coverSeed, generatedCover } from "@/features/media/generated-cover";
import { homeTopSongArtist, homeTopSongPlays, homeTopSongTitle } from "@/features/media/top-songs";
import { formatWrappedDuration } from "@/features/wrapped/art";
import { type YearMonthBar, yearBounds, yearMonthBars } from "@/features/wrapped/listening/year";
import { getTimeline, getTopSongs } from "@/queries/stats";
import { useAuthStore } from "@/stores/auth";
import { usePlayerStore } from "@/stores/stream-player";
import { formatNumber } from "@/utils/count";

const TOP_SONG_LIMIT = 5;

const auth = useAuthStore();
const player = usePlayerStore();
const { isDark } = useTheme();

const userId = computed(() => auth.user?.user.id);
const now = useNow({ scheduler: (cb) => useIntervalFn(cb, 60_000) });
const year = new Date().getUTCFullYear();
const from = yearBounds(year).from;
const coverColor = ref<string | null>(null);

// Shares its cache entry with the year view of the timeline below.
const timeline = useQuery(() => getTimeline({ from, userId: userId.value }));
const topSongs = useQuery(() => getTopSongs({ from, limit: TOP_SONG_LIMIT, userId: userId.value }));

const authWaiting = computed(
  () => !userId.value || isAuthError(timeline.error.value) || isAuthError(topSongs.error.value),
);

const loading = computed(
  () =>
    authWaiting.value || timeline.status.value === "pending" || topSongs.status.value === "pending",
);

const failed = computed(() => Boolean(timeline.error.value) && !authWaiting.value);

const songs = computed(() => topSongs.data.value?.items ?? []);
const leader = computed(() => songs.value[0] ?? null);
const totalSeconds = computed(() => timeline.data.value?.totalSeconds ?? 0);
const empty = computed(() => totalSeconds.value <= 0 && songs.value.length === 0);

const facts = computed(() => {
  const data = timeline.data.value;

  if (!data) return [];

  return [
    { label: "Plays", value: formatNumber(data.qualifiedPlayCount) },
    { label: "Listening days", value: formatNumber(data.activeDays) },
  ];
});

const months = computed(() => yearMonthBars(timeline.data.value?.months ?? [], year, now.value));

const elapsedMonths = computed(() => months.value.filter((month) => !month.future));

const leaderSeed = computed(() => (leader.value ? coverSeed(leader.value.file) : `year-${year}`));

const tint = computed(() => {
  if (!coverColor.value) return null;

  return heroTint(coverColor.value, isDark.value);
});

const ink = computed(() => {
  const fallback = generatedCover(leaderSeed.value).base;

  return coverInk(coverColor.value ?? fallback, isDark.value, fallback);
});

const disabled = computed(
  () => player.playbackOwnerId !== userId.value || player.orderBusy || songs.value.length === 0,
);

const playTopSongs = () => {
  if (disabled.value) return;

  void player.playNow(songs.value.map((song) => song.file));
};

const songRoute = (song: TopSong) => ({
  name: "track-details",
  params: { fileId: song.file.fileId },
});

const barStyle = (month: YearMonthBar) => {
  if (month.seconds <= 0) return undefined;

  return {
    height: `${month.height}%`,
    backgroundColor: ink.value,
    opacity: month.current ? 1 : 0.7,
  };
};

const monthTitle = (month: YearMonthBar) => {
  if (month.future) return `${month.label} · still ahead`;

  return `${month.label} · ${formatWrappedDuration(month.seconds)}`;
};
</script>

<template>
  <section
    aria-labelledby="listening-title"
    class="frosted-glass glass-surface-strong relative isolate overflow-hidden rounded-3xl border border-gray-200/70 p-5 sm:p-6 dark:border-gray-700/70"
  >
    <div
      v-if="tint && !loading && !empty"
      class="pointer-events-none absolute inset-0 -z-10 opacity-35 transition-colors duration-500 motion-reduce:transition-none dark:opacity-45"
      :style="{ backgroundColor: tint }"
      aria-hidden="true"
    />

    <div
      class="flex items-center gap-2 text-xs font-semibold tracking-widest text-gray-600 uppercase dark:text-gray-400"
    >
      <h1 id="listening-title">Your listening</h1>
      <span aria-hidden="true">·</span>
      <span>{{ year }} so far</span>
    </div>

    <div
      v-if="loading"
      class="flex h-56 items-center justify-center"
      role="status"
      aria-label="Loading your year in music"
    >
      <BlockSpinner :size="24" class="text-gray-500" />
    </div>

    <div v-else-if="failed" class="flex h-56 flex-col items-center justify-center text-center">
      <p class="font-semibold text-gray-900 dark:text-gray-100">Could not load your year</p>

      <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
        Something went wrong adding up your listening.
      </p>

      <UButton class="mt-4" variant="outline" color="neutral" @click="timeline.refetch()"
        >Try again</UButton
      >
    </div>

    <div v-else-if="empty" class="flex h-56 flex-col items-center justify-center text-center">
      <UIcon name="mdi:music-note-outline" class="h-12 w-12 text-gray-400 dark:text-gray-600" />

      <h2 class="mt-4 font-semibold text-gray-900 dark:text-gray-100">
        Nothing played in {{ year }} yet
      </h2>

      <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
        Your year in music starts with the first song.
      </p>

      <UButton class="mt-6" to="/streaming/music" color="primary">Browse music</UButton>
    </div>

    <div v-else class="mt-4 flex flex-wrap items-center gap-6 sm:gap-8">
      <MediaArtwork
        :key="leaderSeed"
        :file-id="leader?.file.fileId"
        :version-id="leader?.file.playbackVersionId ?? leader?.file.currentVersionId"
        :mime-type="leader?.file.mimeType"
        :cover-updated-at="leader?.file.coverUpdatedAt"
        :seed="leaderSeed"
        skeleton
        class="aspect-square w-40 shrink-0 rounded-2xl shadow-xl sm:w-[clamp(176px,12vw,232px)]"
        @color="coverColor = $event"
      />

      <div class="flex min-w-0 flex-[1_1_320px] flex-col gap-4">
        <div>
          <p
            class="text-5xl font-black tracking-tight text-gray-900 tabular-nums sm:text-6xl dark:text-gray-100"
          >
            {{ formatWrappedDuration(totalSeconds) }}
          </p>

          <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
            with your music this year
            <template v-if="leader">
              · most played
              <RouterLink
                :to="songRoute(leader)"
                class="font-semibold text-gray-900 underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-100"
                >{{ homeTopSongTitle(leader) }}</RouterLink
              >
            </template>
          </p>
        </div>

        <dl class="flex flex-wrap gap-x-8 gap-y-2">
          <div v-for="fact in facts" :key="fact.label" class="min-w-0">
            <dt class="text-xs text-gray-600 dark:text-gray-400">{{ fact.label }}</dt>
            <dd class="mt-1 text-xl font-bold text-gray-900 tabular-nums dark:text-gray-100">
              {{ fact.value }}
            </dd>
          </div>
        </dl>

        <figure class="max-w-md" :aria-label="`Listening by month in ${year}`">
          <div class="flex h-14 items-end gap-1.5" aria-hidden="true">
            <span
              v-for="month in months"
              :key="month.key"
              :title="monthTitle(month)"
              class="min-w-0 flex-1 rounded-t-sm"
              :class="{
                'h-1 border border-dashed border-gray-400 dark:border-gray-600': month.future,
                'h-px bg-gray-500/30': !month.future && month.seconds <= 0,
              }"
              :style="barStyle(month)"
            />
          </div>

          <div class="mt-1.5 flex gap-1.5" aria-hidden="true">
            <span
              v-for="month in months"
              :key="month.key"
              class="min-w-0 flex-1 text-center text-[10px] text-gray-600 dark:text-gray-400"
              :class="{ 'font-bold text-gray-900 dark:text-gray-100': month.current }"
              >{{ month.initial }}</span
            >
          </div>

          <ul class="sr-only">
            <li v-for="month in elapsedMonths" :key="month.key">
              {{ monthTitle(month) }}
            </li>
          </ul>
        </figure>

        <div class="flex flex-wrap gap-2">
          <UButton
            color="primary"
            size="lg"
            icon="i-mdi-play"
            :disabled="disabled"
            class="min-h-11 rounded-full px-6"
            @click="playTopSongs"
            >Play top songs</UButton
          >

          <UButton
            :to="`/stats/wrapped/${year}`"
            color="neutral"
            variant="outline"
            size="lg"
            icon="mdi:gift-outline"
            class="min-h-11 rounded-full px-5"
            >Open {{ year }} Wrapped</UButton
          >
        </div>
      </div>

      <div
        v-if="songs.length > 0"
        class="flex min-w-0 flex-[1_1_300px] flex-col gap-1 rounded-2xl bg-gray-500/10 p-3"
      >
        <h2
          id="listening-top-songs"
          class="px-2 pb-1 text-xs font-semibold tracking-widest text-gray-600 uppercase dark:text-gray-400"
        >
          Top songs this year
        </h2>

        <ol class="flex flex-col" aria-labelledby="listening-top-songs">
          <li v-for="(song, index) in songs" :key="song.file.fileId">
            <RouterLink
              :to="songRoute(song)"
              class="flex min-h-13 items-center gap-3 rounded-xl px-2 py-1.5 transition-colors hover:bg-gray-500/10 focus-visible:outline-2 focus-visible:outline-primary"
            >
              <span
                class="w-5 shrink-0 text-center text-sm font-semibold text-gray-600 tabular-nums dark:text-gray-400"
                >{{ index + 1 }}</span
              >

              <MediaArtwork
                :file-id="song.file.fileId"
                :version-id="song.file.playbackVersionId ?? song.file.currentVersionId"
                :mime-type="song.file.mimeType"
                :cover-updated-at="song.file.coverUpdatedAt"
                :seed="coverSeed(song.file)"
                class="size-10 shrink-0 rounded-lg"
              />

              <span class="min-w-0 flex-1">
                <span
                  class="block truncate text-sm font-semibold text-gray-900 dark:text-gray-100"
                  >{{ homeTopSongTitle(song) }}</span
                >

                <span class="block truncate text-xs text-gray-600 dark:text-gray-400">{{
                  homeTopSongArtist(song)
                }}</span>
              </span>

              <span class="shrink-0 text-xs text-gray-600 tabular-nums dark:text-gray-400">{{
                homeTopSongPlays(song)
              }}</span>
            </RouterLink>
          </li>
        </ol>
      </div>
    </div>
  </section>
</template>
