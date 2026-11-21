import { defineQueryOptions } from "@pinia/colada";

import { notesApi } from "@/api/notes";

export const NOTES_QUERY_KEYS = {
  folder: () => [...NOTES_QUERY_KEYS.root, "folder"],
  root: ["notes"] as const,
  versionText: (versionId: string) => [...NOTES_QUERY_KEYS.root, "version-text", versionId],
};

// A version's bytes never change, so the text is read once. Refetching on focus would replace
// the editor's starting point while someone is typing.
export const getNoteVersionText = defineQueryOptions((versionId: string) => ({
  key: NOTES_QUERY_KEYS.versionText(versionId),
  query: () => notesApi.getVersionText(versionId),
  refetchOnMount: false,
  refetchOnReconnect: false,
  refetchOnWindowFocus: false,
  staleTime: Infinity,
}));

// Null until the first note creates the folder. It can't be renamed or moved, so it rarely changes.
export const getNotesFolder = defineQueryOptions(() => ({
  key: NOTES_QUERY_KEYS.folder(),
  query: () => notesApi.getNotesFolder(),
  staleTime: 5 * 60_000,
}));
