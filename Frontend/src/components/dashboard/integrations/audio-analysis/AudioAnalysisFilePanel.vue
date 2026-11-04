<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed, ref } from "vue";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { useTrackAudioAnalysis } from "@/composables/useTrackAudioAnalysis";
import { formatConfidence } from "@/features/audio-analysis/taxonomy";
import { formatDate } from "@/utils/date-formatters";

import AudioConfidenceBar from "./AudioConfidenceBar.vue";
import AudioMoodBalance from "./AudioMoodBalance.vue";
import AudioMoodRadar from "./AudioMoodRadar.vue";
import AudioRawPayload from "./AudioRawPayload.vue";

const { fileId, accent } = defineProps<{
  fileId: string;
  /** Mark color for the bars and the mood shape, usually derived from the cover art. */
  accent: string;
}>();

const LOW_CONFIDENCE_THRESHOLD = 0.15;
const BAR_STAGGER_MS = 80;

const DIVIDER_CLASS = "border-gray-200/60 dark:border-gray-700/60";
const EYEBROW_CLASS =
  "text-xs font-semibold uppercase tracking-widest text-gray-600 dark:text-gray-400";
const CHIP_CLASS =
  "rounded-full border border-gray-200/70 px-2 py-0.5 text-xs text-gray-600 dark:border-gray-700/70 dark:text-gray-400";
const BATCH_GRID_CLASS =
  "@3xl:grid @3xl:grid-cols-[140px_160px_minmax(0,1fr)_auto] @3xl:items-center @3xl:gap-4";

const {
  batches,
  error,
  genreEnrichment,
  genrePredictions,
  isPending,
  latestAnalyzedAt,
  moodEnrichment,
  moodEntries,
  refresh,
  topGenre,
  topMood,
  voice,
} = useTrackAudioAnalysis(() => fileId);

const showAdvanced = ref(false);
const isHistoryOpen = ref(false);

const restGenres = computed(() => genrePredictions.value.slice(1));

const isGenreLowConfidence = computed(
  () => (topGenre.value?.confidence ?? 0) < LOW_CONFIDENCE_THRESHOLD,
);

const voiceIcon = computed(() => {
  if (voice.value?.name === "Instrumental") return "lucide:audio-lines";

  return "mdi:microphone-variant";
});

const analyzedLabel = computed(() => {
  if (latestAnalyzedAt.value) return `Analyzed ${formatDate(latestAnalyzedAt.value).toLowerCase()}`;

  return "Genre and mood, read from the audio itself";
});

const fileStatusMeta: Record<string, { dot: string; label: string }> = {
  Cancelled: { dot: "bg-gray-400", label: "Cancelled" },
  CancellationRequested: { dot: "bg-orange-400", label: "Cancelling" },
  Failed: { dot: "bg-red-500", label: "Failed" },
  Partial: { dot: "bg-amber-400", label: "Partial" },
  Processing: { dot: "bg-blue-500", label: "Processing" },
  Queued: { dot: "bg-amber-500", label: "Queued" },
  Ready: { dot: "bg-emerald-500", label: "Ready" },
};

const batchStatusMeta: Record<string, { bg: string; label: string; text: string }> = {
  Completed: {
    bg: "bg-emerald-500/10",
    label: "Completed",
    text: "text-emerald-600 dark:text-emerald-400",
  },
  Dispatched: {
    bg: "bg-amber-500/10",
    label: "In progress",
    text: "text-amber-600 dark:text-amber-400",
  },
  TimedOut: {
    bg: "bg-red-500/10",
    label: "Timed out",
    text: "text-red-600 dark:text-red-400",
  },
};

const fileMeta = (fileStatus: string) =>
  fileStatusMeta[fileStatus] ?? { dot: "bg-gray-400", label: fileStatus };

const batchMeta = (batchStatus: string) =>
  batchStatusMeta[batchStatus] ?? {
    bg: "bg-gray-500/10",
    label: batchStatus,
    text: "text-gray-600 dark:text-gray-400",
  };

const batchReference = (id: string) => {
  if (showAdvanced.value) return id;

  return id.slice(0, 8).toUpperCase();
};

