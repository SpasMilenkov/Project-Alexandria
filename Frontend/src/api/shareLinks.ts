import { api } from "./client";

// Response / DTO Types

export interface CreateShareLinkResponse {
  id: string;
  token: string;
  expiresAt: string;
  maxAccessCount: number | null;
  createdAt: string;
  /** Set when this link is pinned to a specific version. Null means it always follows the current version. */
  fileVersionId: string | null;
}

export interface SharedFileMetadataDto {
  fileName: string;
  mimeType: string;
  size: number;
  expiresAt: string;
  hasPreview: boolean;
  versionNumber: number;
  /** True when the link targets a specific pinned version rather than the always-current version. */
  isPinnedVersion: boolean;
  /** True when the shared bytes are ciphertext; mimeType still names the plaintext type. */
  isEncrypted: boolean;
}

export interface ShareDownloadResponse {
  presignedUrl: string;
  fileName: string;
  mimeType: string;
}

export interface ShareLinkSummaryDto {
  id: string;
  token: string;
  expiresAt: string;
  createdAt: string;
  isExpired: boolean;
  maxAccessCount: number | null;
  accessCount: number;
  isRevoked: boolean;
  revokedAt: string | null;
  /** Set when this link is pinned to a specific version. Null means it always follows the current version. */
  fileVersionId: string | null;
}

// API

export const shareLinkApi = {
  /**
   * Creates a share link for a file the authenticated user owns.
   * @param fileId - The file to share.
   * @param expiry - Optional .NET TimeSpan string (e.g. "1.00:00:00" = 1 day).
   *                 Must be between "01:00:00" (1 h) and "30.00:00:00" (30 days).
   *                 Defaults to 7 days on the server when omitted.
   * @param fileVersionId - Optional. When provided, the link permanently resolves to this
   *                         version. Omit to always follow the file's current version.
   * @param maxAccessCount - Optional. When provided, the link stops working once the
   *                          download endpoint has been hit this many times. Null/omitted means unlimited.
   */
  createShareLink: (
    fileId: string,
    expiry?: string | null,
    fileVersionId?: string | null,
    maxAccessCount?: number | null,
  ): Promise<CreateShareLinkResponse> =>
    api.post<CreateShareLinkResponse>(`/files/${fileId}/share`, {
      expiry: expiry ?? null,
      fileVersionId: fileVersionId ?? null,
      maxAccessCount: maxAccessCount ?? null,
    }),

  /**
   * Fetches metadata for a shared file by token. Anonymous, no auth required.
   */
  getSharedFileMetadata: (token: string): Promise<SharedFileMetadataDto> =>
    api.get<SharedFileMetadataDto>(`/share/${token}`),

  /**
   * Resolves a short-lived presigned S3 download URL for a shared file. Anonymous.
   */
  getDownloadUrl: (token: string): Promise<ShareDownloadResponse> =>
    api.get<ShareDownloadResponse>(`/share/${token}/download`),

  /**
   * Lists all share links for a file owned by the authenticated user.
   */
  getShareLinksForFile: (fileId: string): Promise<ShareLinkSummaryDto[]> =>
    api.get<ShareLinkSummaryDto[]>(`/files/${fileId}/share`),

  /**
   * Revokes a share link by its ID. Only the creator can revoke.
   * Resolves on success (204 No Content); throws on 404 or other errors.
   */
  revokeShareLink: async (id: string): Promise<void> => {
    await api.delete(`/share/${id}`);
  },
};
