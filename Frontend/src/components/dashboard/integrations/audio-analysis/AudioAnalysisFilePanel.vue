<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";

import type { EnrichmentRowDto } from "@/api/audioAnalysis";

import { getFileAudioAnalysis } from "@/queries/audioAnalysis";
import { type MoodSide, buildMoodPairs } from "@/utils/audio-mood-pairs.utils";
import {
  buildMoodEntries,
  formatConfidence,
  parseGenreLabel,
  voicePole,
} from "@/utils/audio-taxonomy.utils";
import { formatDate } from "@/utils/date-formatters";

import AudioConfidenceBar from "./AudioConfidenceBar.vue";
import AudioRawPayload from "./AudioRawPayload.vue";

interface GenrePrediction {
  label: string;
  score: number;
}

interface GenrePayload {
  predictions: GenrePrediction[];
}

type MoodPayload = Record<string, Record<string, number>>;

const isGenrePayload = (payload: Record<string, unknown>): payload is GenrePayload =>
  Array.isArray((payload as GenrePayload).predictions);

const isMoodPayload = (payload: Record<string, unknown>): payload is MoodPayload => {
  const values = Object.values(payload);
  return (
    values.length > 0 &&
    values.every((value) => typeof value === "object" && value !== null && !Array.isArray(value))
  );
};

const props = defineProps<{
  fileId: string;
  fileName?: string;
  enabled?: boolean;
  showHeading?: boolean;
}>();

const LOW_CONFIDENCE_THRESHOLD = 0.15;
const ACCENT_THRESHOLD = 0.5;

const CARD_CLASS =
  "rounded-2xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface";
const EMPTY_CLASS =
  "flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-gray-200/70 px-6 py-12 text-center dark:border-gray-700/70";
const EYEBROW_CLASS =
  "text-[11px] font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-500";
const CHIP_CLASS =
  "rounded-full bg-black/5 px-2 py-0.5 text-[11px] text-gray-600 dark:bg-white/5 dark:text-gray-400";
// One grid template shared by the batch table header and rows. Below the
// container breakpoint the rows stay as wrapping flex lines.
const BATCH_GRID_CLASS =
  "@3xl:grid @3xl:grid-cols-[140px_160px_minmax(0,1fr)_auto] @3xl:items-center @3xl:gap-4 @3xl:px-6";

const showAdvanced = ref(false);

const { data, status, error, refresh } = useQuery(() => ({
  ...getFileAudioAnalysis(props.fileId),
  enabled: props.enabled ?? true,
}));

const isLoading = computed(() => status.value === "pending");

const byNewest = (a: { createdAt: string }, b: { createdAt: string }) =>
  new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();

const sortedEnrichments = computed(() => [...(data.value?.enrichments ?? [])].sort(byNewest));

const latestAnalyzedAt = computed(() => sortedEnrichments.value[0]?.createdAt);

const genreEnrichment = computed<EnrichmentRowDto | undefined>(() =>
  sortedEnrichments.value.find((row) => isGenrePayload(row.payload)),
);

const moodEnrichment = computed<EnrichmentRowDto | undefined>(() =>
  sortedEnrichments.value.find((row) => isMoodPayload(row.payload)),
);

const genrePredictions = computed(() => {
  const payload = genreEnrichment.value?.payload as GenrePayload | undefined;
  const predictions = payload?.predictions ?? [];
  return [...predictions]
    .sort((a, b) => b.score - a.score)
    .slice(0, 5)
    .map((prediction) => ({
      ...parseGenreLabel(prediction.label),
      confidence: prediction.score,
    }));
});

const topGenre = computed(() => genrePredictions.value[0]);
const restGenres = computed(() => genrePredictions.value.slice(1));
const genreLowConfidence = computed(
  () => (topGenre.value?.confidence ?? 0) < LOW_CONFIDENCE_THRESHOLD,
);

const flattenMoodPayload = (payload: MoodPayload): Record<string, number> =>
  Object.entries(payload).reduce<Record<string, number>>((flat, [axis, poles]) => {
    for (const [pole, value] of Object.entries(poles)) {
      flat[`${axis}:${pole}`] = value;
    }
    return flat;
  }, {});

const moodScores = computed(() => {
  const payload = moodEnrichment.value?.payload as MoodPayload | undefined;
  return payload ? flattenMoodPayload(payload) : {};
});

