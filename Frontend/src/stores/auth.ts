import type { AxiosError } from "axios";

import { acceptHMRUpdate, defineStore } from "pinia";
import { computed, ref } from "vue";

import type { LoginSchema } from "@/schemas/auth";

import { type AuthResponse, authApi } from "@/api/auth";
import { logger } from "@/utils/logger";


export const useAuthStore = defineStore(
  "auth",
  () => {
    // State
    const user = ref<AuthResponse | null>(null);
    const isLoading = ref(false);
    const error = ref<string | null>(null);

    // Getters
    const isAuthenticated = computed(() => Boolean(user.value));
    const isAdmin = computed(() => user.value?.userRoles?.includes("Admin") ?? false);

    // Actions
    const login = async (credentials: LoginSchema) => {
      isLoading.value = true;
      error.value = null;
      try {
        const response = await authApi.login(credentials);
        user.value = response;

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
      try {
        const { useTabStore } = await import('@/stores/tab');
        
        const tabStore = useTabStore();

        tabStore.closeAllTabs();
        // localStorage.removeItem('tab');
        await authApi.logout();
      } catch (err: unknown) {
        logger.error("Logout error:", err);
      } finally {
        user.value = null;
      }
    };

    const clearSession = () => {
      user.value = null;
    };

    const clearError = () => {
      error.value = null;
    };

    return {
      // State
      user,
      isLoading,
      error,
      // Getters
      isAuthenticated,
      isAdmin,
      // Actions
      login,
      logout,
      clearError,
      clearSession,
    };
  },
  {
    persist: true,
  },
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useAuthStore, import.meta.hot));
}
