<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, ref } from "vue";

import type { EnrichmentBatchAttemptDto, EnrichmentRowDto } from "@/api/audioAnalysis";

import { getFileAudioAnalysis } from "@/queries/audioAnalysis";
import {
  buildMoodEntries,
  formatConfidence,
  parseGenreLabel,
  voicePole,
} from "@/utils/audio-taxonomy.utils";
import { formatDate } from "@/utils/date-formatters";

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
}>();

const LOW_CONFIDENCE_THRESHOLD = 0.15;

const showAdvanced = ref(false);

const { data, status, error, refresh } = useQuery(() => ({
  ...getFileAudioAnalysis(props.fileId),
  enabled: props.enabled ?? true,
}));

const isLoading = computed(() => status.value === "pending");

const sortedEnrichments = computed(() =>
  [...(data.value?.enrichments ?? [])].sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
  ),
);

const genreEnrichment = computed<EnrichmentRowDto | undefined>(() =>
  sortedEnrichments.value.find((row) => isGenrePayload(row.payload)),
);

const moodEnrichment = computed<EnrichmentRowDto | undefined>(() =>
  sortedEnrichments.value.find((row) => isMoodPayload(row.payload)),
);

const otherEnrichments = computed(() =>
  sortedEnrichments.value.filter(
    (row) => row !== genreEnrichment.value && row !== moodEnrichment.value,
  ),
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
  Object.entries(payload).reduce<Record<string, number>>(
    (flat, [axis, poles]) => {
      for (const [pole, value] of Object.entries(poles)) {
        flat[`${axis}:${pole}`] = value;
      }
      return flat;
    },
    {},
  );

const moodScores = computed(() => {
  const payload = moodEnrichment.value?.payload as MoodPayload | undefined;
  return payload ? flattenMoodPayload(payload) : {};
});

const moodEntries = computed(() => buildMoodEntries(moodScores.value));

const topMood = computed(() => moodEntries.value[0]);
const restMoods = computed(() => moodEntries.value.slice(1));

const voice = computed(() => (moodEnrichment.value ? voicePole(moodScores.value) : null));

const voiceIcon = computed(() =>
  voice.value?.name === "Instrumental" ? "mdi:waveform" : "mdi:microphone-variant",
);

const sortedBatches = computed(() =>
  [...(data.value?.batches ?? [])].sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
  ),
);

