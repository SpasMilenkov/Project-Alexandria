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
  isEncrypted: boolean;
  // Tags only.
  color: string | null;
}

interface ResolveLinksResponse {
  targets: LinkTargetDto[];
}

// Must match the backend's ResolveLinksRequestValidator.MaxTargets.
export const MAX_RESOLVE_TARGETS = 200;

export const linksApi = {
  // One entry per distinct target, in request order.
  resolve: async (targets: LinkTargetRef[]): Promise<LinkTargetDto[]> =>
    (await api.post<ResolveLinksResponse>("/links/resolve", { targets })).targets,
};
