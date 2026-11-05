import { useQuery } from "@pinia/colada";
import { type MaybeRefOrGetter, computed, toValue } from "vue";

import { HomeRecapPeriod } from "@/enums/home-widget";
import { yearBounds } from "@/features/wrapped/listening/year";
import { wrappedMonth, wrappedMonthRange } from "@/features/wrapped/month";
import { SUMMARY_KIND_WRAPPED } from "@/features/wrapped/summary";
import { getTimeline, listSummaries } from "@/queries/stats";

export const useListeningHistoryEmpty = (userId: MaybeRefOrGetter<string | undefined>) => {
  // Reaching back to last month keeps a January visitor's December recap visible;
  // for the rest of the year this shares the listening hero's cache entry.
  const yearStart = yearBounds(new Date().getUTCFullYear()).from;
  const previousMonthStart = wrappedMonthRange(wrappedMonth(HomeRecapPeriod.PreviousMonth)).from;
  const recentFrom = previousMonthStart < yearStart ? previousMonthStart : yearStart;

  const recentTimeline = useQuery(() => getTimeline({ from: recentFrom, userId: toValue(userId) }));

  const summaries = useQuery(() =>
    listSummaries({ kind: SUMMARY_KIND_WRAPPED, userId: toValue(userId) }),
  );

  // Only a confirmed empty history counts; loading or failed queries keep the regular sections.
  const listeningEmpty = computed(() => {
    const timeline = recentTimeline.data.value;
    const archived = summaries.data.value;

    if (!timeline || !archived) return false;

    return timeline.totalSeconds <= 0 && timeline.days.length === 0 && archived.length === 0;
  });

  return { listeningEmpty };
};