const moodEntries = computed(() => buildMoodEntries(moodScores.value));
const topMood = computed(() => moodEntries.value[0]);

const moodLayout = computed(() => buildMoodPairs(moodEntries.value));
const hasMoodPairs = computed(() => moodLayout.value.pairs.length > 0);

const voice = computed(() => (moodEnrichment.value ? voicePole(moodScores.value) : null));

const voiceIcon = computed(() =>
  voice.value?.name === "Instrumental" ? "mdi:waveform" : "mdi:microphone-variant",
);

const showSummary = computed(() => !isLoading.value && (!!topGenre.value || !!topMood.value));
const showModelDetails = computed(
  () => showAdvanced.value && (!!genreEnrichment.value || !!moodEnrichment.value),
);

const isStrong = (value: number) => value >= ACCENT_THRESHOLD;
const leads = (pair: { leading: MoodSide }, side: MoodSide) => pair.leading === side;

const poleNameClass = (emphasised: boolean) =>
  emphasised ? "font-medium text-gray-900 dark:text-gray-100" : "text-gray-600 dark:text-gray-400";

const poleValueClass = (emphasised: boolean) =>
  emphasised ? "text-gray-600 dark:text-gray-400" : "text-gray-500 dark:text-gray-500";

const rankedNameClass = (index: number) =>
  index === 0 ? "font-medium text-gray-900 dark:text-gray-100" : "text-gray-700 dark:text-gray-300";

