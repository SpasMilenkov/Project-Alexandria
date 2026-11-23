import type { LinkTargetKind } from "@/enums/link-target-kind";
import type { LinkTargetState } from "@/enums/link-target-state";

import { api } from "./client";

export interface LinkTargetRef {
  kind: LinkTargetKind;
  id: string;
}

// Only kind, id and state are set for an unavailable target.
export interface LinkTargetDto extends LinkTargetRef {
  state: LinkTargetState;
  name: string | null;
  // Files only.
  mimeType: string | null;
  directoryId: string | null;
  // The current version, which embeds read their preview and thumbnail from.
  versionId: string | null;
  isEncrypted: boolean;
  // Tags only.
  color: string | null;
}

interface ResolveLinksResponse {
  targets: LinkTargetDto[];
}

/** A note that links to an item, with the line the link sits on. */
export interface NoteBacklinkDto {
  fileId: string;
  name: string;
  directoryId: string | null;
  updatedAt: string;
  // Markdown, so its links can show their labels or live names.
  snippet: string;
}

interface NoteBacklinksResponse {
  notes: NoteBacklinkDto[];
}

// Must match the backend's ResolveLinksRequestValidator.MaxTargets.
export const MAX_RESOLVE_TARGETS = 200;

export const linksApi = {
  // The viewer's own notes linking to the target, most recently edited first.
  backlinks: async ({ kind, id }: LinkTargetRef): Promise<NoteBacklinkDto[]> =>
    (await api.get<NoteBacklinksResponse>("/links/backlinks", { params: { id, kind } })).notes,

  // One entry per distinct target, in request order.
  resolve: async (targets: LinkTargetRef[]): Promise<LinkTargetDto[]> =>
    (await api.post<ResolveLinksResponse>("/links/resolve", { targets })).targets,
};
