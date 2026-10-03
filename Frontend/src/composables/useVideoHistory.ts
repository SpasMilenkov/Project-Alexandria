import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { StreamHistoryResponse } from "@/api/streaming";

import { getHistory } from "@/queries/streaming";

export const VIDEO_HISTORY_PAGE_SIZE = 100;

export const isWatchedEntry = (entry: StreamHistoryResponse | undefined): boolean =>
  (entry?.timesCompleted ?? 0) > 0;

export const progressPercentOf = (
  entry: StreamHistoryResponse | undefined,
  durationSeconds: number | null,
): number | null => {
  if (!entry) return null;
  if (!durationSeconds || durationSeconds <= 0) return null;
  if (isWatchedEntry(entry)) return null;
  const percent = (entry.positionSeconds / durationSeconds) * 100;
  if (!(percent > 0) || percent >= 100) return null;
  return Math.min(99, Math.max(1, Math.round(percent)));
};

const byLastAccessedDesc = (a: StreamHistoryResponse, b: StreamHistoryResponse): number =>
  new Date(b.lastAccessedAt).getTime() - new Date(a.lastAccessedAt).getTime();

export const useVideoHistory = (enabled: () => boolean = () => true) => {
  const {
    data,
    isLoading,
    error,
    refresh,
  } = useQuery(() => ({
    ...getHistory({ currentPage: 1, pageSize: VIDEO_HISTORY_PAGE_SIZE }),
    enabled: enabled(),
  }));

  const historyItems = computed<StreamHistoryResponse[]>(() => data.value?.items ?? []);

  const historyByFileId = computed(() => {
    const map = new Map<string, StreamHistoryResponse>();
    for (const entry of historyItems.value) map.set(entry.fileId, entry);
    return map;
  });

  const recentEntries = computed<StreamHistoryResponse[]>(() =>
    [...historyItems.value].sort(byLastAccessedDesc),
  );

  return {
    historyByFileId,
    historyError: error,
    historyItems,
    historyLoading: isLoading,
    recentEntries,
    refreshHistory: refresh,
  };
};
