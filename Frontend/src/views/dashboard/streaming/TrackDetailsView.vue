<template>
  <div class="px-2 md:px-6 py-5">
    <div class="flex flex-col gap-6">
      <UButton
        icon="mdi:arrow-left"
        label="Library"
        color="neutral"
        variant="soft"
        size="sm"
        class="self-start"
        @click="goBack"
      />

      <div
        v-if="trackQuery.status.value === 'pending'"
        class="frosted-glass glass-surface flex h-64 items-center justify-center rounded-3xl border border-gray-200/70 dark:border-gray-700/70"
      >
        <BlockSpinner :size="24" aria-label="Loading track" class="text-gray-500" />
      </div>

      <UAlert
        v-else-if="trackQuery.status.value === 'error'"
        icon="mdi:alert-circle-outline"
        color="error"
        variant="subtle"
        title="Failed to load track"
      />

      <div
        v-else-if="!track"
        class="frosted-glass glass-surface flex flex-col items-center gap-2 rounded-3xl border border-gray-200/70 px-6 py-16 text-center dark:border-gray-700/70"
      >
        <UIcon
          name="mdi:music-note-off-outline"
          class="w-12 h-12 text-gray-400 dark:text-gray-600"
        />
        <p class="m-0 text-base font-semibold text-gray-900 dark:text-gray-100">Track not found</p>
        <p class="m-0 text-sm text-gray-600 dark:text-gray-400">
          It may have been deleted, or it has no completed transcode yet.
        </p>
      </div>

      <template v-else>
        <div class="flex flex-col gap-4">
          <section
            :aria-label="displayName"
            class="frosted-glass glass-surface-strong relative isolate overflow-hidden rounded-3xl border border-gray-200/70 dark:border-gray-700/70"
          >
            <div
              v-if="tint"
              class="pointer-events-none absolute inset-0 -z-10 opacity-35 transition-colors duration-500 motion-reduce:transition-none dark:opacity-45"
              :style="{ backgroundColor: tint }"
              aria-hidden="true"
            />

            <div
              v-if="tint"
              class="hero-glow pointer-events-none absolute -top-1/2 -left-1/4 -z-10 aspect-square w-[min(48rem,90%)] rounded-full opacity-60"
              :style="{ background: `radial-gradient(closest-side, ${tint}, transparent)` }"
              aria-hidden="true"
            />

            <div class="flex flex-col gap-6 p-5 sm:p-7 lg:flex-row lg:items-center lg:gap-8">
              <div class="flex min-w-0 flex-1 flex-col gap-5 sm:flex-row sm:items-center sm:gap-7">
                <button
                  type="button"
                  class="group relative aspect-square w-36 shrink-0 cursor-zoom-in overflow-hidden rounded-2xl shadow-xl sm:w-44 xl:w-52"
                  :aria-label="`View full-size artwork for ${displayName}`"
                  @click="isArtworkModalOpen = true"
                >
                  <MediaArtwork
                    :file-id="track.fileId"
                    :version-id="track.currentVersionId"
                    :mime-type="track.mimeType"
                    :seed="artworkSeed"
                    zoom
                    skeleton
                    class="size-full"
                    @color="coverColor = $event"
                  />
                </button>

                <TrackMetadataEditor
                  v-if="isEditing"
                  :file="track"
                  class="min-w-0 flex-1"
                  @saved="isEditing = false"
                  @cancel="isEditing = false"
                />

                <div v-else class="flex min-w-0 flex-1 flex-col gap-3">
                  <p
                    class="m-0 flex items-center gap-2 text-xs font-semibold uppercase tracking-widest text-gray-600 dark:text-gray-400"
                  >
                    <MediaEqualizer
                      v-if="isCurrentTrack"
                      :playing="isPlaying"
                      class="text-primary"
                    />
                    {{ eyebrow }}
                  </p>

                  <div class="flex flex-col gap-1">
                    <h1
                      class="m-0 line-clamp-2 break-words text-3xl font-bold leading-tight tracking-tight text-gray-900 xl:text-4xl dark:text-gray-100"
                    >
                      {{ displayName }}
                    </h1>
                    <p class="m-0 truncate text-lg text-gray-600 dark:text-gray-400">
                      {{ track.artist || "Unknown artist" }}
                    </p>
                  </div>

                  <p
                    v-if="detailParts.length"
                    class="m-0 flex flex-wrap items-center gap-x-2 text-sm text-gray-600 dark:text-gray-400"
                  >
                    <template v-for="(part, index) in detailParts" :key="part">
                      <span v-if="index > 0" aria-hidden="true">·</span>
                      <span>{{ part }}</span>
                    </template>
                  </p>

                  <p
                    class="m-0 flex min-w-0 items-center gap-2 text-xs text-gray-600 dark:text-gray-400"
                  >
                    <span
                      v-if="track.duration !== null"
                      class="flex shrink-0 items-center gap-1 tabular-nums"
                    >
                      <UIcon name="mdi:clock-outline" class="w-3.5 h-3.5" />
                      {{ formatDuration(track.duration) }}
                    </span>
                    <span
                      class="shrink-0 rounded-md border border-gray-200/70 px-1.5 py-0.5 font-semibold text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
                      :title="track.mimeType"
                    >
                      {{ formatLabel }}
                    </span>
                    <span class="min-w-0 truncate" :title="track.fileName">{{
                      track.fileName
                    }}</span>
                  </p>

                  <div class="flex flex-wrap items-center gap-2 pt-2">
                    <UButton
                      :icon="playIcon"
                      :label="playLabel"
                      color="primary"
                      size="lg"
                      class="rounded-full px-5"
                      @click="handlePlay"
                    />
                    <UButton
                      icon="mdi:pencil-outline"
                      label="Edit details"
                      color="neutral"
                      variant="outline"
                      size="lg"
                      class="rounded-full"
                      @click="isEditing = true"
                    />
                  </div>
                </div>
              </div>

              <dl
                v-if="glance.length"
                class="m-0 grid grid-cols-3 gap-4 border-t border-gray-200/60 pt-5 lg:w-56 lg:shrink-0 lg:grid-cols-1 lg:gap-0 lg:divide-y lg:divide-gray-200/60 lg:border-t-0 lg:border-l lg:pt-0 lg:pl-8 dark:border-gray-700/60 lg:dark:divide-gray-700/60"
              >
                <div
                  v-for="item in glance"
                  :key="item.label"
                  class="flex min-w-0 flex-col gap-0.5 lg:py-3 lg:first:pt-0 lg:last:pb-0"
                >
                  <dt
                    class="text-xs font-semibold uppercase tracking-widest text-gray-600 dark:text-gray-400"
                  >
                    {{ item.label }}
                  </dt>
                  <dd class="m-0 flex min-w-0 items-center gap-2">
                    <span
                      class="h-2 w-2 shrink-0 rounded-full"
                      :style="{ backgroundColor: ink }"
                      aria-hidden="true"
                    />
                    <span
                      class="truncate text-base font-semibold text-gray-900 lg:text-lg dark:text-gray-100"
                      :title="item.value"
                    >
                      {{ item.value }}
                    </span>
                  </dd>
                  <dd class="m-0 truncate text-xs text-gray-600 dark:text-gray-400">
                    {{ item.hint }}
                  </dd>
                </div>
              </dl>
            </div>
          </section>

          <TrackTagsSection :file="track" />
        </div>

        <AudioAnalysisFilePanel
          v-if="!track.isVideo"
          :file-id="track.fileId"
          :accent="ink"
          class="mt-2"
        />
      </template>

      <!-- Deliberate exception to the shared modalUi preset: this modal sizes itself to the
           artwork (w-max) instead of a fixed preset width. Still solid, no glass classes. -->
      <UModal
        v-model:open="isArtworkModalOpen"
        :title="displayName"
        :description="track?.artist ?? undefined"
        :ui="{ content: 'w-max max-w-[calc(100vw-2rem)] sm:max-w-[calc(100vw-4rem)]' }"
      >
        <template #content="{ close }">
          <div class="overflow-hidden rounded-lg">
            <div class="relative">
              <img
                v-if="track && !thumbnailErrored"
                :src="thumbnailUrl"
                :alt="displayName"
                class="block max-h-[75dvh] w-auto max-w-full"
                @load="onThumbnailLoad"
                @error="onThumbnailError"
              />
              <div
                v-else
                class="flex h-64 w-64 items-center justify-center bg-gray-100 dark:bg-white/5"
              >
                <UIcon name="mdi:music-note" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
              </div>

              <UButton
                icon="mdi:close"
                color="neutral"
                variant="solid"
                size="sm"
                square
                aria-label="Close"
                class="absolute top-3 right-3 justify-center rounded-full bg-black/60 text-white hover:bg-black/80"
                @click="close"
              />
            </div>

            <!-- w-0 min-w-full: the caption matches the image width instead of widening the modal -->
            <div class="w-0 min-w-full px-4 py-3">
              <p class="m-0 truncate text-sm font-semibold text-gray-900 dark:text-gray-100">
                {{ displayName }}
              </p>
              <p v-if="track?.artist" class="m-0 truncate text-sm text-gray-600 dark:text-gray-400">
                {{ track.artist }}
              </p>
            </div>
          </div>
        </template>
      </UModal>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { storeToRefs } from "pinia";
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import AudioAnalysisFilePanel from "@/components/dashboard/integrations/audio-analysis/AudioAnalysisFilePanel.vue";
import MediaArtwork from "@/components/streaming/overview/MediaArtwork.vue";
import MediaEqualizer from "@/components/streaming/overview/MediaEqualizer.vue";
import TrackMetadataEditor from "@/components/streaming/TrackMetadataEditor.vue";
import TrackTagsSection from "@/components/streaming/TrackTagsSection.vue";
import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { useTheme } from "@/composables/useTheme";
import { useTrackAudioAnalysis } from "@/composables/useTrackAudioAnalysis";
import { getStreamingFile } from "@/queries/streaming";
import { usePlayerStore } from "@/stores/stream-player";
import { formatConfidence } from "@/utils/audio-taxonomy.utils";
import { coverInk, heroTint } from "@/utils/cover-color";
import { formatDuration } from "@/utils/date-formatters";
import { coverSeed, generatedCover } from "@/utils/generated-cover";

