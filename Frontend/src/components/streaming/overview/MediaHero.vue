<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";
import { RouterLink } from "vue-router";

import type { UpNextItem } from "@/stores/player/types";

import { isAuthError } from "@/api/shuffle";
import { type MediaFileDto, type StreamHistoryResponse, streamingApi } from "@/api/streaming";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { useAppToast } from "@/composables/useAppToast";
import { useTheme } from "@/composables/useTheme";
import { heroTint } from "@/features/media/cover-color";
import { coverSeed } from "@/features/media/generated-cover";
import { mediaOverviewHistory, mediaOverviewLibrary } from "@/queries/media-overview";
import { usePlayerStore } from "@/stores/stream-player";
import { formatDuration } from "@/utils/date-formatters";

import MediaArtwork from "./MediaArtwork.vue";
import MediaEqualizer from "./MediaEqualizer.vue";
import MediaMarquee from "./MediaMarquee.vue";

const { userId, session } = defineProps<{ userId: string; session: string }>();

interface HeroSubject {
  kind: "active" | "history" | "fresh";
  fileId: string;
  versionId?: string;
  mimeType?: string;
  title: string;
  artist: string | null;
  album: string | null;
  isVideo: boolean;
  position: number;
  duration: number;
  seed: string;
}

const EYEBROW: Record<HeroSubject["kind"], string> = {
  active: "Now playing",
  history: "Pick up where you left off",
  fresh: "Newest in your library",
};

const player = usePlayerStore();
const toast = useAppToast("playback");
const { isDark } = useTheme();
const history = useQuery(() => mediaOverviewHistory({ userId, session }));
const music = useQuery(() => mediaOverviewLibrary({ userId, session, isVideo: false }));
const coverColor = ref<string | null>(null);
const resuming = ref(false);

const isOwner = computed(() => player.playbackOwnerId === userId);
const disabled = computed(() => !isOwner.value || player.orderBusy || resuming.value);

const fromFile = (file: MediaFileDto, kind: "active" | "fresh"): HeroSubject => ({
  kind,
  fileId: file.fileId,
  versionId: file.playbackVersionId ?? file.currentVersionId,
  mimeType: file.mimeType,
  title: file.title || file.fileName,
  artist: file.artist,
  album: file.album,
  isVideo: file.isVideo,
  position: kind === "active" ? (player.currentTime ?? 0) : 0,
  duration: kind === "active" ? player.duration || file.duration || 0 : (file.duration ?? 0),
  seed: coverSeed(file),
});

const fromHistory = (entry: StreamHistoryResponse): HeroSubject => ({
  kind: "history",
  fileId: entry.fileId,
  title: entry.title,
  artist: entry.artist ?? null,
  album: entry.album ?? null,
  isVideo: entry.isVideo ?? false,
  position: entry.hasFinished ? 0 : entry.positionSeconds,
  duration: entry.duration ?? 0,
  seed: coverSeed({ fileId: entry.fileId, artist: entry.artist, album: entry.album }),
});

const subject = computed((): HeroSubject | null => {
  if (player.activeFile && isOwner.value) return fromFile(player.activeFile, "active");

  const last = history.data.value?.items[0];
  if (last) return fromHistory(last);

  const newest = music.data.value?.items[0];
  if (newest) return fromFile(newest, "fresh");

  return null;
});

// Wait for every source that could outrank the current pick, otherwise the hero swaps subjects mid-load.
const resolving = computed(() => {
  if (player.activeFile && isOwner.value) return false;

  const historySettling = history.status.value === "pending" || isAuthError(history.error.value);
  if (historySettling) return true;
  if (history.data.value?.items[0]) return false;

  return music.status.value === "pending" || isAuthError(music.error.value);
});

const isPlaying = computed(() => subject.value?.kind === "active" && player.isPlaying);
const progress = computed(() => {
  const current = subject.value;
  if (!current || current.duration <= 0) return 0;

  return Math.min(100, (current.position / current.duration) * 100);
});

const meta = computed(() =>
  [subject.value?.artist, subject.value?.album].filter((part) => part?.trim()).join(" · "),
);

const eyebrow = computed(() => {
  const current = subject.value;
  if (!current) return "";
  if (current.kind === "active" && !player.isPlaying) return "Paused";

  return EYEBROW[current.kind];
});

const playLabel = computed(() => {
  if (isPlaying.value) return "Pause";
  if (subject.value?.kind === "history" && progress.value > 0) return "Resume";

  return "Play";
});

const tint = computed(() => {
  if (!coverColor.value) return null;

  return heroTint(coverColor.value, isDark.value);
});

const upNext = computed(() => {
  if (subject.value?.kind !== "active") return [];

  return (player.upNextItems ?? []).filter((item) => item.kind !== "history").slice(0, 4);
});

const detailsRoute = computed(() => {
  const current = subject.value;
  if (!current || current.isVideo) return "/streaming/videos";

  return { name: "track-details", params: { fileId: current.fileId } };
});

const play = async () => {
  const current = subject.value;
  if (!current || disabled.value) return;

  if (current.kind === "active") {
    void player.togglePlay();

    return;
  }

  if (current.kind === "fresh" && music.data.value?.items[0]) {
    const file = music.data.value.items[0];
    void player.playTrackInContext(file, { isVideo: false, playlistId: null }, "Music");

    return;
  }

  resuming.value = true;

  try {
    const file = await streamingApi.getStreamingFile(current.fileId);
    if (file) void player.playNow([file]);
  } catch (error) {
    toast.error("Couldn't resume playback", error, { retry: play });
  } finally {
    resuming.value = false;
  }
};

