import { defineQueryOptions } from "@pinia/colada";

import { audioAnalysisApi } from "@/api/audioAnalysis";

export const INTEGRATIONS_QUERY_KEYS = {
  batches: () => [...INTEGRATIONS_QUERY_KEYS.root, "batches"],
  duration: () => [...INTEGRATIONS_QUERY_KEYS.root, "duration"],
  failureRate: (from: string, to: string, bucket: string) => [
    ...INTEGRATIONS_QUERY_KEYS.root,
    "failure-rate",
    from,
    to,
    bucket,
  ],
  file: (fileId: string) => [...INTEGRATIONS_QUERY_KEYS.root, "file", fileId],
  queueDepth: () => [...INTEGRATIONS_QUERY_KEYS.root, "queue-depth"],
  root: ["audio-analysis"] as const,
  stuck: () => [...INTEGRATIONS_QUERY_KEYS.root, "stuck"],
  volumeByHour: (from: string, to: string) => [
    ...INTEGRATIONS_QUERY_KEYS.root,
    "volume-by-hour",
    from,
    to,
  ],
};

export const queueDepth = defineQueryOptions(() => ({
  key: INTEGRATIONS_QUERY_KEYS.queueDepth(),
  query: () => audioAnalysisApi.getQueueDepth(),
  // Live-ish numbers, cheap to refetch on the admin page.
  staleTime: 5000,
  refetchInterval: 30000,
}));

export const stuck = defineQueryOptions(() => ({
  key: INTEGRATIONS_QUERY_KEYS.stuck(),
  query: () => audioAnalysisApi.getStuck(),
  staleTime: 5000,
  refetchInterval: 30000,
}));

export const failureRate = defineQueryOptions(
  (range: { from: string; to: string; bucket: "hour" | "day" }) => ({
    key: INTEGRATIONS_QUERY_KEYS.failureRate(range.from, range.to, range.bucket),
    query: () => audioAnalysisApi.getFailureRate(range.from, range.to, range.bucket),
    staleTime: 15000,
  }),
);

export const duration = defineQueryOptions(() => ({
  key: INTEGRATIONS_QUERY_KEYS.duration(),
  query: () => audioAnalysisApi.getDuration(),
  staleTime: 15000,
}));

export const batches = defineQueryOptions((limit?: number) => ({
  key: INTEGRATIONS_QUERY_KEYS.batches(),
  query: () => audioAnalysisApi.getBatches(limit),
  staleTime: 10000,
}));

export const volumeByHour = defineQueryOptions((range: { from: string; to: string }) => ({
  key: INTEGRATIONS_QUERY_KEYS.volumeByHour(range.from, range.to),
  query: () => audioAnalysisApi.getVolumeByHour(range.from, range.to),
  staleTime: 15000,
}));

export const getFileAudioAnalysis = defineQueryOptions((fileId: string) => ({
  key: INTEGRATIONS_QUERY_KEYS.file(fileId),
  query: () => audioAnalysisApi.getFileAudioAnalysis(fileId),
}));