interface GlanceItem {
  label: string;
  value: string;
  hint: string;
}

const formatLabels: Record<string, string> = {
  "audio/aac": "AAC",
  "audio/flac": "FLAC",
  "audio/mp4": "M4A",
  "audio/mpeg": "MP3",
  "audio/ogg": "OGG",
  "audio/opus": "OPUS",
  "audio/wav": "WAV",
  "audio/x-flac": "FLAC",
  "audio/x-m4a": "M4A",
  "audio/x-wav": "WAV",
  "video/mp4": "MP4",
  "video/webm": "WEBM",
};

const route = useRoute();
const router = useRouter();
const { isDark } = useTheme();

const fileId = computed(() => route.params.fileId as string);

const trackQuery = useQuery(() => ({
  ...getStreamingFile(fileId.value),
  enabled: fileId.value.length > 0,
}));

const track = computed(() => trackQuery.data.value ?? null);
const displayName = computed(() => track.value?.title ?? track.value?.fileName ?? "");

const goBack = () => {
  const fallback = track.value?.isVideo ? "/streaming/videos" : "/streaming/music";
  router.push(fallback);
};

const isEditing = ref(false);
const isArtworkModalOpen = ref(false);

watch(fileId, () => {
  isEditing.value = false;
});

const { thumbnailUrl, thumbnailErrored, onThumbnailLoad, onThumbnailError } = useFileThumbnail(
  () => ({
    fileId: track.value?.fileId ?? "",
    versionId: track.value?.currentVersionId ?? "",
  }),
);

