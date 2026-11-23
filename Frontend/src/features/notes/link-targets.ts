import type { RouteLocationRaw } from "vue-router";

import type { LinkTargetDto } from "@/api/links";

import { LinkTargetKind } from "@/enums/link-target-kind";
import { LinkTargetState } from "@/enums/link-target-state";
import { pdfPageOf, startSecondsOf } from "@/features/notes/deep-links";
import { noteTitleOf } from "@/features/notes/note-name";
import { getFileIcon } from "@/utils/files/icons";
import { isMarkdownMimeType, isPdfPreviewMimeType } from "@/utils/files/mimetype";

/**
 * Where following a link leads: a page, a file selected in its folder (its details open at a
 * page when the link names one), or playback from a time.
 */
export type LinkDestination =
  | { type: "route"; to: RouteLocationRaw }
  | { type: "reveal"; directoryId: string | null; fileId: string; page?: number }
  | { type: "play"; fileId: string; startSeconds: number; fallback: LinkDestination };

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

// A plain click on a link or embed selects it for editing; a modified click opens it, the way
// links behave in document editors.
export const isOpenClick = (event: MouseEvent): boolean => event.ctrlKey || event.metaKey;

const IS_APPLE = typeof navigator !== "undefined" && /Mac|iPhone|iPad/u.test(navigator.userAgent);

export const OPEN_CLICK_HINT = IS_APPLE ? "⌘-click to open" : "Ctrl+click to open";

export const isMediaTarget = (target: LinkTargetDto): boolean =>
  target.kind === LinkTargetKind.File &&
  (target.mimeType?.startsWith("audio/") === true || target.mimeType?.startsWith("video/") === true);

// Media that can't play yet still has a page: tracks their details, videos their folder.
const mediaFallback = (target: LinkTargetDto): LinkDestination => {
  if (target.mimeType?.startsWith("audio/")) {
    return { to: { name: "track-details", params: { fileId: target.id } }, type: "route" };
  }

  return { directoryId: target.directoryId, fileId: target.id, type: "reveal" };
};

const fileDestination = (target: LinkTargetDto, suffix: string): LinkDestination => {
  if (isNoteFile(target) && !target.isEncrypted) {
    return { to: { name: "note-editor", params: { fileId: target.id } }, type: "route" };
  }

  const startSeconds = startSecondsOf(suffix);

  if (startSeconds !== null && isMediaTarget(target)) {
    return { fallback: mediaFallback(target), fileId: target.id, startSeconds, type: "play" };
  }

  const page = pdfPageOf(suffix);
  const reveal: LinkDestination = { directoryId: target.directoryId, fileId: target.id, type: "reveal" };

  if (page !== null && isPdfPreviewMimeType(target.mimeType) && !target.isEncrypted) {
    return { ...reveal, page };
  }

  return reveal;
};

/** Where the link goes; `suffix` is the `?t=`/`#page=` part as written, when there is one. */
export const linkDestination = (target: LinkTargetDto, suffix = ""): LinkDestination | null => {
  if (target.state === LinkTargetState.Unavailable) return null;
  if (target.state === LinkTargetState.Trashed) return { to: { name: "trash" }, type: "route" };

  if (target.kind === LinkTargetKind.File) return fileDestination(target, suffix);

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
