import { defineMutation, useMutation, useQueryCache } from "@pinia/colada";

import { shareLinkApi } from "@/api/shareLinks";
import { SHARE_LINKS_QUERY_KEYS } from "@/queries/shareLinks";

interface CreateShareLinkInput {
  fileId: string;
  expiry?: string | null;
  fileVersionId?: string | null;
  maxAccessCount?: number | null;
}

export const createShareLink = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ fileId, expiry, fileVersionId, maxAccessCount }: CreateShareLinkInput) =>
      shareLinkApi.createShareLink(fileId, expiry, fileVersionId, maxAccessCount),
    onSettled(_: any, __: any, { fileId }: CreateShareLinkInput) {
      queryCache.invalidateQueries({ key: SHARE_LINKS_QUERY_KEYS.forFile(fileId) });
    },
  });
});

/**
 * fileId is not used by the API call itself but is required here so onSettled
 * can narrow the invalidation to the right file's link list, same pattern as deleteVersion.
 */
export const revokeShareLink = defineMutation(() => {
  const queryCache = useQueryCache();
  return useMutation({
    mutation: ({ id }: { id: string; fileId: string }) => shareLinkApi.revokeShareLink(id),
    onSettled(_: any, __: any, { fileId }: { id: string; fileId: string }) {
      queryCache.invalidateQueries({ key: SHARE_LINKS_QUERY_KEYS.forFile(fileId) });
    },
  });
});
