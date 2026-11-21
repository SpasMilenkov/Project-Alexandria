import { type QueryCache, defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import {
  type CreateNoteRequest,
  type FileContentSaveResult,
  type RenameNoteRequest,
  type SaveNoteContentRequest,
  notesApi,
} from "@/api/notes";
import { DIRECTORY_QUERY_KEYS } from "@/queries/directories";
import { FILES_QUERY_KEYS } from "@/queries/files";
import { NOTES_QUERY_KEYS } from "@/queries/notes";

// A new version changes the file's size and dates in every list that shows it, and the subtree
// sizes of the folders above it. Searches can match the new text.
const invalidateAfterWrite = (queryCache: QueryCache, saved: FileContentSaveResult) => {
  queryCache.invalidateQueries({ exact: true, key: FILES_QUERY_KEYS.getFile(saved.fileId) });
  queryCache.invalidateQueries({ key: [...FILES_QUERY_KEYS.root, "versions-for-file", saved.fileId] });
  queryCache.invalidateQueries({ key: [...FILES_QUERY_KEYS.root, "search-files"] });

  const listing = saved.directoryId
    ? [...FILES_QUERY_KEYS.root, "sub-files", saved.directoryId]
    : [...FILES_QUERY_KEYS.root, "root-sub-files"];

  queryCache.invalidateQueries({ key: listing });
  queryCache.invalidateQueries({ key: DIRECTORY_QUERY_KEYS.root });
};

export const saveNoteContent = defineMutation(() => {
  const queryCache = useQueryCache();

  return useMutation({
    mutation: (request: SaveNoteContentRequest) => notesApi.saveContent(request),
    onSuccess(saved, request) {
      if (saved.unchanged) return;

      // The editor already holds this text, so reopening the version needs no request.
      queryCache.setQueryData(NOTES_QUERY_KEYS.versionText(saved.versionId), request.content);
      invalidateAfterWrite(queryCache, saved);
    },
  });
});

// A rename changes the name in every list that shows the file, and what name searches find.
export const renameNote = defineMutation(() => {
  const queryCache = useQueryCache();

  return useMutation({
    mutation: (request: RenameNoteRequest & { directoryId: string | null }) =>
      notesApi.rename(request),
    onSettled(_renamed, _error, request) {
      queryCache.invalidateQueries({ exact: true, key: FILES_QUERY_KEYS.getFile(request.fileId) });
      queryCache.invalidateQueries({ key: [...FILES_QUERY_KEYS.root, "search-files"] });

      const listing = request.directoryId
        ? [...FILES_QUERY_KEYS.root, "sub-files", request.directoryId]
        : [...FILES_QUERY_KEYS.root, "root-sub-files"];

      queryCache.invalidateQueries({ key: listing });
    },
  });
});

export const createNote = defineMutation(() => {
  const queryCache = useQueryCache();

  return useMutation({
    mutation: (request: CreateNoteRequest) => notesApi.createNote(request),
    onSuccess(created) {
      queryCache.setQueryData(NOTES_QUERY_KEYS.versionText(created.versionId), "");
      // The first note also creates the Notes folder at the root.
      queryCache.invalidateQueries({ key: NOTES_QUERY_KEYS.folder() });
      invalidateAfterWrite(queryCache, created);
    },
  });
});
