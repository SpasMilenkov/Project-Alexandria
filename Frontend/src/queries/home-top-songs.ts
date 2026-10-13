import { defineQueryOptions } from "@pinia/colada";

import { type TopSongsQuery, listeningRankingsApi } from "@/api/listening-rankings";

export interface HomeTopSongsParams extends TopSongsQuery {
  userId: string;
  epoch: number;
  enabled?: boolean;
}

export const HOME_TOP_SONGS_QUERY_KEYS = {
  owner: (userId: string) => ["home", "top-songs", userId],
};

export const homeTopSongs = defineQueryOptions((params: HomeTopSongsParams) => ({
  key: [
    ...HOME_TOP_SONGS_QUERY_KEYS.owner(params.userId),
    params.epoch,
    params.from,
    params.to,
    params.limit,
  ],
  enabled: Boolean(params.userId) && params.enabled !== false,
  staleTime: 30_000,
  query: async () => ({
    ...params,
    value: await listeningRankingsApi.getTopSongs({
      from: params.from,
      to: params.to,
      limit: params.limit,
    }),
  }),
}));
