import type { FileResult, FileVersionDto } from "@/api/file";

import { MAX_NOTE_BYTES } from "@/api/notes";
import { isMarkdownMimeType } from "@/utils/files/mimetype";
import { formatBytes } from "@/utils/size";

export interface NoteBlocker {
  icon: string;
  title: string;
  description: string;
}

type BlockerVersion = Pick<FileVersionDto, "isEncrypted" | "mimeType" | "size">;
type BlockerFile = Pick<FileResult, "deletedAt"> & { currentVersion: BlockerVersion };

/** Why a file can't open in the note editor, or null when it can. */
export const noteBlocker = (file: BlockerFile): NoteBlocker | null => {
  const version = file.currentVersion;

  if (file.deletedAt) {
    return {
      description: "Restore it from the trash to open it again.",
      icon: "i-lucide-file-x",
      title: "This note is in the trash",
    };
  }

  if (!isMarkdownMimeType(version.mimeType)) {
    return {
      description: "Only markdown files open in the note editor.",
      icon: "i-lucide-file-x",
      title: "Not a markdown file",
    };
  }

  if (version.isEncrypted) {
    return {
      description: "Encrypted notes need a password step the editor doesn't have yet.",
      icon: "i-lucide-lock",
      title: "Encrypted notes can't be opened yet",
    };
  }

  const size = Number(version.size);

  if (size > MAX_NOTE_BYTES) {
    return {
      description: `The editor opens notes up to ${formatBytes(MAX_NOTE_BYTES)}, and this one is ${formatBytes(size)}.`,
      icon: "i-lucide-file-warning",
      title: "Too large for the editor",
    };
  }

  return null;
};
