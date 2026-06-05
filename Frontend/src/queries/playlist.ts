import { defineQueryOptions } from "@pinia/colada";

import { playlistApi } from "@/api/playlist";

export const PLAYLIST_QUERY_KEYS = {
  root: ["playlists"] as const,
  list: (page: number, pageSize: number) => [...PLAYLIST_QUERY_KEYS.root, "list", page, pageSize],
  detail: (id: string) => [...PLAYLIST_QUERY_KEYS.root, "detail", id],
  cover: (id: string) => [...PLAYLIST_QUERY_KEYS.root, "cover", id],
};

export interface GetPlaylistsQuery {
  page: number;
  pageSize: number;
}

export const getPlaylists = defineQueryOptions((query: GetPlaylistsQuery) => ({
  key: PLAYLIST_QUERY_KEYS.list(query.page, query.pageSize),
  placeholderData: (prev) => prev,
  query: () => playlistApi.getAll(query.page, query.pageSize),
}));

export const getPlaylistById = defineQueryOptions((id: string) => ({
  key: PLAYLIST_QUERY_KEYS.detail(id),
  query: () => playlistApi.getById(id),
  staleTime: 30_000,
}));

export const getPlaylistCover = defineQueryOptions((id: string) => ({
  key: PLAYLIST_QUERY_KEYS.cover(id),
  query: () => playlistApi.getPlaylistCover(id),
}));
