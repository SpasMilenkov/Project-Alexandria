import type { OnboardingStep } from "@/enums";
import type { LoginSchema } from "@/schemas/auth";
import type { ChangePasswordSchema } from "@/schemas/user";

import { logger } from "@/utils/logger";

import { api } from "./client";

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
    await api.patch("/auth/change-initial-password", payload);
  },

  getSession: (): Promise<SessionResponse> =>
    api.get<SessionResponse>("/auth/session", { timeout: 10_000 }),

  login: (credentials: LoginSchema): Promise<AuthResponse> => {
    logger.log("sending login request from api layer");
    return api.post<AuthResponse>("/auth/login", credentials);
  },

  logout: async (): Promise<void> => {
    await api.post("/auth/logout");
  },

  refreshToken: (): Promise<{ token: string }> => api.post<{ token: string }>("/auth/refresh"),
};
