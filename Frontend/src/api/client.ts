import axios, { type AxiosInstance } from "axios";

const MAX_REFRESH_FAILURES = 3;
let refreshFailures = 0;

// If a refresh is already in-flight, every other 401 waits on this
// same promise instead of firing its own refresh call.
let refreshPromise: Promise<void> | null = null;

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

  refreshPromise = apiClient
    .post("/auth/refresh")
    .then(() => {
      refreshFailures = 0;
    })
    .catch(async (err) => {
      refreshFailures++;
      if (refreshFailures >= MAX_REFRESH_FAILURES) {
        refreshFailures = 0;
        await forceLogout();
      }
      throw err;
    })
    .finally(() => {
      refreshPromise = null;
    });

  return refreshPromise;
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

