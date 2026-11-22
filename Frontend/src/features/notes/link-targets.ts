import type { RouteLocationRaw } from "vue-router";

import type { LinkTargetDto } from "@/api/links";

import { LinkTargetKind } from "@/enums/link-target-kind";
import { LinkTargetState } from "@/enums/link-target-state";
import { noteTitleOf } from "@/features/notes/note-name";
import { getFileIcon } from "@/utils/files/icons";
import { isMarkdownMimeType } from "@/utils/files/mimetype";

/** Where following a link leads: a page, or a file selected in its folder. */
export type LinkDestination =
  | { type: "route"; to: RouteLocationRaw }
  | { type: "reveal"; directoryId: string | null; fileId: string };

const KIND_NOUNS: Record<LinkTargetKind, string> = {
  [LinkTargetKind.File]: "file",
  [LinkTargetKind.Folder]: "folder",
  [LinkTargetKind.Tag]: "tag",
  [LinkTargetKind.Playlist]: "playlist",
};

const KIND_ICONS: Record<LinkTargetKind, string> = {
  [LinkTargetKind.File]: "i-lucide-file",
  [LinkTargetKind.Folder]: "i-lucide-folder",
  [LinkTargetKind.Tag]: "i-lucide-tag",
  [LinkTargetKind.Playlist]: "i-lucide-list-music",
};

export const linkKindNoun = (kind: LinkTargetKind): string => KIND_NOUNS[kind];

const isNoteFile = (target: LinkTargetDto): boolean =>
  target.kind === LinkTargetKind.File && isMarkdownMimeType(target.mimeType);

export const linkTargetIcon = (kind: LinkTargetKind, target: LinkTargetDto | undefined): string => {
  if (target?.state === LinkTargetState.Unavailable) return "i-lucide-link-2-off";
  if (kind === LinkTargetKind.File && target?.name) return getFileIcon(target.name);

  return KIND_ICONS[kind];
};

/** The target's live name; notes drop their extension like they do in the editor header. */
export const linkTargetName = (target: LinkTargetDto): string | null => {
  if (!target.name) return null;
  if (isNoteFile(target)) return noteTitleOf(target.name);

  return target.name;
};

export const linkDestination = (target: LinkTargetDto): LinkDestination | null => {
  if (target.state === LinkTargetState.Unavailable) return null;
  if (target.state === LinkTargetState.Trashed) return { to: { name: "trash" }, type: "route" };

  if (target.kind === LinkTargetKind.File) {
    if (isNoteFile(target) && !target.isEncrypted) {
      return { to: { name: "note-editor", params: { fileId: target.id } }, type: "route" };
    }

    return { directoryId: target.directoryId, fileId: target.id, type: "reveal" };
  }

  if (target.kind === LinkTargetKind.Folder) {
    return { to: { name: "dashboard", params: { dirId: target.id } }, type: "route" };
  }

  if (target.kind === LinkTargetKind.Playlist) {
    return { to: { name: "playlist-details", params: { id: target.id } }, type: "route" };
  }

  return { to: { name: "tags" }, type: "route" };
};

/** A short state for the link, or null when it simply opens. */
export const linkStateLabel = (target: LinkTargetDto | undefined): string | null => {
  if (target?.state === LinkTargetState.Trashed) return "In trash";
  if (target?.state === LinkTargetState.Unavailable) return "Unavailable";

  return null;
};
