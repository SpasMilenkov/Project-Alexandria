<template>
  <div class="px-2 md:px-6 py-5">
    <!-- Back -->
    <UButton
      icon="mdi:arrow-left"
      label="Library"
      color="neutral"
      variant="ghost"
      size="sm"
      class="mb-4 -ml-1"
      @click="goBack"
    />

    <!-- Loading -->
    <div v-if="trackQuery.status.value === 'pending'" class="flex justify-center py-16">
      <UIcon name="mdi:loading" class="w-6 h-6 animate-spin text-muted" />
    </div>

    <UAlert
      v-else-if="trackQuery.status.value === 'error'"
      icon="mdi:alert-circle-outline"
      color="error"
      variant="subtle"
      title="Failed to load track"
    />

    <!-- Empty: unknown id or no Ready transcode -->
    <div v-else-if="!track" class="flex flex-col items-center gap-2.5 py-20 text-center">
      <UIcon name="mdi:music-note-off-outline" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
      <p class="text-base font-medium text-gray-900 dark:text-gray-100 m-0">Track not found</p>
      <p class="text-sm text-gray-600 dark:text-gray-400 m-0">
        It may have been deleted, or it has no completed transcode yet.
      </p>
    </div>

    <template v-else>
      <div class="flex flex-col gap-6">
        <!-- Hero header -->
        <div
          class="relative rounded-2xl overflow-hidden border border-gray-200/70 dark:border-gray-700/70"
        >
          <div class="relative flex items-center gap-5 p-5 md:p-6 frosted-glass glass-surface">
            <!-- Artwork -->
            <div
              class="w-32 md:w-48 shrink-0 aspect-square rounded-xl overflow-hidden shadow-lg bg-gray-100 dark:bg-white/5"
            >
              <img
                v-if="!thumbnailErrored"
                :src="thumbnailUrl"
                :alt="displayName"
                class="w-full h-full object-cover"
                @load="onThumbnailLoad"
                @error="onThumbnailError"
              />
              <div v-else class="w-full h-full flex items-center justify-center">
                <UIcon name="mdi:music-note" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
              </div>
            </div>

            <!-- Titles -->
            <div class="flex flex-col gap-1.5 min-w-0 flex-1">
              <h1 class="text-2xl font-semibold text-gray-900 dark:text-gray-100 m-0 truncate">
                {{ displayName }}
              </h1>
              <p v-if="track.artist" class="text-sm text-gray-600 dark:text-gray-400 m-0 truncate">
                {{ track.artist }}
              </p>
              <div v-if="track.album" class="flex flex-wrap gap-1.5">
                <UBadge color="neutral" variant="subtle" :label="track.album" />
              </div>
            </div>

            <!-- Play / pause: resumes the active track in place, otherwise starts it -->
            <UButton
              :icon="playIcon"
              color="primary"
              size="xl"
              class="shrink-0 rounded-full"
              :aria-label="isCurrentTrack ? 'Pause' : 'Play'"
              @click="handlePlay"
            />
          </div>
        </div>

        <!-- Tags: front and center, right after the hero -->
        <TrackTagsSection :file="track" />

        <!-- Body: editor + facts rail -->
        <div class="grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_20rem] gap-6 items-start">
          <TrackMetadataEditor :file="track" />

          <UCard class="frosted-glass glass-surface" :ui="{ body: 'p-4' }">
            <template #header>
              <span class="font-semibold text-sm text-gray-700 dark:text-gray-300">Facts</span>
            </template>
            <div class="flex flex-col gap-3">
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-500 mb-0.5">File</div>
                <div class="text-sm font-medium truncate">{{ track.fileName }}</div>
              </div>
              <div v-if="track.duration !== null">
                <div class="text-xs text-gray-500 dark:text-gray-500 mb-0.5">Duration</div>
                <div class="text-sm font-medium tabular-nums">
                  {{ formatDuration(track.duration) }}
                </div>
              </div>
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-500 mb-0.5">Format</div>
                <div class="text-sm font-medium">{{ track.mimeType }}</div>
              </div>
            </div>
          </UCard>
        </div>

        <!-- Audio analysis: full width, no cap -->
        <div v-if="!track.isVideo" class="flex flex-col gap-3">
          <h2
            class="text-sm font-semibold tracking-widest uppercase text-gray-500 dark:text-gray-400 m-0"
          >
            Audio analysis
          </h2>
          <AudioAnalysisFilePanel :file-id="track.fileId" :enabled="true" />
        </div>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { storeToRefs } from "pinia";
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";

import AudioAnalysisFilePanel from "@/components/dashboard/integrations/audio-analysis/AudioAnalysisFilePanel.vue";
import TrackMetadataEditor from "@/components/streaming/TrackMetadataEditor.vue";
import TrackTagsSection from "@/components/streaming/TrackTagsSection.vue";
import { useFileThumbnail } from "@/composables/useFileThumbnail";
import { getStreamingFile } from "@/queries/streaming";
import { usePlayerStore } from "@/stores/stream-player";
import { formatDuration } from "@/utils/date-formatters";

const route = useRoute();
const router = useRouter();

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

const { thumbnailUrl, thumbnailErrored, onThumbnailLoad, onThumbnailError } = useFileThumbnail(
  () => ({
    fileId: track.value?.fileId ?? "",
    versionId: track.value?.currentVersionId ?? "",
  }),
);

const playerStore = usePlayerStore();
const { activeFile, isPlaying } = storeToRefs(playerStore);

const isCurrentTrack = computed(
  () => track.value !== null && activeFile.value?.fileId === track.value.fileId,
);

const playIcon = computed(() => {
  if (isCurrentTrack.value && isPlaying.value) return "mdi:pause";
  return "mdi:play";
});

const handlePlay = () => {
  if (!track.value) return;
  if (isCurrentTrack.value) playerStore.togglePlay();
  else playerStore.playNow([track.value]);
};
</script>
