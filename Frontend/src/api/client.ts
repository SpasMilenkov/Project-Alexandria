import axios, { type AxiosInstance } from "axios";

import { isSessionEpochCurrent } from "@/utils/sessionBoundary";

let refreshGeneration = 0;

// If a refresh is already in-flight, every other 401 waits on this
// same promise instead of firing its own refresh call.
let refreshPromise: Promise<void> | null = null;

// A login or logout must not inherit an in-flight refresh from the previous session.
export const resetRefreshState = (): void => {
  refreshGeneration += 1;
  refreshPromise = null;
};

export const apiClient: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || "http://localhost:5000",
  headers: { "Content-Type": "application/json" },
  timeout: 100_000,
  withCredentials: true,
});

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

const refreshErrorStatus = (err: unknown): number | null => {
  if (!axios.isAxiosError(err)) return null;

  return err.response?.status ?? null;
};

export const attemptRefresh = (): Promise<void> => {
  if (refreshPromise) return refreshPromise;

  const generation = refreshGeneration;

  const pending = apiClient
    .post("/auth/refresh")
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
      if (refreshErrorStatus(err) === 401) {
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

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (originalRequest.url === "/auth/refresh") {
      return Promise.reject(error);
    }

    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      try {
        await attemptRefresh();
        return apiClient(originalRequest);
      } catch {
        return Promise.reject(error);
      }
    }

    return Promise.reject(error);
  },
);
