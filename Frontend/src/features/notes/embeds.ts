import type { LinkTargetDto } from "@/api/links";

import { LinkTargetKind } from "@/enums/link-target-kind";
import { LinkTargetState } from "@/enums/link-target-state";
import { isPdfPreviewMimeType } from "@/utils/files/mimetype";

import { isMediaTarget } from "./link-targets";

/** How an embed shows its target. */
export type EmbedView = "pending" | "unavailable" | "trashed" | "image" | "pdf" | "media" | "card";

/**
 * Images and PDFs show their preview, audio and video a player card, and everything else (notes,
 * folders, tags, encrypted files) a card that opens the item. The server never reads an
 * encrypted version, so it has no preview to show.
 */
export const embedView = (target: LinkTargetDto | undefined, isPending: boolean): EmbedView => {
  if (!target) return isPending ? "pending" : "unavailable";
  if (target.state === LinkTargetState.Unavailable) return "unavailable";
  if (target.state === LinkTargetState.Trashed) return "trashed";
  if (target.kind !== LinkTargetKind.File || target.isEncrypted || !target.versionId) return "card";
  if (target.mimeType?.startsWith("image/")) return "image";
  if (isPdfPreviewMimeType(target.mimeType)) return "pdf";
  if (isMediaTarget(target)) return "media";

  return "card";
};
