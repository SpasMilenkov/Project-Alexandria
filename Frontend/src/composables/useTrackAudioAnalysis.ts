import { useQuery } from "@pinia/colada";
import { type MaybeRefOrGetter, computed, toValue } from "vue";

import type { EnrichmentRowDto } from "@/api/audioAnalysis";

import { getFileAudioAnalysis } from "@/queries/audioAnalysis";
import { buildMoodEntries, parseGenreLabel, voicePole } from "@/utils/audio-taxonomy.utils";

interface GenrePrediction {
  label: string;
  score: number;
}

type MoodPayload = Record<string, Record<string, number>>;

const MAX_GENRES = 5;

const readGenrePredictions = (payload: Record<string, unknown>): GenrePrediction[] | null => {
  const { predictions } = payload;

  return Array.isArray(predictions) ? (predictions as GenrePrediction[]) : null;
};

const isMoodPayload = (payload: Record<string, unknown>): payload is MoodPayload => {
  const values = Object.values(payload);

  return (
    values.length > 0 &&
    values.every((value) => typeof value === "object" && value !== null && !Array.isArray(value))
  );
};

const flattenMoodPayload = (payload: MoodPayload): Record<string, number> =>
  Object.entries(payload).reduce<Record<string, number>>((flat, [axis, poles]) => {
    for (const [pole, value] of Object.entries(poles)) {
      flat[`${axis}:${pole}`] = value;
    }

    return flat;
  }, {});

const byNewest = (a: { createdAt: string }, b: { createdAt: string }) =>
  new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();

// Shared by the track hero and the analysis panel; both resolve to one cached query.
export const useTrackAudioAnalysis = (
  fileId: MaybeRefOrGetter<string>,
  enabled: MaybeRefOrGetter<boolean> = true,
) => {
  const { data, status, error, refresh } = useQuery(() => ({
    ...getFileAudioAnalysis(toValue(fileId)),
    enabled: toValue(enabled) && toValue(fileId).length > 0,
  }));

  const isPending = computed(() => status.value === "pending");

  const sortedEnrichments = computed(() => [...(data.value?.enrichments ?? [])].sort(byNewest));
  const latestAnalyzedAt = computed(() => sortedEnrichments.value[0]?.createdAt);

  const genreEnrichment = computed<EnrichmentRowDto | undefined>(() =>
    sortedEnrichments.value.find((row) => readGenrePredictions(row.payload) !== null),
  );

  const moodEnrichment = computed<EnrichmentRowDto | undefined>(() =>
    sortedEnrichments.value.find((row) => isMoodPayload(row.payload)),
  );

  const genrePredictions = computed(() => {
    const payload = genreEnrichment.value?.payload;
    const predictions = payload ? (readGenrePredictions(payload) ?? []) : [];

    return [...predictions]
      .sort((a, b) => b.score - a.score)
      .slice(0, MAX_GENRES)
      .map((prediction) => ({
        ...parseGenreLabel(prediction.label),
        confidence: prediction.score,
      }));
  });

  const topGenre = computed(() => genrePredictions.value[0]);

  const moodScores = computed(() => {
    const payload = moodEnrichment.value?.payload;

    if (!payload || !isMoodPayload(payload)) return {};

    return flattenMoodPayload(payload);
  });

  const moodEntries = computed(() =>
    moodEnrichment.value ? buildMoodEntries(moodScores.value) : [],
  );
  const topMood = computed(() => moodEntries.value[0]);
  const voice = computed(() => (moodEnrichment.value ? voicePole(moodScores.value) : null));

  const batches = computed(() => [...(data.value?.batches ?? [])].sort(byNewest));

  return {
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
  };
};
