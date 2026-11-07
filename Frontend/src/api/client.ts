import { ApiError, isApiError } from "@/api/errors";
import { isSessionEpochCurrent } from "@/features/auth/session-boundary";
import { logger } from "@/utils/logger";

export const API_BASE_URL: string = import.meta.env.VITE_API_BASE_URL || "http://localhost:5000";

const DEFAULT_TIMEOUT_MS = 100_000;

const REFRESH_PATH = "/auth/refresh";

export interface RequestOptions {
  params?: object;
  timeout?: number;
}

export interface RequestOptionsWithBody extends RequestOptions {
  body?: unknown;
}

interface RequestConfig extends RequestOptionsWithBody {
  method: string;
  path: string;
}

let refreshGeneration = 0;

// If a refresh is already in-flight, every other 401 waits on this
// same promise instead of firing its own refresh call.
let refreshPromise: Promise<void> | null = null;

// A login or logout must not inherit an in-flight refresh from the previous session.
export const resetRefreshState = (): void => {
  refreshGeneration += 1;
  refreshPromise = null;
};

// The compiler cannot catch these because params is typed as object, and String() would
// quietly send "Sat Nov 07 2026..." or "a,b" instead of a value the backend can read.
const toQueryValue = (key: string, value: unknown): string => {
  if (value instanceof Date) return value.toISOString();

  if (typeof value === "object") {
    throw new TypeError(`Query param "${key}" must be a single value, not an array or object`);
  }

  return String(value);
};

const buildUrl = (path: string, params?: object): string => {
  // The base can be relative ("/api"), so new URL() would throw without an origin.
  const url = `${API_BASE_URL.replace(/\/+$/u, "")}/${path.replace(/^\/+/u, "")}`;
  if (!params) return url;

  const query = new URLSearchParams();

  for (const [key, value] of Object.entries(params)) {
    if (value !== null && value !== undefined) query.append(key, toQueryValue(key, value));
  }

  const queryString = query.toString();

  return queryString ? `${url}?${queryString}` : url;
};

const buildRequestInit = ({ body, method, timeout = DEFAULT_TIMEOUT_MS }: RequestConfig) => {
  const init: RequestInit = {
    credentials: "include",
    method,
    signal: AbortSignal.timeout(timeout),
  };

  if (body === undefined) return init;

  // The browser has to set the multipart content type itself, because it includes the boundary.
  if (body instanceof FormData) return { ...init, body };

  return {
    ...init,
    body: JSON.stringify(body),
    headers: { "Content-Type": "application/json" },
  };
};

const readBody = async (response: Response): Promise<unknown> => {
  const text = await response.text();
  if (!text) return undefined;

  const isJson = response.headers.get("content-type")?.includes("json");
  if (!isJson) return text;

  try {
    return JSON.parse(text);
  } catch (err) {
    // A bad body must not hide the response status, such as an error page from a proxy.
    logger.warn("Could not parse a JSON response body", err);

    return text;
  }
};

const toTransportError = (cause: unknown): ApiError => {
  if ((cause as { name?: string } | null)?.name === "TimeoutError") {
    return new ApiError("The request timed out", { cause, kind: "timeout" });
  }

  return new ApiError("The server could not be reached", { cause, kind: "network" });
};

const fetchResponse = async (url: string, init: RequestInit) => {
  try {
    const response = await fetch(url, init);
    const data = await readBody(response);

    return { data, response };
  } catch (cause) {
    throw toTransportError(cause);
  }
};

const sendOnce = async <T>(config: RequestConfig): Promise<T> => {
  const url = buildUrl(config.path, config.params);
  const init = buildRequestInit(config);
  const { data, response } = await fetchResponse(url, init);

  if (!response.ok) {
    throw new ApiError(`Request failed with status ${response.status}`, {
      data,
      kind: "http",
      status: response.status,
    });
  }

  return data as T;
};

const forceLogout = async () => {
  const { useAuthStore } = await import("@/stores/auth");
  const { getActivePinia } = await import("pinia");
  const { default: router } = await import("@/router");

  const pinia = getActivePinia();
  if (pinia) {
    if (!isSessionEpochCurrent()) {
      window.location.reload();
      return;
    }

    const authStore = useAuthStore(pinia);
    authStore.clearSession();
  }

  const currentPath = router.currentRoute.value.fullPath;
  // Avoid redirecting to auth if already there
  if (currentPath !== "/auth") {
    router.push({ name: "auth", query: { redirect: currentPath } });
  }
};

export const attemptRefresh = (): Promise<void> => {
  if (refreshPromise) return refreshPromise;

  const generation = refreshGeneration;

  const pending = api
    .post(REFRESH_PATH)
    .then(() => {
      // A login or logout superseded this refresh. Do not retry the old request
      // under the new session's cookies.
      if (generation !== refreshGeneration) {
        throw new Error("Session changed during token refresh");
      }
    })
    .catch(async (err: unknown) => {
      // A refresh started for the previous account must not clear the new one.
      if (generation !== refreshGeneration) throw err;

      // A rejected refresh is authoritative: the refresh token is gone or invalid.
      // Network failures and server errors must leave the local session alone.
      if (isApiError(err) && err.status === 401) {
        await forceLogout();
      }

      throw err;
    });

  const tracked: Promise<void> = pending.finally(() => {
    if (refreshPromise === tracked) refreshPromise = null;
  });

  refreshPromise = tracked;

  return tracked;
};

const send = async <T>(config: RequestConfig): Promise<T> => {
  try {
    return await sendOnce<T>(config);
  } catch (err) {
    const needsRefresh = isApiError(err) && err.status === 401 && config.path !== REFRESH_PATH;
    if (!needsRefresh) throw err;

    try {
      await attemptRefresh();
    } catch (refreshErr) {
      logger.warn("Session refresh failed, so the request was not retried", refreshErr);

      throw err;
    }

    // Goes straight to sendOnce so a second 401 rejects instead of refreshing again.
    return sendOnce<T>(config);
  }
};

const withBody =
  (method: string) =>
  <T = unknown>(path: string, body?: unknown, options: RequestOptions = {}): Promise<T> =>
    send<T>({ ...options, body, method, path });

export const api = {
  delete: <T = unknown>(path: string, options: RequestOptionsWithBody = {}): Promise<T> =>
    send<T>({ ...options, method: "DELETE", path }),

  get: <T = unknown>(path: string, options: RequestOptions = {}): Promise<T> =>
    send<T>({ ...options, method: "GET", path }),

  patch: withBody("PATCH"),

  post: withBody("POST"),

  put: withBody("PUT"),
};