const artworkSeed = computed(() => (track.value ? coverSeed(track.value) : ""));
const coverColor = ref<string | null>(null);

const tint = computed(() => {
  if (!coverColor.value) return null;

  return heroTint(coverColor.value, isDark.value);
});

const ink = computed(() => {
  const fallback = generatedCover(artworkSeed.value).base;

  return coverInk(coverColor.value ?? fallback, isDark.value, fallback);
});

const playerStore = usePlayerStore();
const { activeFile, isPlaying } = storeToRefs(playerStore);

const isCurrentTrack = computed(
  () => track.value !== null && activeFile.value?.fileId === track.value.fileId,
);

const isCurrentlyPlaying = computed(() => isCurrentTrack.value && isPlaying.value);

const playIcon = computed(() => (isCurrentlyPlaying.value ? "mdi:pause" : "mdi:play"));
const playLabel = computed(() => (isCurrentlyPlaying.value ? "Pause" : "Play"));

const eyebrow = computed(() => {
  if (isCurrentlyPlaying.value) return "Now playing";
  if (isCurrentTrack.value) return "Paused";
  if (track.value?.isVideo) return "Video";

  return "Track";
});

const handlePlay = () => {
  if (!track.value) return;
  if (isCurrentTrack.value) playerStore.togglePlay();
  else playerStore.playNow([track.value]);
};

const detailParts = computed(() => {
  const current = track.value;
  if (!current) return [];

  // The genre field is written by audio analysis, so the glance already shows it when available.
  const genre = analysis.topGenre.value ? null : current.genre;

  return [current.album, current.year, genre].filter(
    (part): part is string => typeof part === "string" && part.trim().length > 0,
  );
});

const formatLabel = computed(() => {
  const mimeType = track.value?.mimeType ?? "";
  const known = formatLabels[mimeType];
  if (known) return known;

  const subtype = mimeType.split("/")[1] ?? mimeType;

  return subtype.replace(/^x-/u, "").toUpperCase();
});

const analysis = useTrackAudioAnalysis(fileId, () => track.value !== null && !track.value.isVideo);

const glance = computed<GlanceItem[]>(() => {
  const items: GlanceItem[] = [];
  const { topGenre, topMood, voice } = analysis;

  if (topGenre.value) {
    items.push({
      hint: `${topGenre.value.parent} · ${formatConfidence(topGenre.value.confidence)}`,
      label: "Genre",
      value: topGenre.value.name,
    });
  }

  if (topMood.value) {
    items.push({
      hint: `${formatConfidence(topMood.value.value)} confidence`,
      label: "Mood",
      value: topMood.value.name,
    });
  }

  if (voice.value) {
    items.push({
      hint: `${formatConfidence(voice.value.value)} confidence`,
      label: "Voice",
      value: voice.value.name,
    });
  }

  return items;
});
</script>

<style scoped>
.hero-glow {
  animation: hero-drift 24s ease-in-out infinite alternate;
}

@keyframes hero-drift {
  from {
    transform: translate(0, 0) scale(1);
  }

  to {
    transform: translate(6%, 8%) scale(1.08);
  }
}

@media (prefers-reduced-motion: reduce) {
  .hero-glow {
    animation: none;
  }
}
</style>
