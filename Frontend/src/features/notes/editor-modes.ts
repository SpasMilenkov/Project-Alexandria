export type NoteEditorMode = "rich" | "source";

export const NOTE_EDITOR_MODE_OPTIONS = [
  { label: "Rich text", value: "rich" },
  { label: "Markdown", value: "source" },
] as const satisfies readonly { label: string; value: NoteEditorMode }[];
