import { defineQueryOptions } from "@pinia/colada";

import {
  type ShareDownloadResponse,
  type ShareLinkSummaryDto,
  type SharedFileMetadataDto,
  shareLinkApi,
} from "@/api/shareLinks";

export const SHARE_LINKS_QUERY_KEYS = {
  root: ["share-links"] as const,
  downloadUrl: (token: string) => [...SHARE_LINKS_QUERY_KEYS.root, "download-url", token],
  forFile: (fileId: string) => [...SHARE_LINKS_QUERY_KEYS.root, "for-file", fileId],
  metadata: (token: string) => [...SHARE_LINKS_QUERY_KEYS.root, "metadata", token],
};

export const getSharedFileMetadata = defineQueryOptions<string, SharedFileMetadataDto>(
  (token: string) => ({
    key: SHARE_LINKS_QUERY_KEYS.metadata(token),
    query: async () =>await shareLinkApi.getSharedFileMetadata(token),
    retry: 1,
  }),
);

/**
 * The presigned S3 URL returned by the backend is only valid for 5 minutes.
 * staleTime is kept safely under that window so the cache never serves an expired URL.
 */
export const getSharedFileDownloadUrl = defineQueryOptions<string, ShareDownloadResponse>(
  (token: string) => ({
    key: SHARE_LINKS_QUERY_KEYS.downloadUrl(token),
    query: () => shareLinkApi.getDownloadUrl(token),
    refetchOnMount: true,
    staleTime: 3 * 60 * 1000,
    retry: 0,
  }),
);

export const getShareLinksForFile = defineQueryOptions<string, ShareLinkSummaryDto[]>(
  (fileId: string) => ({
    key: SHARE_LINKS_QUERY_KEYS.forFile(fileId),
    query: () => shareLinkApi.getShareLinksForFile(fileId),
  }),
);
