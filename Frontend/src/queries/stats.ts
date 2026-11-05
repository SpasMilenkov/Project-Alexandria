import { defineQueryOptions } from "@pinia/colada";

import { listeningRankingsApi } from "@/api/listening-rankings";
import { statsApi } from "@/api/stats";

export const STATS_QUERY_KEYS = {
  root: ["stats"] as const,

  wrapped: (from?: string, to?: string, userId?: string) => [
    ...STATS_QUERY_KEYS.root,
    "wrapped",
    userId ?? null,
    from ?? null,
    to ?? null,
  ],

  summaries: (kind?: string, userId?: string) => [
    ...STATS_QUERY_KEYS.root,
    "summaries",
    kind ?? null,
    userId ?? null,
  ],

  summary: (query: GetSummaryQuery) => [
    ...STATS_QUERY_KEYS.root,
    "summary",
    query.kind ?? null,
    query.userId ?? null,
    query.from ?? null,
    query.to ?? null,
  ],

  topSongs: (from: string, limit: number, userId?: string) => [
    ...STATS_QUERY_KEYS.root,
    "top-songs",
    userId ?? null,
    from,
    limit,
  ],

  timeline: (from?: string, to?: string, userId?: string) => [
    ...STATS_QUERY_KEYS.root,
    "timeline",
    userId ?? null,
    from ?? null,
    to ?? null,
  ],
};

export interface GetWrappedQuery {
  userId?: string;
  from?: string;
  to?: string;
}

export const getWrapped = defineQueryOptions((query: GetWrappedQuery) => ({
  key: STATS_QUERY_KEYS.wrapped(query.from, query.to, query.userId),
  enabled: Boolean(query.userId),
  staleTime: 1000 * 60 * 5,
  query: () => statsApi.getWrapped(query.from, query.to),
}));

export interface ListSummariesQuery {
  userId?: string;
  kind: string;
}

export const listSummaries = defineQueryOptions((query: ListSummariesQuery) => ({
  key: STATS_QUERY_KEYS.summaries(query.kind, query.userId),
  enabled: Boolean(query.userId),
  staleTime: 1000 * 60 * 5,
  query: () => statsApi.listSummaries(query.kind),
}));

export interface GetSummaryQuery {
  userId?: string;
  kind: string;
  from?: string;
  to?: string;
}

export const getSummary = defineQueryOptions((query: GetSummaryQuery) => ({
  key: STATS_QUERY_KEYS.summary(query),
  enabled: Boolean(query.userId),
  staleTime: 1000 * 60 * 5,
  query: () => statsApi.getSummary(query.kind, query.from, query.to),
}));

export interface GetTimelineQuery {
  userId?: string;
  from?: string;
  to?: string;
}

export const getTimeline = defineQueryOptions((query: GetTimelineQuery) => ({
  key: STATS_QUERY_KEYS.timeline(query.from, query.to, query.userId),
  enabled: Boolean(query.userId),
  staleTime: 1000 * 60 * 5,
  query: () => statsApi.getTimeline(query.from, query.to),
}));

export interface GetTopSongsQuery {
  userId?: string;
  from: string;
  limit: number;
}

// The end boundary stays open so a client clock running ahead of the server cannot trip range validation.
export const getTopSongs = defineQueryOptions((query: GetTopSongsQuery) => ({
  key: STATS_QUERY_KEYS.topSongs(query.from, query.limit, query.userId),
  enabled: Boolean(query.userId),
  staleTime: 1000 * 60 * 5,
  query: () => listeningRankingsApi.getTopSongs({ from: query.from, limit: query.limit }),
}));