const playUpNext = (item: UpNextItem) => {
  if (disabled.value) return;

  if (item.kind === "queue") {
    void player.skipToQueue(item.queueIndex);

    return;
  }

  void player.jumpTo(item.position);
};
</script>

<template>
  <section
    v-if="resolving"
    aria-label="Now playing"
    class="frosted-glass glass-surface-strong flex items-center justify-center rounded-3xl border border-gray-200/70 p-5 sm:p-6 dark:border-gray-700/70"
  >
    <div class="flex h-40 items-center justify-center sm:h-[clamp(176px,10vw,224px)]">
      <BlockSpinner :size="24" aria-label="Loading now playing" class="text-gray-500" />
    </div>
  </section>

  <section
    v-else-if="subject"
    aria-label="Now playing"
    class="frosted-glass glass-surface-strong relative isolate flex flex-wrap items-center gap-6 overflow-hidden rounded-3xl border border-gray-200/70 p-5 sm:gap-8 sm:p-6 dark:border-gray-700/70"
  >
    <div
      v-if="tint"
      class="pointer-events-none absolute inset-0 -z-10 opacity-35 transition-colors duration-500 motion-reduce:transition-none dark:opacity-45"
      :style="{ backgroundColor: tint }"
      aria-hidden="true"
    />

    <MediaArtwork
      :key="subject.fileId"
      :file-id="subject.fileId"
      :version-id="subject.versionId"
      :mime-type="subject.mimeType"
      :seed="subject.seed"
      skeleton
      class="aspect-square w-40 shrink-0 rounded-2xl shadow-xl sm:w-[clamp(176px,10vw,224px)]"
      @color="coverColor = $event"
    />

    <div class="flex min-w-0 flex-[1_1_320px] flex-col gap-3">
      <p
        class="flex items-center gap-2 text-xs font-semibold uppercase tracking-widest text-gray-600 dark:text-gray-400"
      >
        <MediaEqualizer
          v-if="subject.kind === 'active'"
          :playing="isPlaying"
          class="text-primary"
        />

        {{ eyebrow }}
      </p>

      <h2
        class="min-w-0 py-1 text-3xl font-bold leading-tight tracking-tight text-gray-900 xl:text-4xl dark:text-gray-100"
      >
        <MediaMarquee :text="subject.title" />
      </h2>

      <p v-if="meta" class="truncate text-base text-gray-600 dark:text-gray-400">{{ meta }}</p>

      <div v-if="progress > 0" class="mt-1 flex max-w-md flex-col gap-1.5">
        <div class="h-1 overflow-hidden rounded-full bg-gray-500/20">
          <div
            class="h-full rounded-full bg-gray-900 dark:bg-gray-100"
            :style="{ width: `${progress}%` }"
          />
        </div>

        <div class="flex justify-between text-xs tabular-nums text-gray-600 dark:text-gray-400">
          <span>{{ formatDuration(subject.position) }}</span>
          <span>{{ formatDuration(subject.duration) }}</span>
        </div>
      </div>

      <div class="mt-1 flex flex-wrap gap-2">
        <UButton
          color="primary"
          size="lg"
          :icon="isPlaying ? 'i-mdi-pause' : 'i-mdi-play'"
          :loading="resuming"
          :disabled="disabled"
          class="min-h-11 rounded-full px-6"
          @click="play"
          >{{ playLabel }}<span class="sr-only"> {{ subject.title }}</span></UButton
        >

        <UButton
          :to="detailsRoute"
          color="neutral"
          variant="outline"
          size="lg"
          class="min-h-11 rounded-full px-5"
          >{{ subject.isVideo ? "All videos" : "Track details" }}</UButton
        >
      </div>
    </div>

    <div
      v-if="upNext.length > 0"
      class="flex min-w-0 flex-[1_1_300px] flex-col gap-1 rounded-2xl bg-gray-500/10 p-3"
    >
      <div class="flex items-center justify-between px-2 pb-1">
        <h3
          class="text-xs font-semibold uppercase tracking-widest text-gray-600 dark:text-gray-400"
        >
          Up next
        </h3>
      </div>

      <button
        v-for="item in upNext"
        :key="`${item.kind}-${item.queueIndex}-${item.position}-${item.file.fileId}`"
        type="button"
        :disabled="disabled"
        :aria-label="`Play ${item.file.title || item.file.fileName}`"
        class="flex min-h-13 items-center gap-3 rounded-xl px-2 py-1.5 text-left transition-colors hover:bg-gray-500/10 focus-visible:outline-2 focus-visible:outline-primary disabled:cursor-wait"
        @click="playUpNext(item)"
      >
        <MediaArtwork
          :file-id="item.file.fileId"
          :version-id="item.file.playbackVersionId ?? item.file.currentVersionId"
          :mime-type="item.file.mimeType"
          :seed="coverSeed(item.file)"
          skeleton
          class="size-10 shrink-0 rounded-lg"
        />

        <span class="min-w-0 flex-1">
          <span class="block truncate text-sm font-semibold text-gray-900 dark:text-gray-100">{{
            item.file.title || item.file.fileName
          }}</span>

          <span class="block truncate text-xs text-gray-600 dark:text-gray-400">{{
            item.file.artist || "Unknown artist"
          }}</span>
        </span>

        <span
          v-if="item.file.duration"
          class="shrink-0 text-xs tabular-nums text-gray-600 dark:text-gray-400"
          >{{ formatDuration(item.file.duration) }}</span
        >
      </button>
    </div>
  </section>
</template>
