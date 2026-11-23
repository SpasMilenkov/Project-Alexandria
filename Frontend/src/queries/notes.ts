import { defineQueryOptions } from "@pinia/colada";

import { notesApi } from "@/api/notes";

export const NOTES_QUERY_KEYS = {
  draft: (fileId: string) => [...NOTES_QUERY_KEYS.root, "draft", fileId],
  folder: () => [...NOTES_QUERY_KEYS.root, "folder"],
  root: ["notes"] as const,
  summaries: (fileIds: string[]) => [...NOTES_QUERY_KEYS.summariesRoot, fileIds.join(",")],
  summariesRoot: ["notes", "summaries"] as const,
  versionText: (versionId: string) => [...NOTES_QUERY_KEYS.root, "version-text", versionId],
};

// Saves invalidate these; a note's excerpt otherwise changes only when its text does.
export const getNoteSummaries = defineQueryOptions((fileIds: string[]) => ({
  key: NOTES_QUERY_KEYS.summaries(fileIds),
  query: () => notesApi.getSummaries(fileIds),
  staleTime: 30_000,
}));

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

// Read fresh on every opening, since another tab or device may have written it, and never again
// while the editor is open: the editor's own writes move it on, and a refetch would replace the
// text being typed. Dropped from the cache as soon as the editor closes.
export const getNoteDraft = defineQueryOptions((fileId: string) => ({
  gcTime: 0,
  key: NOTES_QUERY_KEYS.draft(fileId),
  query: () => notesApi.getDraft(fileId),
  refetchOnReconnect: false,
  refetchOnWindowFocus: false,
  staleTime: 0,
}));

// Null until the first note creates the folder. It can't be renamed or moved, so it rarely changes.
export const getNotesFolder = defineQueryOptions(() => ({
  key: NOTES_QUERY_KEYS.folder(),
  query: () => notesApi.getNotesFolder(),
  staleTime: 5 * 60_000,
}));