const describeModel = (row: { analyzer: string; version: string } | undefined, suffix = "") => {
  if (!row) return "Not run";

  return `${row.analyzer}-${row.version}${suffix}`;
};

const modelDetails = computed(() => [
  {
    label: "Genre model",
    name: describeModel(genreEnrichment.value, ` · ${genrePredictions.value.length} predictions`),
    payload: genreEnrichment.value?.payload,
  },
  {
    label: "Mood model",
    name: describeModel(moodEnrichment.value),
    payload: moodEnrichment.value?.payload,
  },
]);

const referenceHeading = computed(() => (showAdvanced.value ? "Batch id" : "Reference"));

const batchRows = computed(() =>
  batches.value.map((attempt) => ({
    batch: batchMeta(attempt.batchStatus),
    createdAt: attempt.createdAt,
    errorDetail: attempt.errorDetail,
    file: fileMeta(attempt.fileStatus),
    id: attempt.batchId,
    key: `${attempt.batchId}-${attempt.createdAt}`,
  })),
);

const historySummary = computed(() => {
  const [latest] = batchRows.value;

  if (!latest) return "No dispatch attempts yet";

  const count = batchRows.value.length;
  const attempts = count === 1 ? "1 attempt" : `${count} attempts`;

  return `${attempts} · last ${latest.batch.label.toLowerCase()} ${formatDate(latest.createdAt).toLowerCase()}`;
});
</script>

