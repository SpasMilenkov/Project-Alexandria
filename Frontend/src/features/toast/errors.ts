interface ErrorResponseLike {
  data?: unknown;
  status?: number;
}

interface ErrorLike {
  code?: string;
  isAxiosError?: boolean;
  message?: string;
  response?: ErrorResponseLike;
}

const OFFLINE = "Can't reach the server. Check your connection and try again.";

const TIMED_OUT = "The server took too long to answer. Try again in a moment.";

const SERVER_FAILURE = "The server ran into a problem. Try again in a moment.";

const FALLBACK = "Something went wrong. Try again.";

// Generic framework messages for these statuses ("Forbidden", "Unauthorized") say less than the hint.
const STATUS_HINTS: Record<number, string> = {
  401: "Your session expired. Sign in again to continue.",
  403: "You don't have permission to do this.",
  404: "It no longer exists. It may have been moved or deleted.",
  409: "It changed in the meantime. Refresh and try again.",
  413: "It's too large for the server or your storage quota.",
  429: "Too many requests. Wait a moment and try again.",
};

const HINT_FIRST_STATUSES = new Set([401, 403, 429]);

// Retrying the same request cannot fix these; offering a Retry button would only repeat the failure.
const PERMANENT_STATUSES = new Set([400, 401, 403, 404, 409, 413, 422]);

const asErrorLike = (cause: unknown): ErrorLike | null => {
  if (typeof cause !== "object" || cause === null) return null;

  return cause as ErrorLike;
};

const collectValidationMessages = (errors: unknown): string[] => {
  if (Array.isArray(errors)) return errors.filter((entry) => typeof entry === "string");

  if (typeof errors !== "object" || errors === null) return [];

  return Object.values(errors as Record<string, unknown>)
    .flatMap((entry) => (Array.isArray(entry) ? entry : [entry]))
    .filter((entry): entry is string => typeof entry === "string" && entry.length > 0);
};

const readServerMessage = (data: unknown): string | undefined => {
  if (typeof data === "string" && data.trim()) return data;

  if (typeof data !== "object" || data === null) return undefined;

  const body = data as { error?: unknown; errors?: unknown; message?: unknown };
  const validation = collectValidationMessages(body.errors);

  if (validation.length > 0) return validation.join(" · ");
  if (typeof body.message === "string" && body.message) return body.message;
  if (typeof body.error === "string" && body.error) return body.error;

  return undefined;
};

const isTimeout = (error: ErrorLike) => error.code === "ECONNABORTED" || error.code === "ETIMEDOUT";

const isOffline = () => typeof navigator !== "undefined" && navigator.onLine === false;

const describeHttpFailure = (status: number, data: unknown): string => {
  if (status >= 500) return SERVER_FAILURE;

  const hint = STATUS_HINTS[status];
  if (hint && HINT_FIRST_STATUSES.has(status)) return hint;

  return readServerMessage(data) ?? hint ?? FALLBACK;
};

// Turns whatever a failed operation threw into one readable cause line for a toast.
export const describeToastError = (cause: unknown): string | undefined => {
  if (cause === undefined || cause === null) return undefined;
  if (typeof cause === "string") return cause || undefined;

  const error = asErrorLike(cause);
  if (!error) return FALLBACK;

  const status = error.response?.status;
  if (typeof status === "number") return describeHttpFailure(status, error.response?.data);

  const serverMessage = readServerMessage(error.response?.data);
  if (serverMessage) return serverMessage;

  if (error.isAxiosError) {
    if (isTimeout(error)) return TIMED_OUT;

    return OFFLINE;
  }

  if (isOffline()) return OFFLINE;

  return error.message || FALLBACK;
};

export const isRetriableError = (cause: unknown): boolean => {
  const status = asErrorLike(cause)?.response?.status;
  if (typeof status !== "number") return true;

  return !PERMANENT_STATUSES.has(status);
};
