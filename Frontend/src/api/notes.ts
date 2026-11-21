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
  content: string;
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
}

// Must match the backend's TextContentLimits.MaxBytes.
export const MAX_NOTE_BYTES = 1_000_000;

export const notesApi = {
  createNote: (request: CreateNoteRequest): Promise<FileContentSaveResult> =>
    api.post<FileContentSaveResult>("/notes", request),

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
};
