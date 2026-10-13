import { defineQueryOptions } from "@pinia/colada";

import type { HomeRecentFilesSource } from "@/enums/home-widget";

import { fileApi } from "@/api/file";
import { statusApi } from "@/api/status";
import { DIRECTORY_QUERY_KEYS, favoriteDirectories } from "@/queries/directories";
import { FILES_QUERY_KEYS } from "@/queries/files";
import { STATUS_QUERY_KEYS } from "@/queries/status";
import { homeRecentFilesSearch } from "@/utils/home-file-widgets";

interface HomeWidgetSession {
  userId: string;
  epoch: number;
}

export const HOME_WIDGET_QUERY_KEYS = {
  foldersOwner: (userId: string) => [...DIRECTORY_QUERY_KEYS.favorites(userId, 0, 20), "home"],
  storageOwner: (userId: string) => [...STATUS_QUERY_KEYS.myStorage(), "home", userId],
  filesOwner: (userId: string) => [...FILES_QUERY_KEYS.root, "search-files", "home", userId],
};

export const homeFavoriteFolders = defineQueryOptions((session: HomeWidgetSession) => ({
  key: [...HOME_WIDGET_QUERY_KEYS.foldersOwner(session.userId), session.epoch],
  enabled: Boolean(session.userId),
  staleTime: 30_000,
  query: async (context) => ({
    ...session,
    value: await favoriteDirectories({ userId: session.userId }).query(context),
  }),
}));

export const homeStorage = defineQueryOptions((session: HomeWidgetSession) => ({
  key: [...HOME_WIDGET_QUERY_KEYS.storageOwner(session.userId), session.epoch],
  enabled: Boolean(session.userId),
  staleTime: 30_000,
  query: async () => ({ ...session, value: await statusApi.getMyStorage(true) }),
}));

export const homeRecentFiles = defineQueryOptions(
  (params: HomeWidgetSession & { source: HomeRecentFilesSource }) => ({
    key: [...HOME_WIDGET_QUERY_KEYS.filesOwner(params.userId), params.epoch, params.source],
    enabled: Boolean(params.userId),
    staleTime: 30_000,
    query: async () => ({
      ...params,
      value: await fileApi.searchFiles(homeRecentFilesSearch(params.source)),
    }),
  }),
);
