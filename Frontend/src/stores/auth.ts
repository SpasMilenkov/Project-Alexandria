import type { AxiosError } from "axios";

import { useMutationCache, useQueryCache } from "@pinia/colada";
import { type Pinia, acceptHMRUpdate, defineStore, getActivePinia } from "pinia";
import { computed, nextTick, ref } from "vue";

import type { LoginSchema } from "@/schemas/auth";

import { type AuthResponse, authApi } from "@/api/auth";
import { resetRefreshState } from "@/api/client";
import { useActivityStore } from "@/stores/activity";
import { useDirectoryStore } from "@/stores/directory";
import { useFileStore } from "@/stores/file";
import { useHomeStore } from "@/stores/home";
import { usePlayerStore } from "@/stores/stream-player";
import { useSettingsStore } from "@/stores/settings";
import { useTabStore } from "@/stores/tab";
import { cancelBackgroundImageLoad } from "@/utils/backgroundImageObjectUrl";
import { logger } from "@/utils/logger";
import { rotateSessionEpoch } from "@/utils/sessionBoundary";

const clearSessionCaches = (pinia: Pinia) => {
  try {
    const queryCache = useQueryCache(pinia);

    // cancelQueries suppresses a pending fetch's commit, so a late response
    // cannot recreate the entry that remove() is about to delete.
    queryCache.cancelQueries(undefined, "session-boundary");
    queryCache.getEntries().forEach((entry) => queryCache.remove(entry));

    const mutationCache = useMutationCache(pinia);

    mutationCache.getEntries().forEach((entry) => mutationCache.remove(entry));
  } catch (err: unknown) {
    // Colada is not installed in some store-only unit tests.
    logger.error("Unable to clear query cache:", err);
  }
};

const resetOwnerStores = (pinia: Pinia) => {
  cancelBackgroundImageLoad();
  useActivityStore(pinia).resetSessionState();
  useDirectoryStore(pinia).resetSessionState();
  useFileStore(pinia).resetSessionState();
  useHomeStore(pinia).resetSessionState();
  useSettingsStore(pinia).resetSessionState();
  useTabStore(pinia).resetSessionState();

  // The shuffle session belongs to the account being torn down. Releasing it
  // must happen before this reset, while its request still carries that account's cookies.
  usePlayerStore(pinia).clearOwnerState({ releaseSession: false });
};

const removeLegacyOwnerStorage = () => {
  try {
    localStorage.removeItem("player");
  } catch {
    // Storage can be unavailable during private browsing; in-memory state is still reset.
  }
};

const resetPrivateState = (): void => {
  const pinia = getActivePinia();

  if (!pinia) {
    return;
  }

  clearSessionCaches(pinia);
  resetOwnerStores(pinia);
  removeLegacyOwnerStorage();
};

const publishSessionBoundary = async (): Promise<void> => {
  // Persistence watchers run on the next flush. Waiting for it guarantees
  // peers see the new snapshots before they see the new epoch.
  await nextTick();
  rotateSessionEpoch();
};

export const useAuthStore = defineStore(
  "auth",
  () => {
    // State
    const user = ref<AuthResponse | null>(null);
    const isLoading = ref(false);
    const error = ref<string | null>(null);
    const sessionVerified = ref(false);

    let sessionGeneration = 0;
    let verificationPromise: Promise<boolean> | null = null;

    // Getters
    const isAuthenticated = computed(() => Boolean(user.value));
    const isAdmin = computed(() => user.value?.userRoles?.includes("Admin") ?? false);
    const isSessionVerified = computed(() => Boolean(user.value) && sessionVerified.value);

    const resetSessionVerification = () => {
      sessionGeneration += 1;
      verificationPromise = null;
      sessionVerified.value = false;
    };

    const ensureSessionVerified = (): Promise<boolean> => {
      if (!user.value) {
        return Promise.resolve(false);
      }

      if (sessionVerified.value) {
        return Promise.resolve(true);
      }

      if (verificationPromise) {
        return verificationPromise;
      }

      const generation = sessionGeneration;
      const expectedUserId = user.value.user?.id;

      const pending = (async () => {
        try {
          const session = await authApi.getSession();

          if (generation !== sessionGeneration || !user.value) {
            return false;
          }

          if (!expectedUserId || session.user.id !== expectedUserId) {
            await clearSession();
            return false;
          }

          user.value = { ...session, success: true };
          sessionVerified.value = true;
          verificationPromise = null;

          return true;
        } catch (err: unknown) {
          // A server rejection is authoritative only through the refresh 401 path.
          // Network and 5xx failures keep the hint but never unlock private routes.
          if (generation !== sessionGeneration) {
            return false;
          }

          verificationPromise = null;
          logger.error("Session verification failed:", err);
          return false;
        }
      })();

      verificationPromise = pending;

      return pending;
    };

    // Actions
    const login = async (credentials: LoginSchema) => {
      isLoading.value = true;
      error.value = null;
      try {
        const response = await authApi.login(credentials);

        resetRefreshState();
        resetSessionVerification();
        user.value = null;
        resetPrivateState();

        user.value = response;
        sessionVerified.value = true;
        useHomeStore().setOwner(response.user?.id ?? null);
        await publishSessionBoundary();

        return { success: true };
      } catch (err: unknown) {
        let message = "Login failed";
        if (err instanceof Error) {
          ({ message } = err);
        }
        if ((err as AxiosError)?.response?.data) {
          message = (err as AxiosError<{ message: string }>).response?.data?.message ?? message;
        }
        error.value = message;
        return { error: message, success: false };
      } finally {
        isLoading.value = false;
      }
    };

    const logout = async () => {
      // Start the release while the retiring account's cookies are still active.
      let release: Promise<void> | void = Promise.resolve();

      try {
        release = usePlayerStore().releaseShuffleSession();
      } catch (err: unknown) {
        logger.error("Shuffle session release error:", err);
      }

      resetRefreshState();
      resetSessionVerification();
      user.value = null;
      resetPrivateState();
      await publishSessionBoundary();

      try {
        await authApi.logout();
      } catch (err: unknown) {
        logger.error("Logout error:", err);
      }

      try {
        await release;
      } catch (err: unknown) {
        logger.error("Shuffle session release error:", err);
      }
    };

    const clearSession = async () => {
      resetRefreshState();
      resetSessionVerification();
      user.value = null;
      resetPrivateState();
      await publishSessionBoundary();
    };

    const clearError = () => {
      error.value = null;
    };

    // oxlint-disable-next-line sort-keys
    return {
      // State
      user,
      isLoading,
      error,
      // Getters
      isAuthenticated,
      isAdmin,
      isSessionVerified,
      // Actions
      login,
      logout,
      clearError,
      clearSession,
      ensureSessionVerified,
    };
  },
  {
    persist: {
      pick: ["user"],
    },
  },
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useAuthStore, import.meta.hot));
}
