import apiClient from "@/api/client";
import { fileApi } from "@/api/file";
import FileDecryptionModal from "@/components/dashboard/file-system/Modals/FileDecryptionModal.vue";
import { decryptFile } from "@/composables/useDecrypt";
import { logger } from "@/utils/logger";

/**
 * Single entry point for all download operations across the app.
 * Replaces all direct `fileApi.downloadFile` / `fileApi.downloadFileVersion` calls.
 *
 * For non-encrypted files: triggers a direct browser download via the presigned URL
 *
 * For encrypted files:
 *   1. Fetches the raw bytes from the presigned URL once, before the modal opens.
 *   2. Opens FileDecryptionModal with an onAttempt callback that runs decryption
 *      entirely in a Web Worker.
 *   3. On correct password: assembles a Blob, triggers a download, closes modal.
 *   4. On wrong password: the callback throws; the modal surfaces the error and
 *      stays open for another attempt without re-fetching the bytes.
 */
export const useFileDownload = () => {
  const overlay = useOverlay();
  // Create once per composable instance; the overlay handles mounting/unmounting.
  const decryptionModal = overlay.create(FileDecryptionModal);

  // Helpers

  /** Triggers a direct browser download via a presigned URL (no local Blob). */
  const triggerDirectDownload = (presignedUrl: string, fileName: string): void => {
    const a = document.createElement("a");
    a.href = presignedUrl;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  };

  /** Triggers a browser download from an in-memory Blob and revokes the object URL. */
  const triggerBlobDownload = (blob: Blob, fileName: string): void => {
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    // Revoke after a generous delay so the browser has time to start the download.
    setTimeout(() => URL.revokeObjectURL(url), 10_000);
  };

  // Core encrypted download flow

  const handleEncryptedDownload = async (info: {
    presignedUrl: string;
    fileName: string;
    mimeType: string;
    encryptionIv: string | null;
    encryptionSalt: string | null;
    integrityTag: string | null;
    encryptionHint: string | null;
  }): Promise<void> => {
    if (!info.encryptionIv || !info.encryptionSalt || !info.integrityTag) {
      throw new Error("File is marked encrypted but is missing encryption metadata.");
    }

    // Pre-fetch the raw bytes once so that every subsequent password attempt
    // only pays the cost of PBKDF2 + AES-GCM, not another network round-trip.
    const response = await fetch(info.presignedUrl);
    if (!response.ok) {
      throw new Error(`Failed to fetch file from storage (${response.status})`);
    }
    const rawBytes = await response.arrayBuffer();

    // Capture metadata in closure — avoids passing large buffers as props.
    const { encryptionIv, encryptionSalt, integrityTag, fileName, mimeType, encryptionHint } = info;

    const instance = decryptionModal.open({
      hint: encryptionHint ?? null,
      onAttempt: async (password: string): Promise<void> => {
        // rawBytes is never transferred to the worker, so it survives retries.
        const plaintext = await decryptFile(
          rawBytes,
          password,
          encryptionIv,
          encryptionSalt,
          integrityTag,
        );

        const blob = new Blob([plaintext], { type: mimeType });
        triggerBlobDownload(blob, fileName);
        // Returning without throwing signals success → modal closes itself.
      },
    });

    // Await result so that callers can chain .catch() if needed,
    // and to keep the overlay instance alive until the modal closes.
    await instance.result;
  };

  // Public API

  const downloadFile = async (fileId: string): Promise<void> => {
    const info = await fileApi.downloadFile(fileId);

    if (!info.isEncrypted) {
      triggerDirectDownload(info.presignedUrl, info.fileName);
      return;
    }

    await handleEncryptedDownload(info);
  };

  const downloadVersion = async (versionId: string): Promise<void> => {
    const info = await fileApi.downloadFileVersion(versionId);

    if (!info.isEncrypted) {
      triggerDirectDownload(info.presignedUrl, info.fileName);
      return;
    }

    await handleEncryptedDownload(info);
  };

  const downloadBulk = async (fileIds?: string[], directoryIds?: string[]) => {
    const token = await fileApi.bulkDownloadInit({ directoryIds, fileIds });
    logger.log("token", token);
    const a = document.createElement("a");
    a.href = `${apiClient.defaults.baseURL}/files/download-bulk/${token}`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  };

  return { downloadBulk, downloadFile, downloadVersion };
};
