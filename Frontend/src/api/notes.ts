import type { DirectorySummaryDto } from "./directory";

import { api } from "./client";

// What a save or create left behind: enough for the editor to keep saving from the new version.
export interface FileContentSaveResult {
  fileId: string;
  versionId: string;
  versionNumber: number;
  size: number;
  fileName: string;
  mimeType: string;
  directoryId: string | null;
  // A new file was made, rather than a version added to one.
  created: boolean;
  // The text matched the parent version exactly, so nothing was written.
  unchanged: boolean;
}

export interface SaveNoteContentRequest {
  fileId: string;
  // The version the edit started from; the server refuses the save with 409 if it isn't current.
  parentVersionId: string;
  // The draft this version replaces. A stored draft at another revision refuses the save (409).
  draftRevision: string | null;
  content: string;
}

// The editor's working text between versions, kept on the server so autosave makes no version.
export interface NoteDraft {
  fileId: string;
  // May be older than the file's current version, when a version arrived from somewhere else.
  baseVersionId: string;
  revision: string;
  content: string;
  updatedAt: string;
}

export interface SaveNoteDraftRequest {
  fileId: string;
  // 409 when this is no longer the file's current version.
  baseVersionId: string;
  // The revision this editor last saw; 409 when the stored draft is at another one.
  expectedRevision: string | null;
  content: string;
}

export interface NoteDraftSaveResult {
  // Null when the text matched the version, so the server dropped the draft.
  revision: string | null;
  savedAt: string;
  // Set when this write named a note still called "Untitled".
  renamedTo: string | null;
}

export interface RenameNoteRequest {
  fileId: string;
  // The name without its extension; the file keeps the one it has.
  title: string;
}

export interface NoteRenameResult {
  fileId: string;
  fileName: string;
}

export interface CreateNoteRequest {
  // A folder inside Notes. Left out, the note goes in the Notes folder itself.
  directoryId?: string | null;
  title?: string | null;
  // The note's first text, such as a link to what it is about.
  content?: string | null;
}

// Must match the backend's TextContentLimits.MaxBytes.
export const MAX_NOTE_BYTES = 1_000_000;

export const notesApi = {
  createNote: (request: CreateNoteRequest): Promise<FileContentSaveResult> =>
    api.post<FileContentSaveResult>("/notes", request),

  // Keeps a newer draft written elsewhere; the server answers 204 either way.
  discardDraft: (fileId: string, revision: string): Promise<void> =>
    api.delete<void>(`/files/${fileId}/draft`, { params: { revision } }),

  // 204 when the file has no draft, which the client hands back as no body.
  getDraft: async (fileId: string): Promise<NoteDraft | null> =>
    (await api.get<NoteDraft | undefined>(`/files/${fileId}/draft`)) ?? null,

  // 204 until the first note creates the folder, which the client hands back as no body.
  getNotesFolder: async (): Promise<DirectorySummaryDto | null> =>
    (await api.get<DirectorySummaryDto | undefined>("/notes/folder")) ?? null,

  // Not audited as a download. An empty note has no body, which the client hands back as nothing.
  getVersionText: async (versionId: string): Promise<string> =>
    (await api.get<string | undefined>(`/files/versions/${versionId}/content`)) ?? "",

  // 409 when another file in the folder already has the name; it is never numbered silently.
  rename: ({ fileId, title }: RenameNoteRequest): Promise<NoteRenameResult> =>
    api.patch<NoteRenameResult>(`/notes/${fileId}/name`, { title }),

  saveContent: ({ fileId, ...body }: SaveNoteContentRequest): Promise<FileContentSaveResult> =>
    api.put<FileContentSaveResult>(`/files/${fileId}/content`, body),

  saveDraft: ({ fileId, ...body }: SaveNoteDraftRequest): Promise<NoteDraftSaveResult> =>
    api.put<NoteDraftSaveResult>(`/files/${fileId}/draft`, body),
};
