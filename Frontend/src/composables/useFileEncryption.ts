// composables/useFileEncryption.ts

export interface EncryptionResult {
  ciphertext: ArrayBuffer;
  iv: string;
  salt: string;
  authTag: string;
}

/**
 * Spawns a short-lived encryption worker, encrypts the file, and resolves
 * with the ciphertext blob and base64-encoded AES-GCM parameters.
 *
 * The worker is always terminated after one use regardless of outcome.
 */
export const encryptFile = (file: File, password: string): Promise<EncryptionResult> =>
  new Promise((resolve, reject) => {
    const worker = new Worker(new URL("@/workers/encryption.worker.ts", import.meta.url), {
      type: "module",
    });

    worker.onmessage = ({ data }) => {
      worker.terminate();
      if (data.type === "done") {
        resolve(data as EncryptionResult);
      } else {
        reject(new Error(data.message ?? "Encryption failed"));
      }
    };

    worker.onerror = (e) => {
      worker.terminate();
      reject(e);
    };

    worker.postMessage({ file, password });
  });