const fileStatusMeta: Record<string, { dot: string; label: string }> = {
  Queued: { dot: "bg-amber-500", label: "Queued" },
  Processing: { dot: "bg-blue-500", label: "Processing" },
  Partial: { dot: "bg-amber-400", label: "Partial" },
  Ready: { dot: "bg-emerald-500", label: "Ready" },
  Failed: { dot: "bg-red-500", label: "Failed" },
  Cancelled: { dot: "bg-gray-400", label: "Cancelled" },
  CancellationRequested: { dot: "bg-orange-400", label: "Cancelling" },
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

const fileMeta = (status: string) =>
  fileStatusMeta[status] ?? { dot: "bg-gray-400", label: status };

const batchMeta = (status: string) =>
  batchStatusMeta[status] ?? {
    bg: "bg-gray-500/10",
    label: status,
    text: "text-gray-600 dark:text-gray-400",
  };

const shortId = (id: string) => id.slice(0, 8).toUpperCase();

const batchReference = (id: string) => (showAdvanced.value ? id : shortId(id));

const referenceHeading = computed(() => (showAdvanced.value ? "Batch id" : "Reference"));

const batchRows = computed(() =>
  [...(data.value?.batches ?? [])].sort(byNewest).map((attempt) => ({
    key: `${attempt.batchId}-${attempt.createdAt}`,
    id: attempt.batchId,
    createdAt: attempt.createdAt,
    errorDetail: attempt.errorDetail,
    batch: batchMeta(attempt.batchStatus),
    file: fileMeta(attempt.fileStatus),
  })),
);
</script>

<template>
  <div class="@container">
    <div class="flex flex-col gap-6">
      <header class="flex flex-wrap items-end justify-end gap-4 @3xl:justify-between">
        <div v-if="showHeading" class="hidden @3xl:block">
          <h1 class="text-xl font-semibold text-gray-900 dark:text-gray-100">Audio analysis</h1>
          <p class="mt-0.5 text-xs text-gray-600 dark:text-gray-400">
            Genre, mood and processing history for this file
          </p>
        </div>
        <div class="ml-auto flex items-center gap-4">
          <label
            class="flex cursor-pointer select-none items-center gap-2 text-xs text-gray-600 dark:text-gray-400"
          >
            <USwitch v-model="showAdvanced" size="sm" />
            Advanced
          </label>
          <UButton
            size="sm"
            color="neutral"
            variant="outline"
            icon="i-mdi-refresh"
            :loading="isLoading"
            @click="refresh()"
          >
            Refresh
          </UButton>
        </div>
      </header>

      <UAlert
        v-if="error"
        color="error"
        variant="soft"
        title="Could not load the analysis"
        description="The server may be offline, or this file hasn't been analyzed yet."
        icon="i-mdi-connection"
      />

      <div v-if="showSummary" class="hidden gap-6 @5xl:grid @5xl:grid-cols-3">
        <div v-if="topGenre" :class="[CARD_CLASS, 'flex flex-col gap-1 px-6 py-4']">
          <span :class="EYEBROW_CLASS">Top genre</span>
          <p class="text-xl font-semibold text-gray-900 dark:text-gray-100">{{ topGenre.name }}</p>
          <span class="text-xs text-gray-600 dark:text-gray-400">
            {{ topGenre.parent }} · {{ formatConfidence(topGenre.confidence) }} confidence
          </span>
        </div>
        <div v-if="topMood" :class="[CARD_CLASS, 'flex flex-col gap-1 px-6 py-4']">
          <span :class="EYEBROW_CLASS">Top mood</span>
          <p class="text-xl font-semibold text-gray-900 dark:text-gray-100">{{ topMood.name }}</p>
          <span class="text-xs text-gray-600 dark:text-gray-400">
            {{ formatConfidence(topMood.value) }} confidence
          </span>
        </div>
        <div v-if="voice" :class="[CARD_CLASS, 'flex flex-col gap-1 px-6 py-4']">
          <span :class="EYEBROW_CLASS">Voice</span>
          <p class="text-xl font-semibold text-gray-900 dark:text-gray-100">{{ voice.name }}</p>
          <span v-if="latestAnalyzedAt" class="text-xs text-gray-600 dark:text-gray-400">
            Last analyzed {{ formatDate(latestAnalyzedAt) }}
          </span>
        </div>
      </div>

      <div
        class="grid grid-cols-1 items-stretch gap-6 @3xl:grid-cols-2 @5xl:grid-cols-[minmax(0,7fr)_minmax(0,5fr)]"
      >
        <section class="flex min-w-0 flex-col gap-3">
          <div class="flex items-center gap-2">
            <Icon icon="mdi:music-note" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
            <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Genre</h2>
            <span v-if="genreEnrichment" class="ml-auto text-xs text-gray-500 dark:text-gray-500">
              {{ formatDate(genreEnrichment.createdAt) }}
            </span>
          </div>

          <div v-if="isLoading" :class="[CARD_CLASS, 'min-h-40 grow animate-pulse']" />

          <div v-else-if="!genreEnrichment || !topGenre" :class="[EMPTY_CLASS, 'grow']">
            <Icon
              icon="mdi:music-note-off-outline"
              class="w-8 h-8 text-gray-400 dark:text-gray-600"
            />
            <div class="max-w-sm space-y-1">
              <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
                No genre analysis yet
              </p>
              <p class="text-xs leading-relaxed text-gray-600 dark:text-gray-400">
                This file hasn't been through the genre model, or the result hasn't arrived yet.
              </p>
            </div>
          </div>

          <div
            v-else
            :class="[CARD_CLASS, 'flex grow flex-col gap-4 px-5 py-4 @3xl:px-6 @3xl:py-5']"
          >
            <div class="flex flex-col gap-1.5">
              <div class="flex items-center gap-2">
                <span :class="EYEBROW_CLASS">Top pick</span>
                <span :class="CHIP_CLASS">{{ topGenre.parent }}</span>
              </div>
              <div class="flex items-center gap-3">
                <p class="text-lg font-semibold text-gray-900 dark:text-gray-100 @3xl:text-2xl">
                  {{ topGenre.name }}
                </p>
                <span
                  class="ml-auto text-sm font-semibold tabular-nums text-gray-800 dark:text-gray-100"
                >
                  {{ formatConfidence(topGenre.confidence) }}
                </span>
              </div>
              <AudioConfidenceBar :value="topGenre.confidence" accent />
              <p v-if="genreLowConfidence" class="text-xs text-gray-500 dark:text-gray-500">
                Low-confidence match, treat this as a rough guess rather than a firm tag.
              </p>
            </div>

            <div
              v-if="restGenres.length"
              class="flex grow flex-col justify-around gap-2.5 border-t border-gray-200/60 pt-3 dark:border-gray-700/60"
            >
              <div v-for="genre in restGenres" :key="genre.raw" class="flex items-center gap-3">
                <span :class="[CHIP_CLASS, 'shrink-0']">{{ genre.parent }}</span>
                <span class="truncate text-sm text-gray-700 dark:text-gray-300">
                  {{ genre.name }}
                </span>
                <span
                  class="ml-auto shrink-0 text-xs tabular-nums text-gray-500 dark:text-gray-500"
                >
                  {{ formatConfidence(genre.confidence) }}
                </span>
                <AudioConfidenceBar
                  :value="genre.confidence"
                  size="sm"
                  class="w-12 shrink-0 @3xl:w-24"
                />
              </div>
            </div>

            <div v-if="showAdvanced" class="flex flex-col gap-4 @3xl:hidden">
              <p
                class="border-t border-gray-200/60 pt-1 text-[11px] text-gray-500 dark:border-gray-700/60 dark:text-gray-500"
              >
                Model {{ genreEnrichment.analyzer }}-{{ genreEnrichment.version }} ·
                {{ genrePredictions.length }} predictions
              </p>
              <AudioRawPayload :payload="genreEnrichment.payload" />
            </div>
          </div>
        </section>

        <section class="flex min-w-0 flex-col gap-3">
          <div class="flex items-center gap-2">
            <Icon icon="mdi:emoticon-outline" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
            <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Mood</h2>
            <span v-if="voice" :class="[CHIP_CLASS, 'flex items-center gap-1']">
              <Icon :icon="voiceIcon" class="w-3 h-3" />
              {{ voice.name }}
            </span>
            <span v-if="moodEnrichment" class="ml-auto text-xs text-gray-500 dark:text-gray-500">
              {{ formatDate(moodEnrichment.createdAt) }}
            </span>
          </div>

          <div v-if="isLoading" :class="[CARD_CLASS, 'min-h-40 grow animate-pulse']" />

          <div v-else-if="!moodEnrichment || !topMood" :class="[EMPTY_CLASS, 'grow']">
            <Icon icon="mdi:emoticon-outline" class="w-8 h-8 text-gray-400 dark:text-gray-600" />
            <div class="max-w-sm space-y-1">
              <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
                No mood analysis yet
              </p>
              <p class="text-xs leading-relaxed text-gray-600 dark:text-gray-400">
                This file hasn't been through the mood model, or the result hasn't arrived yet.
              </p>
            </div>
          </div>

          <div
            v-else
            :class="[CARD_CLASS, 'flex grow flex-col gap-4 px-5 py-4 @3xl:px-6 @3xl:py-5']"
          >
            <div class="flex flex-col gap-2.5" :class="{ '@3xl:hidden': hasMoodPairs }">
              <div
                v-for="(mood, index) in moodEntries"
                :key="mood.key"
                class="flex items-center gap-3"
              >
                <span class="w-24 shrink-0 text-sm" :class="rankedNameClass(index)">
                  {{ mood.name }}
                </span>
                <AudioConfidenceBar :value="mood.value" :accent="index === 0" class="flex-1" />
                <span
                  class="w-9 shrink-0 text-right text-xs tabular-nums text-gray-500 dark:text-gray-500"
                >
                  {{ formatConfidence(mood.value) }}
                </span>
              </div>
            </div>

            <div v-if="hasMoodPairs" class="hidden grow flex-col justify-between gap-5 @3xl:flex">
              <div v-for="pair in moodLayout.pairs" :key="pair.key" class="flex flex-col gap-1.5">
                <div class="flex items-baseline justify-between gap-3">
                  <span class="flex items-baseline gap-1.5">
                    <span class="text-sm" :class="poleNameClass(leads(pair, 'left'))">
                      {{ pair.left.name }}
                    </span>
                    <span class="text-xs tabular-nums" :class="poleValueClass(leads(pair, 'left'))">
                      {{ formatConfidence(pair.left.value) }}
                    </span>
                  </span>
                  <span class="flex items-baseline gap-1.5">
                    <span
                      class="text-xs tabular-nums"
                      :class="poleValueClass(leads(pair, 'right'))"
                    >
                      {{ formatConfidence(pair.right.value) }}
                    </span>
                    <span class="text-sm" :class="poleNameClass(leads(pair, 'right'))">
                      {{ pair.right.name }}
                    </span>
                  </span>
                </div>
                <div class="grid grid-cols-2 gap-1">
                  <AudioConfidenceBar
                    :value="pair.left.value"
                    align="end"
                    :accent="leads(pair, 'left') && isStrong(pair.left.value)"
                  />
                  <AudioConfidenceBar
                    :value="pair.right.value"
                    :accent="leads(pair, 'right') && isStrong(pair.right.value)"
                  />
                </div>
              </div>

              <div
                v-if="moodLayout.singles.length"
                class="grid grid-cols-2 gap-x-6 gap-y-5 border-t border-gray-200/60 pt-4 dark:border-gray-700/60"
              >
                <div
                  v-for="mood in moodLayout.singles"
                  :key="mood.key"
                  class="flex flex-col gap-1.5"
                >
                  <div class="flex items-baseline justify-between gap-3">
                    <span class="text-sm" :class="poleNameClass(isStrong(mood.value))">
                      {{ mood.name }}
                    </span>
                    <span
                      class="text-xs tabular-nums"
                      :class="poleValueClass(isStrong(mood.value))"
                    >
                      {{ formatConfidence(mood.value) }}
                    </span>
                  </div>
                  <AudioConfidenceBar :value="mood.value" :accent="isStrong(mood.value)" />
                </div>
              </div>
            </div>

            <div v-if="showAdvanced" class="flex flex-col gap-4 @3xl:hidden">
              <p
                class="border-t border-gray-200/60 pt-2 text-[11px] text-gray-500 dark:border-gray-700/60 dark:text-gray-500"
              >
                Model {{ moodEnrichment.analyzer }}-{{ moodEnrichment.version }}
              </p>
              <AudioRawPayload :payload="moodEnrichment.payload" />
            </div>
          </div>
        </section>
      </div>

      <section class="flex flex-col gap-3">
        <div class="flex items-center gap-2">
          <Icon icon="mdi:history" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
          <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Batch history</h2>
        </div>

        <div v-if="isLoading" :class="[CARD_CLASS, 'animate-pulse space-y-3 p-5']">
          <div v-for="i in 3" :key="i" class="h-8 rounded-lg bg-black/5 dark:bg-white/5" />
        </div>

        <div v-else-if="!batchRows.length" :class="EMPTY_CLASS">
          <Icon icon="mdi:history" class="w-8 h-8 text-gray-400 dark:text-gray-600" />
          <div class="max-w-sm space-y-1">
            <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
              No batch attempts yet
            </p>
            <p class="text-xs leading-relaxed text-gray-600 dark:text-gray-400">
              Dispatch attempts for this file will appear here once it enters the queue.
            </p>
          </div>
        </div>

        <div v-else :class="[CARD_CLASS, 'overflow-hidden']">
          <div
            :class="[
              'hidden border-b border-gray-200/60 py-2.5 dark:border-gray-700/60',
              BATCH_GRID_CLASS,
            ]"
          >
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
              <span class="min-w-0 truncate font-mono text-xs text-gray-500 dark:text-gray-500">
                {{ batchReference(row.id) }}
              </span>
              <span class="ml-auto text-xs text-gray-500 dark:text-gray-500">
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
      </section>

      <section v-if="showModelDetails" class="hidden flex-col gap-3 @3xl:flex">
        <div class="flex items-center gap-2">
          <Icon icon="mdi:chip" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
          <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Model details</h2>
        </div>
        <div
          :class="[
            CARD_CLASS,
            'grid grid-cols-2 divide-x divide-gray-200/60 overflow-hidden dark:divide-gray-700/60',
          ]"
        >
          <div v-if="genreEnrichment" class="flex min-w-0 flex-col gap-2 px-6 py-4">
            <span :class="EYEBROW_CLASS">Genre model</span>
            <span class="font-mono text-xs text-gray-500 dark:text-gray-500">
              {{ genreEnrichment.analyzer }}-{{ genreEnrichment.version }} ·
              {{ genrePredictions.length }} predictions
            </span>
            <AudioRawPayload :payload="genreEnrichment.payload" />
          </div>
          <div v-if="moodEnrichment" class="flex min-w-0 flex-col gap-2 px-6 py-4">
            <span :class="EYEBROW_CLASS">Mood model</span>
            <span class="font-mono text-xs text-gray-500 dark:text-gray-500">
              {{ moodEnrichment.analyzer }}-{{ moodEnrichment.version }}
            </span>
            <AudioRawPayload :payload="moodEnrichment.payload" />
          </div>
        </div>
      </section>
    </div>
  </div>
</template>
