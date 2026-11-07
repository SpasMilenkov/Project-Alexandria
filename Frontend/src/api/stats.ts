import type {
  WrappedDurationCategory,
  WrappedDurationRelationship,
  WrappedDurationTier,
  WrappedRhythmEvidence,
  WrappedTimeScene,
} from "@/enums/wrapped-story";

import { api } from "./client";

export interface CardVisualSeedResponse {
  colorKey: string;
  saturation: string;
  contrast: string;
  density: number;
  fillLevel: number;
  shapeGrammar: string;
}

export interface WrappedCardEntryResponse {
  rank: number;
  title: string;
  subtitle?: string | null;
  date?: string | null;
  entityId?: string | null;
  artist?: string | null;
  seconds?: number;
  plays?: number;
  share?: number;
}

export interface WrappedPeriodPoint {
  date: string;
  seconds: number;
  plays: number;
  leader?: string | null;
  leaderKey?: string | null;
}

export interface WrappedComparison {
  key: string;
  name: string;
  category: WrappedDurationCategory;
  tier: WrappedDurationTier;
  durationMinutes: number;
  durationRangeMinutes?: number[] | null;
  durationLabel: string;
  exact: boolean;
  relationship: WrappedDurationRelationship;
  description?: string | null;
  copy: string;
  catalogVersion: string;
}

export interface WrappedCardFacts {
  seconds: number;
  count: number;
  share: number;
  baselineSeconds: number;
  from?: string | null;
  to?: string | null;
  detail?: string | null;
  weights: number[];
  series: WrappedPeriodPoint[];
  comparison?: WrappedComparison | null;
  comparisonAlternatives?: WrappedComparison[];

  rhythm?: {
    scene: WrappedTimeScene | null;
    evidence: WrappedRhythmEvidence;
    share: number;
    windowSeconds: number[];
  } | null;
}

export interface WrappedCardResponse {
  id?: string;
  type: number;
  headline: string;
  subline?: string | null;
  entries: WrappedCardEntryResponse[];
  visual: CardVisualSeedResponse;
  facts?: WrappedCardFacts;
}

export interface CuratedDeckResponse {
  cards: WrappedCardResponse[];
}

export interface WrappedDeckResponse {
  from: string;
  to: string;
  deck: CuratedDeckResponse;
  schemaVersion?: number;
  recipeVersion?: number;
  generatedAt?: string;
  visualIdentity?: string;

  summary?: {
    seconds: number;
    sessions: number;
    qualifiedPlayCount: number;
    tracks: number;
    artists: number;
    activeDays: number;
    knownArtistShare: number;
    hasPriorHistory: boolean;
    historyFrom?: string | null;
  };
}

// Stored recap summaries mirroring Backend/Alexandria.Dto/OverviewSummaries.
// The payload carries a "type" discriminator; enums arrive numeric on the wire,
// matching the live Wrapped contract above.

export interface WrappedPayloadSummary {
  seconds: number;
  sessions: number;
  qualifiedPlayCount: number;
  tracks: number;
  artists: number;
  activeDays: number;
  knownArtistShare: number;
  hasPriorHistory: boolean;
  historyFrom?: string | null;
}

export interface WrappedPayloadResponse {
  type: "wrapped";
  summary: WrappedPayloadSummary;
  deck: CuratedDeckResponse;
  visualIdentity: string;
  recipeVersion: number;
  catalogVersion: string;
}

export type SummaryPayloadResponse = WrappedPayloadResponse;

export interface OverviewSummaryDtoResponse {
  id: string;
  kind: number;
  periodStart: string;
  periodEnd: string;
  generatedAt: string;
  finalizedAt?: string | null;
  isFinal: boolean;
  schemaVersion: number;
  payload: SummaryPayloadResponse;
}

export interface OverviewSummaryHeaderDtoResponse {
  id: string;
  kind: number;
  periodStart: string;
  periodEnd: string;
  generatedAt: string;
  finalizedAt?: string | null;
  isFinal: boolean;
  schemaVersion: number;
}

export interface TimelineDayTop {
  title: string;
  artist?: string | null;
  seconds: number;
}

export interface TimelineDayPoint {
  date: string;
  seconds: number;
  plays: number;
  sessionCount: number;
  top: TimelineDayTop[];
}

export interface TimelineMonthPoint {
  date: string;
  seconds: number;
  plays: number;
  sessionCount: number;
}

export interface TimelineResponse {
  from: string;
  to: string;
  days: TimelineDayPoint[];
  months: TimelineMonthPoint[];
  totalSeconds: number;
  sessionCount: number;
  qualifiedPlayCount: number;
  activeDays: number;
}

// API

export const statsApi = {
  getWrapped: (from?: string, to?: string): Promise<WrappedDeckResponse> =>
    api.get<WrappedDeckResponse>("/stats/wrapped", {
      params: { from, to },
    }),

  listSummaries: (
    kind: string,
    from?: string,
    to?: string,
  ): Promise<OverviewSummaryHeaderDtoResponse[]> =>
    api.get<OverviewSummaryHeaderDtoResponse[]>("/stats/summaries", {
      params: { kind, from, to },
    }),

  getSummary: (kind: string, from?: string, to?: string): Promise<OverviewSummaryDtoResponse> =>
    api.get<OverviewSummaryDtoResponse>(`/stats/summaries/${kind}`, {
      params: { from, to },
    }),

  finalizeSummary: (kind: string, from: string, to: string): Promise<OverviewSummaryDtoResponse> =>
    api.post<OverviewSummaryDtoResponse>(`/stats/summaries/${kind}/finalize`, { from, to }),

  getTimeline: (from?: string, to?: string): Promise<TimelineResponse> =>
    api.get<TimelineResponse>("/stats/timeline", {
      params: { from, to },
    }),
};
