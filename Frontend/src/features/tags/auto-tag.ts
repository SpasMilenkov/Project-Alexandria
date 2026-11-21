export const AUTO_TAGGING_ENABLED = import.meta.env.VITE_AUTOTAGGING_ENABLED === "true";

interface ErrorBody {
  errors?: unknown;
  message?: string;
  error?: string;
}

const bodyOf = (error: unknown): ErrorBody | undefined => {
  if (typeof error !== "object" || error === null || !("data" in error)) return undefined;

  const { data } = error as { data?: unknown };

  if (typeof data !== "object" || data === null) return undefined;

  return data as ErrorBody;
};

/**
 * The readable reason an auto-tag request failed: validation messages first, then the server's
 * message, then the error's own.
 */
export const describeAutoTagError = (error: unknown): string => {
  const body = bodyOf(error);

  if (body?.errors && typeof body.errors === "object") {
    const parts = Object.values(body.errors as Record<string, string[]>)
      .flat()
      .filter(Boolean);

    if (parts.length > 0) return parts.join(" · ");
  }

  if (body?.message) return body.message;
  if (body?.error) return body.error;
  if (error instanceof Error && error.message) return error.message;

  return "Unknown error";
};

/** The server answers this way when auto-tagging is switched off, which is not a failure. */
export const isAutoTaggingOffMessage = (message: string): boolean =>
  message.toLowerCase().includes("disabled");
