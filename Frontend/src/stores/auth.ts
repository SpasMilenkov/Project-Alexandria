import { ref, computed } from "vue";
import { acceptHMRUpdate, defineStore } from "pinia";
import { authApi } from "@/api/auth";
import type { LoginSchema } from "@/schemas/auth";
import type { AxiosError } from "axios";

export const useAuthStore = defineStore(
  "auth",
  () => {
    // State
    const user = ref<{ id: string; email: string; name: string } | null>(null);
    const roles = ref<string[]>([]);
    const isLoading = ref(false);
    const error = ref<string | null>(null);

    // Getters
    const isAuthenticated = computed(() => !!user.value);
    const isAdmin = computed(() => roles.value.includes("Admin"));

    // Actions
    const login = async (credentials: LoginSchema) => {
      isLoading.value = true;
      error.value = null;
      try {
        const response = await authApi.login(credentials);
        user.value = response.user;
        roles.value = response.userRoles ?? [];
        return { success: true };
      } catch (err: unknown) {
        let message = "Login failed";
        if (err instanceof Error) {
          message = err.message;
        }
        if ((err as AxiosError)?.response?.data) {
          message =
            (err as AxiosError<{ message: string }>).response?.data?.message ??
            message;
        }
        error.value = message;
        return { success: false, error: message };
      } finally {
        isLoading.value = false;
      }
    };

    const logout = async () => {
      try {
        await authApi.logout();
      } catch (error) {
        console.error("Logout error:", error);
      } finally {
        user.value = null;
        roles.value = [];
      }
    };

    const clearError = () => {
      error.value = null;
    };

    return {
      // State
      user,
      roles,
      isLoading,
      error,
      // Getters
      isAuthenticated,
      isAdmin,
      // Actions
      login,
      logout,
      clearError,
    };
  },
  {
    persist: true,
  },
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useAuthStore, import.meta.hot));
}
