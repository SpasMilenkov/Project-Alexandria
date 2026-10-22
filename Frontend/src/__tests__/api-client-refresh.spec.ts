import { createPinia, setActivePinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { apiClient, attemptRefresh, resetRefreshState } from "@/api/client";
import { stopSessionBoundaryWatcher } from "@/utils/sessionBoundary";

const { mockClearSession, mockCurrentRoute, mockPush } = vi.hoisted(() => ({
  mockClearSession: vi.fn(),
  mockCurrentRoute: { value: { fullPath: "/home" } },
  mockPush: vi.fn(),
}));

vi.mock("@/stores/auth", () => ({
  useAuthStore: () => ({
    clearSession: mockClearSession,
  }),
}));

vi.mock("@/router", () => ({
  default: {
    currentRoute: mockCurrentRoute,
    push: mockPush,
  },
}));

const httpError = (status: number) => ({
  isAxiosError: true,
  response: { status },
});

const networkError = () => ({
  code: "ERR_NETWORK",
  isAxiosError: true,
});

let postSpy: ReturnType<typeof vi.spyOn>;

beforeEach(() => {
  stopSessionBoundaryWatcher();
  localStorage.clear();
  resetRefreshState();
  setActivePinia(createPinia());
  mockClearSession.mockReset();
  mockPush.mockReset();
  mockCurrentRoute.value.fullPath = "/home";
  postSpy = vi.spyOn(apiClient, "post");
});

afterEach(() => {
  resetRefreshState();
  stopSessionBoundaryWatcher();
  localStorage.clear();
  postSpy.mockRestore();
});

describe("refresh failure policy", () => {
  it("clears the session when the refresh returns 401", async () => {
    postSpy.mockRejectedValueOnce(httpError(401));

    await expect(attemptRefresh()).rejects.toMatchObject({
      response: { status: 401 },
    });

    expect(mockClearSession).toHaveBeenCalledTimes(1);
    expect(mockPush).toHaveBeenCalledWith({
      name: "auth",
      query: { redirect: "/home" },
    });
  });

  it("does not clear the session for a server error", async () => {
    postSpy.mockRejectedValueOnce(httpError(500));

    await expect(attemptRefresh()).rejects.toMatchObject({
      response: { status: 500 },
    });

    expect(mockClearSession).not.toHaveBeenCalled();
  });

  it("does not clear the session for a network error", async () => {
    postSpy.mockRejectedValueOnce(networkError());

    await expect(attemptRefresh()).rejects.toMatchObject({
      code: "ERR_NETWORK",
    });

    expect(mockClearSession).not.toHaveBeenCalled();
  });

  it("does not clear the session after a successful refresh", async () => {
    postSpy.mockResolvedValueOnce({ data: {} });

    await expect(attemptRefresh()).resolves.toBeUndefined();

    expect(mockClearSession).not.toHaveBeenCalled();
  });

  it("shares one refresh request between concurrent callers", async () => {
    let resolveRefresh: ((value: unknown) => void) | undefined;

    postSpy.mockReturnValueOnce(
      new Promise((resolve) => {
        resolveRefresh = resolve;
      }),
    );

    const first = attemptRefresh();
    const second = attemptRefresh();

    expect(postSpy).toHaveBeenCalledTimes(1);

    resolveRefresh?.({ data: {} });

    await expect(Promise.all([first, second])).resolves.toEqual([undefined, undefined]);
    expect(mockClearSession).not.toHaveBeenCalled();
  });

  it("does not clear a new session when an old refresh returns 401", async () => {
    let rejectRefresh: ((reason?: unknown) => void) | undefined;

    postSpy.mockReturnValueOnce(
      new Promise((_, reject) => {
        rejectRefresh = reject;
      }),
    );

    const pending = attemptRefresh();

    resetRefreshState();
    rejectRefresh?.(httpError(401));

    await expect(pending).rejects.toMatchObject({
      response: { status: 401 },
    });

    expect(mockClearSession).not.toHaveBeenCalled();
  });

  it("rejects an old refresh that succeeds after the session changed", async () => {
    let resolveRefresh: ((value: unknown) => void) | undefined;

    postSpy.mockReturnValueOnce(
      new Promise((resolve) => {
        resolveRefresh = resolve;
      }),
    );

    const pending = attemptRefresh();

    resetRefreshState();
    resolveRefresh?.({ data: {} });

    await expect(pending).rejects.toThrow("Session changed during token refresh");
    expect(mockClearSession).not.toHaveBeenCalled();
  });
});
