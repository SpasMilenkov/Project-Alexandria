import apiClient from './client'
import type { LoginSchema, RegisterSchema } from '@/schemas/auth'

export interface AuthResponse {
  id: string
  email: string
  name: string
}

export const authApi = {
  login: async (credentials: LoginSchema): Promise<AuthResponse> => {
    console.log('sending login request from api layer')
    const response = await apiClient.post<AuthResponse>('/auth/login', credentials)
    return response.data
  },

  register: async (userData: RegisterSchema): Promise<AuthResponse> => {
    const response = await apiClient.post<AuthResponse>('/auth/register', userData)
    return response.data
  },

  logout: async (): Promise<void> => {
    await apiClient.post('/auth/logout')
  },

  refreshToken: async (): Promise<{ token: string }> => {
    const response = await apiClient.post<{ token: string }>('/auth/refresh')
    return response.data
  },

  getProfile: async (): Promise<AuthResponse> => {
    const response = await apiClient.get<AuthResponse>('/auth/profile')
    return response.data
  },
}