<template>
  <section
    aria-label="Audio analysis"
    class="@container frosted-glass glass-surface overflow-hidden rounded-2xl border border-gray-200/70 dark:border-gray-700/70"
  >
    <header
      class="flex flex-wrap items-center gap-x-4 gap-y-2 border-b px-5 py-4"
      :class="DIVIDER_CLASS"
    >
      <div class="min-w-0">
        <h2 class="text-base font-semibold text-gray-900 dark:text-gray-100">Audio analysis</h2>
        <p class="mt-0.5 text-xs text-gray-600 dark:text-gray-400">{{ analyzedLabel }}</p>
      </div>

      <div class="ml-auto flex items-center gap-2">
        <label
          class="flex cursor-pointer select-none items-center gap-2 text-xs text-gray-600 dark:text-gray-400"
        >
          <USwitch v-model="showAdvanced" size="sm" />
          Advanced
        </label>

        <UButton
          icon="i-mdi-refresh"
          color="neutral"
          variant="ghost"
          size="sm"
          square
          aria-label="Refresh analysis"
          :loading="isPending"
          @click="refresh()"
        />
      </div>
    </header>

    <div v-if="error" class="px-5 pt-4">
      <UAlert
        color="error"
        variant="soft"
        title="Could not load the analysis"
        description="The server may be offline, or this file hasn't been analyzed yet."
        icon="i-mdi-connection"
      />
    </div>

    <div v-if="isPending" class="flex h-72 items-center justify-center">
      <BlockSpinner :size="24" aria-label="Loading audio analysis" class="text-gray-500" />
    </div>

    <div v-else class="grid grid-cols-1 @3xl:grid-cols-2">
      <div class="flex min-w-0 flex-col gap-5 p-5 @3xl:col-start-1 @3xl:row-start-1 @3xl:p-6">
        <div class="flex items-center gap-2">
          <Icon icon="mdi:music-note" class="w-4 h-4 text-gray-600 dark:text-gray-400" />
          <h3 class="text-sm font-semibold text-gray-900 dark:text-gray-100">Genre</h3>
          <span
            v-if="genreEnrichment"
            class="ml-auto text-xs font-medium text-gray-600 dark:text-gray-400"
          >
            {{ formatDate(genreEnrichment.createdAt) }}
          </span>
        </div>

        <div
          v-if="!genreEnrichment || !topGenre"
          class="flex flex-col items-center gap-2 py-10 text-center"
        >
          <Icon
            icon="mdi:music-note-off-outline"
            class="w-12 h-12 text-gray-400 dark:text-gray-600"
          />
          <p class="text-sm font-semibold text-gray-900 dark:text-gray-100">
            No genre analysis yet
          </p>
          <p class="max-w-xs text-xs text-gray-600 dark:text-gray-400">
            This file hasn't been through the genre model, or the result hasn't arrived yet.
          </p>
        </div>

        <template v-else>
          <div class="flex flex-col gap-2">
            <div class="flex items-center gap-2">
              <span :class="EYEBROW_CLASS">Best match</span>
              <span :class="CHIP_CLASS">{{ topGenre.parent }}</span>
            </div>

            <div class="flex items-baseline gap-4">
              <p
                class="min-w-0 break-words text-2xl font-bold tracking-tight text-gray-900 @3xl:text-3xl dark:text-gray-100"
              >
                {{ topGenre.name }}
              </p>
              <span
                class="ml-auto shrink-0 text-lg font-semibold tabular-nums text-gray-900 dark:text-gray-100"
              >
                {{ formatConfidence(topGenre.confidence) }}
              </span>
            </div>

            <AudioConfidenceBar :value="topGenre.confidence" :color="accent" />

            <p v-if="isGenreLowConfidence" class="text-xs text-gray-600 dark:text-gray-400">
              Low-confidence match, treat this as a rough guess rather than a firm tag.
            </p>
          </div>

          <ol
            v-if="restGenres.length"
            class="flex flex-col gap-3 border-t pt-4"
            :class="DIVIDER_CLASS"
          >
            <li
              v-for="(genre, index) in restGenres"
              :key="genre.raw"
              class="grid grid-cols-[1.5rem_minmax(0,1fr)_auto] items-center gap-x-3 gap-y-1.5"
            >
              <span class="text-xs tabular-nums text-gray-600 dark:text-gray-400">
                {{ String(index + 2).padStart(2, "0") }}
              </span>

              <span class="flex min-w-0 items-center gap-2">
                <span class="truncate text-sm text-gray-900 dark:text-gray-100">{{
                  genre.name
                }}</span>
                <span class="hidden truncate text-xs text-gray-600 @md:inline dark:text-gray-400">
                  {{ genre.parent }}
                </span>
              </span>

              <span class="text-xs font-medium tabular-nums text-gray-900 dark:text-gray-100">
                {{ formatConfidence(genre.confidence) }}
              </span>

              <AudioConfidenceBar
                :value="genre.confidence"
                :color="accent"
                :delay="(index + 1) * BAR_STAGGER_MS"
                size="sm"
                class="col-start-2 col-end-4"
              />
            </li>
          </ol>
        </template>
      </div>

      <div
        class="flex min-w-0 flex-col gap-5 border-t p-5 @3xl:col-start-2 @3xl:row-span-2 @3xl:row-start-1 @3xl:border-t-0 @3xl:border-l @3xl:p-6"
        :class="DIVIDER_CLASS"
      >
        <div class="flex items-center gap-2">
          <Icon icon="mdi:emoticon-outline" class="w-4 h-4 text-gray-600 dark:text-gray-400" />
          <h3 class="text-sm font-semibold text-gray-900 dark:text-gray-100">Mood profile</h3>
          <span v-if="voice" :class="[CHIP_CLASS, 'flex items-center gap-1']">
            <Icon :icon="voiceIcon" class="w-3 h-3" />
            {{ voice.name }}
          </span>
          <span
            v-if="moodEnrichment"
            class="ml-auto text-xs font-medium text-gray-600 dark:text-gray-400"
          >
            {{ formatDate(moodEnrichment.createdAt) }}
          </span>
        </div>

        <div
          v-if="!moodEnrichment || !topMood"
          class="flex flex-col items-center gap-2 py-10 text-center"
        >
          <Icon icon="mdi:emoticon-outline" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
          <p class="text-sm font-semibold text-gray-900 dark:text-gray-100">No mood analysis yet</p>
          <p class="max-w-xs text-xs text-gray-600 dark:text-gray-400">
            This file hasn't been through the mood model, or the result hasn't arrived yet.
          </p>
        </div>

        <template v-else>
          <AudioMoodRadar :entries="moodEntries" :color="accent" class="my-auto" />
        </template>
      </div>

      <div
        v-if="moodEnrichment && topMood"
        class="min-w-0 border-t p-5 @3xl:col-start-1 @3xl:row-start-2 @3xl:p-6"
        :class="DIVIDER_CLASS"
      >
        <AudioMoodBalance :entries="moodEntries" :color="accent" />
      </div>
    </div>

    <div
      v-if="showAdvanced && (genreEnrichment || moodEnrichment)"
      class="grid grid-cols-1 divide-y divide-gray-200/60 border-t @3xl:grid-cols-2 @3xl:divide-x @3xl:divide-y-0 dark:divide-gray-700/60"
      :class="DIVIDER_CLASS"
    >
      <div
        v-for="model in modelDetails"
        :key="model.label"
        class="flex min-w-0 flex-col gap-2 p-5 @3xl:px-6"
      >
        <span :class="EYEBROW_CLASS">{{ model.label }}</span>
        <span
          class="truncate font-mono text-xs text-gray-600 dark:text-gray-400"
          :title="model.name"
        >
          {{ model.name }}
        </span>

        <AudioRawPayload v-if="model.payload" :payload="model.payload" class="h-72" />
        <div
          v-else
          class="flex h-72 items-center justify-center rounded-lg border border-dashed border-gray-200/70 text-xs text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
        >
          No result from this model yet
        </div>
      </div>
    </div>

    <UCollapsible
      v-model:open="isHistoryOpen"
      :disabled="!batchRows.length"
      class="border-t"
      :class="DIVIDER_CLASS"
    >
      <button
        type="button"
        class="flex w-full items-center gap-3 px-5 py-3.5 text-left disabled:cursor-default"
      >
        <Icon icon="mdi:history" class="w-4 h-4 shrink-0 text-gray-600 dark:text-gray-400" />
        <span class="text-sm font-medium text-gray-900 dark:text-gray-100">Processing history</span>
        <span class="min-w-0 truncate text-xs text-gray-600 dark:text-gray-400">
          {{ historySummary }}
        </span>
        <Icon
          v-if="batchRows.length"
          icon="mdi:chevron-down"
          class="ml-auto w-4 h-4 shrink-0 text-gray-600 transition-transform duration-200 motion-reduce:transition-none dark:text-gray-400"
          :class="{ 'rotate-180': isHistoryOpen }"
        />
      </button>

      <template #content>
        <div class="border-t" :class="DIVIDER_CLASS">
          <div :class="['hidden border-b px-5 py-2.5', DIVIDER_CLASS, BATCH_GRID_CLASS]">
            <span :class="EYEBROW_CLASS">Batch</span>
            <span :class="EYEBROW_CLASS">File</span>
            <span :class="EYEBROW_CLASS">{{ referenceHeading }}</span>
            <span :class="[EYEBROW_CLASS, 'justify-self-end']">When</span>
          </div>

          <div class="divide-y divide-gray-200/60 dark:divide-gray-700/60">
            <div
              v-for="row in batchRows"
              :key="row.key"
              :class="['flex flex-wrap items-center gap-3 px-5 py-3', BATCH_GRID_CLASS]"
            >
              <span
                class="justify-self-start whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-semibold"
                :class="[row.batch.bg, row.batch.text]"
              >
                {{ row.batch.label }}
              </span>

              <span class="flex items-center gap-1.5 text-xs text-gray-600 dark:text-gray-400">
                <span class="h-2 w-2 shrink-0 rounded-full" :class="row.file.dot" />
                {{ row.file.label }}
              </span>

              <span class="min-w-0 truncate font-mono text-xs text-gray-600 dark:text-gray-400">
                {{ batchReference(row.id) }}
              </span>

              <span class="ml-auto text-xs font-medium text-gray-600 dark:text-gray-400">
                {{ formatDate(row.createdAt) }}
              </span>

              <p
                v-if="row.errorDetail"
                class="w-full text-xs text-red-600 dark:text-red-400 @3xl:col-span-full"
              >
                {{ row.errorDetail }}
              </p>
            </div>
          </div>
        </div>
      </template>
    </UCollapsible>
  </section>
</template>
