import { defineQueryOptions } from "@pinia/colada";

import { type LinkTargetDto, type LinkTargetRef, MAX_RESOLVE_TARGETS, linksApi } from "@/api/links";
import { createBatcher } from "@/features/notes/link-batcher";
import { linkKey } from "@/features/notes/links";

export const LINK_QUERY_KEYS = {
  root: ["links"] as const,
  target: (ref: LinkTargetRef) => [...LINK_QUERY_KEYS.root, "target", linkKey(ref)],
};

// Every chip on screen asks for its own target; they mount together, so their lookups share a
// request instead of sending one each.
const targetLoader = createBatcher<LinkTargetRef, LinkTargetDto>({
  keyOf: linkKey,
  loadMany: linksApi.resolve,
  maxBatch: MAX_RESOLVE_TARGETS,
  valueKeyOf: linkKey,
  waitMs: 10,
});

// Names change rarely while a note is open, and a focus refetch picks up renames made elsewhere.
export const getLinkTarget = defineQueryOptions((ref: LinkTargetRef) => ({
  key: LINK_QUERY_KEYS.target(ref),
  query: () => targetLoader.load({ id: ref.id, kind: ref.kind }),
  staleTime: 30_000,
}));
