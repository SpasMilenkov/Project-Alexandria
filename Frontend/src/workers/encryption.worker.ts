// workers/encryption.worker.ts
// AES-256-GCM encryption via native Web Crypto.
// PBKDF2 key derivation: 800,000 iterations, SHA-256, 128-bit salt, 96-bit IV.
// The auth tag (16 bytes) is split from the ciphertext tail for separate DB storage.

export interface EncryptionWorkerIn {
  file: File;
  password: string;
}

export interface EncryptionWorkerOut {
  type: "done";
  ciphertext: ArrayBuffer;
  iv: string;
  salt: string;
  authTag: string;
}

export interface EncryptionWorkerError {
  type: "error";
  message: string;
}

const toBase64 = (buf: ArrayBuffer): string => btoa(String.fromCharCode(...new Uint8Array(buf)));
const PBKDF2_ITERATIONS = Number(import.meta.env.VITE_PBKDF2_ITERATIONS) || 800_000;
self.onmessage = async ({ data }: MessageEvent<EncryptionWorkerIn>) => {
  try {
    const { file, password } = data;

    const salt = crypto.getRandomValues(new Uint8Array(16));
    const iv = crypto.getRandomValues(new Uint8Array(12));

    const keyMaterial = await crypto.subtle.importKey(
      "raw",
      new TextEncoder().encode(password),
      "PBKDF2",
      false,
      ["deriveKey"],
    );

    const key = await crypto.subtle.deriveKey(
      { hash: "SHA-256", iterations: PBKDF2_ITERATIONS, name: "PBKDF2", salt },
      keyMaterial,
      { length: 256, name: "AES-GCM" },
      false,
      ["encrypt"],
    );

    const plaintext = await file.arrayBuffer();

    // Web Crypto appends the 16-byte auth tag to the ciphertext output.
    const ciphertextWithTag = await crypto.subtle.encrypt({ iv, name: "AES-GCM" }, key, plaintext);

    const ciphertext = ciphertextWithTag.slice(0, -16);
    const authTag = ciphertextWithTag.slice(-16);

    const result: EncryptionWorkerOut = {
      authTag: toBase64(authTag),
      ciphertext,
      iv: toBase64(iv.buffer),
      salt: toBase64(salt.buffer),
      type: "done",
    };

    // Transfer the ciphertext ArrayBuffer to avoid copying large files.
    self.postMessage(result, [ciphertext]);
  } catch (err: any) {
    const error: EncryptionWorkerError = { message: err.message, type: "error" };
    self.postMessage(error);
  }
};