const fileStatusMeta: Record<string, { dot: string; label: string }> = {
  Failed: { dot: "bg-red-500", label: "Failed" },
  MissingOutput: { dot: "bg-red-400", label: "Missing output" },
  Pending: { dot: "bg-amber-500", label: "Pending" },
  Succeeded: { dot: "bg-emerald-500", label: "Succeeded" },
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

const shortId = (id: string) => id.slice(0, 8).toUpperCase();

const rawPayload = (row: EnrichmentRowDto) => JSON.stringify(row.payload, null, 2);
</script>

<template>
  <div class="flex flex-col gap-6">
    <header class="flex items-start justify-end gap-4 flex-wrap">
      <div class="flex items-center gap-4">
        <label
          class="flex items-center gap-2 text-xs text-gray-600 dark:text-gray-400 cursor-pointer select-none"
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

    <section class="flex flex-col gap-3">
      <div class="flex items-center gap-2">
        <Icon icon="mdi:music-note" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Genre</h2>
        <span v-if="genreEnrichment" class="ml-auto text-xs text-gray-500 dark:text-gray-500">
          {{ formatDate(genreEnrichment.createdAt) }}
        </span>
      </div>

      <div
        v-if="isLoading"
        class="h-40 rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm animate-pulse"
      />

      <div
        v-else-if="!genreEnrichment || !topGenre"
        class="flex flex-col items-center justify-center gap-3 py-12 px-6 text-center rounded-2xl border border-dashed border-gray-200/70 dark:border-gray-700/70"
      >
        <Icon icon="mdi:music-note-off-outline" class="w-8 h-8 text-gray-400 dark:text-gray-600" />
        <div class="space-y-1 max-w-sm">
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No genre analysis yet</p>
          <p class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
            This file hasn't been through the genre model, or the result hasn't arrived yet.
          </p>
        </div>
      </div>

      <div
        v-else
        class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm px-5 py-4 flex flex-col gap-4"
      >
        <div class="flex flex-col gap-1.5">
          <div class="flex items-center gap-2">
            <span
              class="text-[11px] font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-500"
            >
              Top pick
            </span>
            <span
              class="px-2 py-0.5 rounded-full text-[11px] bg-black/5 dark:bg-white/5 text-gray-600 dark:text-gray-400"
            >
              {{ topGenre.parent }}
            </span>
          </div>
          <div class="flex items-center gap-3">
            <p class="text-lg font-semibold text-gray-900 dark:text-gray-100">
              {{ topGenre.name }}
            </p>
            <span
              class="ml-auto text-sm font-semibold text-gray-800 dark:text-gray-100 tabular-nums"
            >
              {{ formatConfidence(topGenre.confidence) }}
            </span>
          </div>
          <div class="w-full h-1.5 rounded-full bg-black/8 dark:bg-white/8 overflow-hidden">
            <div
              class="h-full rounded-full"
              :style="{
                backgroundColor: 'var(--ui-primary)',
                width: formatConfidence(topGenre.confidence),
              }"
            />
          </div>
          <p v-if="genreLowConfidence" class="text-xs text-gray-500 dark:text-gray-500">
            Low-confidence match, treat this as a rough guess rather than a firm tag.
          </p>
        </div>

        <div
          v-if="restGenres.length"
          class="flex flex-col gap-2.5 pt-1 border-t border-gray-200/60 dark:border-gray-700/60"
        >
          <div v-for="genre in restGenres" :key="genre.raw" class="flex items-center gap-3">
            <span
              class="px-2 py-0.5 rounded-full text-[11px] bg-black/5 dark:bg-white/5 text-gray-600 dark:text-gray-400 shrink-0"
            >
              {{ genre.parent }}
            </span>
            <span class="text-sm text-gray-700 dark:text-gray-300 truncate">{{ genre.name }}</span>
            <span class="ml-auto text-xs text-gray-500 dark:text-gray-500 tabular-nums shrink-0">
              {{ formatConfidence(genre.confidence) }}
            </span>
            <div class="w-12 h-1 rounded-full bg-black/8 dark:bg-white/8 overflow-hidden shrink-0">
              <div
                class="h-full rounded-full bg-gray-400 dark:bg-gray-500"
                :style="{ width: formatConfidence(genre.confidence) }"
              />
            </div>
          </div>
        </div>

        <p
          v-if="showAdvanced"
          class="text-[11px] text-gray-500 dark:text-gray-500 pt-1 border-t border-gray-200/60 dark:border-gray-700/60"
        >
          Model {{ genreEnrichment.analyzer }}-{{ genreEnrichment.version }} ·
          {{ genrePredictions.length }} predictions
        </p>

        <UCollapsible v-if="showAdvanced">
          <button
            class="flex items-center gap-1.5 text-xs text-gray-500 dark:text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
          >
            <Icon icon="mdi:code-json" class="w-3.5 h-3.5" />
            View raw payload
          </button>
          <template #content>
            <pre
              class="mt-2 text-[11px] font-mono text-gray-600 dark:text-gray-400 bg-black/5 dark:bg-white/5 rounded-lg p-3 overflow-auto max-h-48"
              >{{ rawPayload(genreEnrichment) }}</pre>
          </template>
        </UCollapsible>
      </div>
    </section>

    <section class="flex flex-col gap-3">
      <div class="flex items-center gap-2">
        <Icon icon="mdi:emoticon-outline" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Mood</h2>
        <span
          v-if="voice"
          class="flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] bg-black/5 dark:bg-white/5 text-gray-600 dark:text-gray-400"
        >
          <Icon :icon="voiceIcon" class="w-3 h-3" />
          {{ voice.name }}
        </span>
        <span v-if="moodEnrichment" class="ml-auto text-xs text-gray-500 dark:text-gray-500">
          {{ formatDate(moodEnrichment.createdAt) }}
        </span>
      </div>

      <div
        v-if="isLoading"
        class="h-40 rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm animate-pulse"
      />

      <div
        v-else-if="!moodEnrichment || !topMood"
        class="flex flex-col items-center justify-center gap-3 py-12 px-6 text-center rounded-2xl border border-dashed border-gray-200/70 dark:border-gray-700/70"
      >
        <Icon icon="mdi:emoticon-outline" class="w-8 h-8 text-gray-400 dark:text-gray-600" />
        <div class="space-y-1 max-w-sm">
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No mood analysis yet</p>
          <p class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
            This file hasn't been through the mood model, or the result hasn't arrived yet.
          </p>
        </div>
      </div>

      <div
        v-else
        class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm px-5 py-4 flex flex-col gap-2.5"
      >
        <div class="flex items-center gap-3">
          <span class="text-sm font-medium text-gray-900 dark:text-gray-100 w-24 shrink-0">
            {{ topMood.name }}
          </span>
          <div class="flex-1 h-1.5 rounded-full bg-black/8 dark:bg-white/8 overflow-hidden">
            <div
              class="h-full rounded-full"
              :style="{
                backgroundColor: 'var(--ui-primary)',
                width: formatConfidence(topMood.value),
              }"
            />
          </div>
          <span
            class="text-xs text-gray-500 dark:text-gray-500 tabular-nums w-9 text-right shrink-0"
          >
            {{ formatConfidence(topMood.value) }}
          </span>
        </div>
        <div v-for="mood in restMoods" :key="mood.key" class="flex items-center gap-3">
          <span class="text-sm text-gray-700 dark:text-gray-300 w-24 shrink-0">{{
            mood.name
          }}</span>
          <div class="flex-1 h-1.5 rounded-full bg-black/8 dark:bg-white/8 overflow-hidden">
            <div
              class="h-full rounded-full bg-gray-400 dark:bg-gray-500"
              :style="{ width: formatConfidence(mood.value) }"
            />
          </div>
          <span
            class="text-xs text-gray-500 dark:text-gray-500 tabular-nums w-9 text-right shrink-0"
          >
            {{ formatConfidence(mood.value) }}
          </span>
        </div>

        <p
          v-if="showAdvanced"
          class="text-[11px] text-gray-500 dark:text-gray-500 pt-2 border-t border-gray-200/60 dark:border-gray-700/60"
        >
          Model {{ moodEnrichment.analyzer }}-{{ moodEnrichment.version }}
        </p>

        <UCollapsible v-if="showAdvanced">
          <button
            class="flex items-center gap-1.5 text-xs text-gray-500 dark:text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
          >
            <Icon icon="mdi:code-json" class="w-3.5 h-3.5" />
            View raw payload
          </button>
          <template #content>
            <pre
              class="mt-2 text-[11px] font-mono text-gray-600 dark:text-gray-400 bg-black/5 dark:bg-white/5 rounded-lg p-3 overflow-auto max-h-48"
              >{{ rawPayload(moodEnrichment) }}</pre>
          </template>
        </UCollapsible>
      </div>
    </section>

    <section v-if="showAdvanced && otherEnrichments.length" class="flex flex-col gap-3">
      <div class="flex items-center gap-2">
        <Icon icon="mdi:puzzle-outline" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Other analyzers</h2>
      </div>
      <div
        class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm divide-y divide-gray-200/60 dark:divide-gray-700/60 overflow-hidden"
      >
        <div
          v-for="row in otherEnrichments"
          :key="`${row.analyzer}-${row.createdAt}`"
          class="px-5 py-3"
        >
          <div class="flex items-center gap-3">
            <p class="text-sm font-mono text-gray-800 dark:text-gray-100">
              {{ row.analyzer }}-{{ row.version }}
            </p>
            <span class="ml-auto text-xs text-gray-500 dark:text-gray-500">{{
              formatDate(row.createdAt)
            }}</span>
          </div>
          <pre
            class="mt-2 text-[11px] font-mono text-gray-600 dark:text-gray-400 bg-black/5 dark:bg-white/5 rounded-lg p-3 overflow-auto max-h-48"
            >{{ rawPayload(row) }}</pre>
        </div>
      </div>
    </section>

    <section class="flex flex-col gap-3">
      <div class="flex items-center gap-2">
        <Icon icon="mdi:history" class="w-4 h-4 text-gray-400 dark:text-gray-500" />
        <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Batch history</h2>
      </div>

      <div
        v-if="isLoading"
        class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm p-5 space-y-3 animate-pulse"
      >
        <div v-for="i in 3" :key="i" class="h-8 rounded-lg bg-black/5 dark:bg-white/5" />
      </div>

      <div
        v-else-if="!sortedBatches.length"
        class="flex flex-col items-center justify-center gap-3 py-12 px-6 text-center rounded-2xl border border-dashed border-gray-200/70 dark:border-gray-700/70"
      >
        <Icon icon="mdi:history" class="w-8 h-8 text-gray-400 dark:text-gray-600" />
        <div class="space-y-1 max-w-sm">
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No batch attempts yet</p>
          <p class="text-xs text-gray-600 dark:text-gray-400 leading-relaxed">
            Dispatch attempts for this file will appear here once it enters the queue.
          </p>
        </div>
      </div>

      <div
        v-else
        class="rounded-2xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm divide-y divide-gray-200/60 dark:divide-gray-700/60 overflow-hidden"
      >
        <div
          v-for="attempt in sortedBatches as EnrichmentBatchAttemptDto[]"
          :key="`${attempt.batchId}-${attempt.createdAt}`"
          class="px-5 py-3 flex items-center gap-3 flex-wrap"
        >
          <span
            class="text-xs font-semibold px-2.5 py-1 rounded-full"
            :class="[
              batchStatusMeta[attempt.batchStatus]?.bg ?? 'bg-gray-500/10',
              batchStatusMeta[attempt.batchStatus]?.text ?? 'text-gray-600 dark:text-gray-400',
            ]"
          >
            {{ batchStatusMeta[attempt.batchStatus]?.label ?? attempt.batchStatus }}
          </span>
          <span class="flex items-center gap-1.5 text-xs text-gray-600 dark:text-gray-400">
            <span
              class="w-2 h-2 rounded-full shrink-0"
              :class="fileStatusMeta[attempt.fileStatus]?.dot"
            />
            {{ fileStatusMeta[attempt.fileStatus]?.label ?? attempt.fileStatus }}
          </span>
          <span v-if="showAdvanced" class="text-xs font-mono text-gray-500 dark:text-gray-500">
            {{ attempt.batchId }}
          </span>
          <span v-else class="text-xs font-mono text-gray-500 dark:text-gray-500">
            {{ shortId(attempt.batchId) }}
          </span>
          <span class="ml-auto text-xs text-gray-500 dark:text-gray-500">
            {{ formatDate(attempt.createdAt) }}
          </span>
          <p v-if="attempt.errorDetail" class="w-full text-xs text-red-600 dark:text-red-400">
            {{ attempt.errorDetail }}
          </p>
        </div>
      </div>
    </section>
  </div>
</template>
