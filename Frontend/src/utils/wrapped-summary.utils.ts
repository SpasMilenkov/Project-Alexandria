import type {
  OverviewSummaryDtoResponse,
  WrappedDeckResponse,
} from "@/api/stats";

export const SUMMARY_KIND_WRAPPED = "Wrapped";

export const summaryToWrappedDeck = (
  summary: OverviewSummaryDtoResponse,
): WrappedDeckResponse | null => {
  if (summary.payload.type !== "wrapped") return null;

  const payload = summary.payload;

  return {
    from: summary.periodStart,
    to: summary.periodEnd,
    deck: payload.deck,
    schemaVersion: summary.schemaVersion,
    recipeVersion: payload.recipeVersion,
    generatedAt: summary.generatedAt,
    visualIdentity: payload.visualIdentity,

    summary: {
      seconds: payload.summary.seconds,
      sessions: payload.summary.sessions,
      qualifiedPlayCount: payload.summary.qualifiedPlayCount,
      tracks: payload.summary.tracks,
      artists: payload.summary.artists,
      activeDays: payload.summary.activeDays,
      knownArtistShare: payload.summary.knownArtistShare,
      hasPriorHistory: payload.summary.hasPriorHistory,
      historyFrom: payload.summary.historyFrom ?? null,
    },
  };
};

export const summaryYear = (periodStart: string): number =>
  new Date(periodStart).getUTCFullYear();
