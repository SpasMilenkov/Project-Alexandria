import type {
  DecryptionWorkerError,
  DecryptionWorkerIn,
  DecryptionWorkerOut,
} from "@/workers/decryption.worker";

/**
 * Spawns a short-lived decryption worker, decrypts the file, and resolves
 * with the plaintext ArrayBuffer.
 *
 * The worker is always terminated after one use regardless of outcome.
 *
 * Note: ciphertext is NOT transferred to the worker (structured clone instead),
 * so the same buffer can safely be reused across multiple password attempts
 * without becoming neutered.
 */
export const decryptFile = (
  ciphertext: ArrayBuffer,
  password: string,
  iv: string,
  salt: string,
  authTag: string,
): Promise<ArrayBuffer> =>
  new Promise((resolve, reject) => {
    const worker = new Worker(new URL("@/workers/decryption.worker.ts", import.meta.url), {
      type: "module",
    });

    worker.onmessage = ({ data }: MessageEvent<DecryptionWorkerOut | DecryptionWorkerError>) => {
      worker.terminate();
      if (data.type === "done") {
        resolve((data as DecryptionWorkerOut).plaintext);
      } else {
        reject(new Error((data as DecryptionWorkerError).message));
      }
    };

    worker.onerror = (e) => {
      worker.terminate();
      reject(e);
    };

    const payload: DecryptionWorkerIn = { authTag, ciphertext, iv, password, salt };
    // No transferable list for ciphertext — keep original buffer intact for retries.
    worker.postMessage(payload);
  });
