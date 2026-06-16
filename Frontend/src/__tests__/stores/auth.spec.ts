import { describe, it, expect, beforeEach, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";

import { useAuthStore } from "@/stores/auth";

const mockLogin = vi.fn();
const mockLogout = vi.fn();
vi.mock("@/api/auth", () => ({
  authApi: {
    login: (...args: unknown[]) => mockLogin(...args),
    logout: (...args: unknown[]) => mockLogout(...args),
  },
}));

const makeAuthResponse = (overrides = {}) => ({
  email: "user@test.com",
  id: "user-1",
  name: "Test User",
  token: "jwt-token",
  userRoles: ["User"],
  ...overrides,
});

//oxlint-disable-next-line max-lines-per-function
describe("useAuthStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    mockLogin.mockReset();
    mockLogout.mockReset();
  });

  it("starts unauthenticated", () => {
    const store = useAuthStore();
    expect(store.user).toBeNull();
    expect(store.isAuthenticated).toBe(false);
    expect(store.isAdmin).toBe(false);
    expect(store.isLoading).toBe(false);
    expect(store.error).toBeNull();
  });

  describe("login", () => {
    it("sets user on successful login", async () => {
      mockLogin.mockResolvedValueOnce(makeAuthResponse());

      const store = useAuthStore();
      const result = await store.login({ email: "user@test.com", password: "secret" });

      expect(result.success).toBe(true);
      expect(store.isAuthenticated).toBe(true);
      expect(store.user?.email).toBe("user@test.com");
    });

    it("sets isLoading during the call", async () => {
      mockLogin.mockImplementationOnce(
        () => new Promise((resolve) => setTimeout(() => resolve(makeAuthResponse()), 50)),
      );

      const store = useAuthStore();
      const promise = store.login({ email: "a@b.com", password: "pw" });
      expect(store.isLoading).toBe(true);
      await promise;
      expect(store.isLoading).toBe(false);
    });

    it("handles login errors", async () => {
      mockLogin.mockRejectedValueOnce(new Error("Invalid credentials"));

      const store = useAuthStore();
      const result = await store.login({ email: "bad@user.com", password: "wrong" });

      expect(result.success).toBe(false);
      expect(result.error).toBe("Invalid credentials");
      expect(store.error).toBe("Invalid credentials");
      expect(store.isAuthenticated).toBe(false);
    });

    it("extracts Axios error message from response data", async () => {
      const axiosError = {
        isAxiosError: true,
        response: { data: { message: "Account locked" } },
      };
      mockLogin.mockRejectedValueOnce(axiosError);

      const store = useAuthStore();
      const result = await store.login({ email: "locked@test.com", password: "pw" });

      expect(result.error).toBe("Account locked");
    });
  });

  describe("isAdmin", () => {
    it("is true when user has Admin role", () => {
      const store = useAuthStore();
      store.user = makeAuthResponse({ userRoles: ["Admin"] }) as any;
      expect(store.isAdmin).toBe(true);
    });

    it("is false when user lacks Admin role", () => {
      const store = useAuthStore();
      store.user = makeAuthResponse({ userRoles: ["User"] }) as any;
      expect(store.isAdmin).toBe(false);
    });

    it("is false when user is null", () => {
      const store = useAuthStore();
      expect(store.isAdmin).toBe(false);
    });
  });

  describe("logout", () => {
    it("clears user and calls the API", async () => {
      mockLogout.mockResolvedValueOnce(undefined);

      const store = useAuthStore();
      store.user = makeAuthResponse() as any;
      await store.logout();

      expect(mockLogout).toHaveBeenCalled();
      expect(store.user).toBeNull();
      expect(store.isAuthenticated).toBe(false);
    });

    it("clears user even if API call fails", async () => {
      mockLogout.mockRejectedValueOnce(new Error("Network error"));

      const store = useAuthStore();
      store.user = makeAuthResponse() as any;
      await store.logout();

      expect(store.user).toBeNull();
    });
  });

  describe("clearSession", () => {
    it("clears user without calling the API", () => {
      const store = useAuthStore();
      store.user = makeAuthResponse() as any;
      store.clearSession();
      expect(store.user).toBeNull();
      expect(mockLogout).not.toHaveBeenCalled();
    });
  });

  describe("clearError", () => {
    it("resets the error", () => {
      const store = useAuthStore();
      store.error = "Something broke";
      store.clearError();
      expect(store.error).toBeNull();
    });
  });
});
