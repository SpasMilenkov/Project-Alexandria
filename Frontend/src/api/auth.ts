import type { OnboardingStep } from "@/enums";
import type { LoginSchema } from "@/schemas/auth";
import type { ChangePasswordSchema } from "@/schemas/user";

import { logger } from "@/utils/logger";

import { apiClient } from "./client";

export interface AuthResponse {
  success: boolean;
  user: { id: string; email: string; name: string };
  userRoles: string[];
  onboardingStep: OnboardingStep;
}

export interface SessionResponse {
  user: { id: string; email: string; name: string };
  userRoles: string[];
  onboardingStep: OnboardingStep;
}

export const authApi = {
  changeInitialPassword: async (payload: ChangePasswordSchema) => {
    await apiClient.patch("/auth/change-initial-password", payload);
  },

  getSession: async (): Promise<SessionResponse> => {
    const response = await apiClient.get<SessionResponse>("/auth/session", { timeout: 10_000 });
    return response.data;
  },

  login: async (credentials: LoginSchema): Promise<AuthResponse> => {
    logger.log("sending login request from api layer");
    const response = await apiClient.post<AuthResponse>("/auth/login", credentials);
    return response.data;
  },

  logout: async (): Promise<void> => {
    await apiClient.post("/auth/logout");
  },

  refreshToken: async (): Promise<{ token: string }> => {
    const response = await apiClient.post<{ token: string }>("/auth/refresh");
    return response.data;
  },
};
