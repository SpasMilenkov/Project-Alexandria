import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import {
  type AddPlaylistItemRequest,
  type CreatePlaylistRequest,
  type ReorderPlaylistItemsRequest,
  type UpdatePlaylistRequest,
  playlistApi,
} from "@/api/playlist";
import { PLAYLIST_QUERY_KEYS } from "@/queries/playlist";

export const createPlaylist = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (req: CreatePlaylistRequest) => playlistApi.create(req),
    onSettled() {
      queryCache.invalidateQueries({ key: PLAYLIST_QUERY_KEYS.root });
    },
  });
});

export const updatePlaylist = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ id, req }: { id: string; req: UpdatePlaylistRequest }) =>
      playlistApi.update(id, req),
    onSettled(_data, _error, vars) {
      queryCache.invalidateQueries({ key: PLAYLIST_QUERY_KEYS.detail(vars.id) });
      // name, cover, and itemCount visible in list cards can change
      queryCache.invalidateQueries({ key: [...PLAYLIST_QUERY_KEYS.root, "list"] });
    },
  });
});

export const deletePlaylist = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (id: string) => playlistApi.delete(id),
    onSettled() {
      queryCache.invalidateQueries({ key: PLAYLIST_QUERY_KEYS.root });
    },
  });
});

export const addPlaylistItem = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ playlistId, req }: { playlistId: string; req: AddPlaylistItemRequest }) =>
      playlistApi.addItem(playlistId, req),
    onSettled(_data, _error, vars) {
      queryCache.invalidateQueries({ key: PLAYLIST_QUERY_KEYS.detail(vars.playlistId) });
      // itemCount on the list card changes
      queryCache.invalidateQueries({ key: [...PLAYLIST_QUERY_KEYS.root, "list"] });
    },
  });
});

export const removePlaylistItem = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ playlistId, itemId }: { playlistId: string; itemId: string }) =>
      playlistApi.removeItem(playlistId, itemId),
    onSettled(_data, _error, vars) {
      queryCache.invalidateQueries({ key: PLAYLIST_QUERY_KEYS.detail(vars.playlistId) });
      queryCache.invalidateQueries({ key: [...PLAYLIST_QUERY_KEYS.root, "list"] });
    },
  });
});

export const reorderPlaylistItems = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ playlistId, req }: { playlistId: string; req: ReorderPlaylistItemsRequest }) =>
      playlistApi.reorderItems(playlistId, req),
    onSettled(_data, _error, vars) {
      // only the detail carries ordered items, list cards are unaffected
      queryCache.invalidateQueries({ key: PLAYLIST_QUERY_KEYS.detail(vars.playlistId) });
    },
  });
});
