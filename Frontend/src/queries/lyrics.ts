import { defineQueryOptions } from "@pinia/colada";

import { lyricsApi, type LyricsDto } from "@/api/lyrics";
import { LyricsStatus } from "@/enums";

export const LYRICS_QUERY_KEYS = {
  root: ["lyrics"] as const,
  byJob: (jobId: string) => [...LYRICS_QUERY_KEYS.root, "by-job", jobId],
};

export const getLyrics = defineQueryOptions((jobId: string) => ({
  enabled: Boolean(jobId),
  key: LYRICS_QUERY_KEYS.byJob(jobId),
  query: () => lyricsApi.getLyrics(jobId),
}));

export const lyricsPollInterval = (data: LyricsDto | undefined): number =>
  data?.status === LyricsStatus.PendingFetch ? 5_000 : 30_000;
