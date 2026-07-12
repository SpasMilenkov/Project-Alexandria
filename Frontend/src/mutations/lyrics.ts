import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import type { ChangeProviderSchema, UploadLyricsSchema } from "@/schemas/lyrics";

import { lyricsApi } from "@/api/lyrics";
import { LYRICS_QUERY_KEYS } from "@/queries/lyrics";

export const uploadLyrics = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (req: UploadLyricsSchema) => lyricsApi.uploadLyrics(req),
    onSettled(_data, _error, vars) {
      queryCache.invalidateQueries({
        key: LYRICS_QUERY_KEYS.byJob(vars.jobId),
      });
    },
  });
});

export const deleteLyrics = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (lyricsId: string) => lyricsApi.deleteLyrics(lyricsId),
    onSettled() {
      queryCache.invalidateQueries({ key: LYRICS_QUERY_KEYS.root });
    },
  });
});

export const changeProvider = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (req: ChangeProviderSchema) => lyricsApi.changeProvider(req),
    onSettled() {
      queryCache.invalidateQueries({ key: LYRICS_QUERY_KEYS.root });
    },
  });
});

export const requeueLyrics = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: (jobId: string) => lyricsApi.requeueLyrics(jobId),
    onSettled(_data, _error, jobId) {
      queryCache.invalidateQueries({
        key: LYRICS_QUERY_KEYS.byJob(jobId),
      });
    },
  });
});
