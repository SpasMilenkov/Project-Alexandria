import { describe, expect, it } from "vitest";

import { MAX_NOTE_BYTES } from "@/api/notes";
import { noteBlocker } from "@/features/notes/note-blocker";

const note = (overrides: {
  deletedAt?: string | null;
  isEncrypted?: boolean;
  mimeType?: string;
  size?: string;
}) => ({
  currentVersion: {
    isEncrypted: overrides.isEncrypted ?? false,
    mimeType: overrides.mimeType ?? "text/markdown",
    size: overrides.size ?? "120",
  },
  deletedAt: overrides.deletedAt ?? null,
});

describe("noteBlocker", () => {
  it("lets a plain markdown note open", () => {
    expect(noteBlocker(note({}))).toBeNull();
  });

  it("stops a note in the trash before anything else", () => {
    const blocker = noteBlocker(note({ deletedAt: "2026-11-20T10:00:00Z", mimeType: "image/png" }));

    expect(blocker?.title).toBe("This note is in the trash");
  });

  it("refuses files that aren't markdown", () => {
    expect(noteBlocker(note({ mimeType: "text/plain" }))?.title).toBe("Not a markdown file");
  });

  it("refuses encrypted notes", () => {
    expect(noteBlocker(note({ isEncrypted: true }))?.icon).toBe("i-lucide-lock");
  });

  it("opens a note right at the size limit and refuses one byte over", () => {
    expect(noteBlocker(note({ size: String(MAX_NOTE_BYTES) }))).toBeNull();
    expect(noteBlocker(note({ size: String(MAX_NOTE_BYTES + 1) }))?.title).toBe(
      "Too large for the editor",
    );
  });
});
