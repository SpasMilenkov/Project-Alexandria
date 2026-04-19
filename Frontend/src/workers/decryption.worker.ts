// AES-256-GCM decryption via native Web Crypto. Zero external dependencies.
// Mirrors the encryption worker exactly: same PBKDF2 params (600k iterations,
// SHA-256, 128-bit salt, 96-bit IV). The 16-byte auth tag is reattached to the
// ciphertext tail before passing to SubtleCrypto, which expects them combined.

export interface DecryptionWorkerIn {
  ciphertext: ArrayBuffer; // raw ciphertext WITHOUT the auth tag
  password: string;
  iv: string; // base64 — 96-bit nonce
  salt: string; // base64 — 128-bit PBKDF2 salt
  authTag: string; // base64 — 128-bit GCM auth tag stored separately
}

export interface DecryptionWorkerOut {
  type: "done";
  plaintext: ArrayBuffer;
}

export interface DecryptionWorkerError {
  type: "error";
  message: string;
}

const fromBase64 = (b64: string): Uint8Array => {
  const binary = atob(b64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
};

self.onmessage = async ({ data }: MessageEvent<DecryptionWorkerIn>) => {
  try {
    const { ciphertext, password, iv: ivB64, salt: saltB64, authTag: authTagB64 } = data;

    const iv = fromBase64(ivB64);
    const salt = fromBase64(saltB64);
    const authTag = fromBase64(authTagB64);

    const keyMaterial = await crypto.subtle.importKey(
      "raw",
      new TextEncoder().encode(password),
      "PBKDF2",
      false,
      ["deriveKey"],
    );

    const key = await crypto.subtle.deriveKey(
      { hash: "SHA-256", iterations: 600_000, name: "PBKDF2", salt },
      keyMaterial,
      { length: 256, name: "AES-GCM" },
      false,
      ["decrypt"],
    );

    // Web Crypto's AES-GCM decrypt expects ciphertext + 16-byte auth tag appended.
    // The backend stores them separately, so we reattach before decrypting.
    const combined = new Uint8Array(ciphertext.byteLength + authTag.byteLength);
    combined.set(new Uint8Array(ciphertext), 0);
    combined.set(authTag, ciphertext.byteLength);

    const plaintext = await crypto.subtle.decrypt({ iv, name: "AES-GCM" }, key, combined);

    const result: DecryptionWorkerOut = { plaintext, type: "done" };
    // Transfer the plaintext buffer to avoid copying large files back to main thread.
    self.postMessage(result, [plaintext]);
  } catch (err: any) {
    // Web Crypto throws DOMException{ name: "OperationError" } on wrong password.
    // Surface a clear message rather than the raw crypto error.
    const isWrongPassword = err instanceof DOMException && err.name === "OperationError";

    const error: DecryptionWorkerError = {
      message: isWrongPassword
        ? "Incorrect password — please try again"
        : (err?.message ?? "Decryption failed"),
      type: "error",
    };
    self.postMessage(error);
  }
};
