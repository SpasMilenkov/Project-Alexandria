import type { OnboardingStep } from "@/enums";
import type { LoginSchema } from "@/schemas/auth";
import type { ChangePasswordSchema } from "@/schemas/user";

import { logger } from "@/utils/logger";

import { api } from "./client";

export interface SessionUser {
  id: string;
  email: string;
  name: string;
  // Missing on sessions persisted before profile pictures existed.
  avatarUrl?: string | null;
}

export interface AuthResponse {
  success: boolean;
  user: SessionUser;
  userRoles: string[];
  onboardingStep: OnboardingStep;
}

export interface SessionResponse {
  user: SessionUser;
  userRoles: string[];
  onboardingStep: OnboardingStep;
}

export interface UserSession {
  id: string;
  sessionStartedAt: string;
  lastActiveAt: string;
  expiresAt: string;
  ipAddress: string | null;
  userAgent: string | null;
  isCurrent: boolean;
}

export const authApi = {
  changeInitialPassword: async (payload: ChangePasswordSchema) => {
    await api.patch("/auth/change-initial-password", payload);
  },

  getSessions: (): Promise<UserSession[]> => api.get<UserSession[]>("/auth/sessions"),

  getSession: (): Promise<SessionResponse> =>
    api.get<SessionResponse>("/auth/session", { timeout: 10_000 }),

  login: (credentials: LoginSchema): Promise<AuthResponse> => {
    logger.log("sending login request from api layer");
    return api.post<AuthResponse>("/auth/login", credentials);
  },

  logout: async (): Promise<void> => {
    await api.post("/auth/logout");
  },

  revokeSession: async (sessionId: string): Promise<void> => {
    await api.delete(`/auth/sessions/${sessionId}`);
  },

  refreshToken: (): Promise<{ token: string }> => api.post<{ token: string }>("/auth/refresh"),
};
