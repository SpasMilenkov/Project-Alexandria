import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import { authApi, type AuthResponse } from '@/api/auth'
import type { LoginSchema, RegisterSchema } from '@/schemas/auth'
import type { AxiosError } from 'axios'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref<AuthResponse | null>(null)
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  // Getters
  const isAuthenticated = computed(() => !!user.value)

  // Actions
  const login = async (credentials: LoginSchema) => {
    isLoading.value = true
    error.value = null

    try {
      console.log('sending login request from store')

      // Temporary removing until we implement backend API calls for that stuff.
      // const response = await authApi.login(credentials)

      // user.value = response

      return { success: true }
    } catch (err: unknown) {
      let message = 'Login failed'

      if (err instanceof Error) {
        message = err.message
      }

      if ((err as AxiosError)?.response?.data) {
        message = (err as AxiosError<{ message: string }>).response?.data?.message ?? message
      }

      error.value = message
      return { success: false, error: message }
    } finally {
      isLoading.value = false
    }
  }

  const register = async (userData: RegisterSchema) => {
    isLoading.value = true
    error.value = null

    try {
      const response = await authApi.register(userData)

      user.value = response

      return { success: true }
    } catch (err: unknown) {
      let message = 'Registration failed'

      if (err instanceof Error) {
        message = err.message
      }

      if ((err as AxiosError)?.response?.data) {
        message = (err as AxiosError<{ message: string }>).response?.data?.message ?? message
      }

      error.value = message
      return { success: false, error: message }
    } finally {
      isLoading.value = false
    }
  }

  const logout = async () => {
    try {
      await authApi.logout()
    } catch (error) {
      console.error('Logout error:', error)
    } finally {
      user.value = null
    }
  }

  const clearError = () => {
    error.value = null
  }

  return {
    // State
    user,
    isLoading,
    error,
    // Getters
    isAuthenticated,
    // Actions
    login,
    register,
    logout,
    clearError,
  }
})
