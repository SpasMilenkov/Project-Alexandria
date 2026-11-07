import { createPinia, setActivePinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { api, attemptRefresh, resetRefreshState } from "@/api/client";
import { ApiError } from "@/api/errors";
import { stopSessionBoundaryWatcher } from "@/features/auth/session-boundary";

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

const httpError = (status: number) =>
  new ApiError(`Request failed with status ${status}`, { kind: "http", status });

const networkError = () => new ApiError("The server could not be reached", { kind: "network" });

let postSpy: ReturnType<typeof vi.spyOn>;

beforeEach(() => {
  stopSessionBoundaryWatcher();
  localStorage.clear();
  resetRefreshState();
  setActivePinia(createPinia());
  mockClearSession.mockReset();
  mockPush.mockReset();
  mockCurrentRoute.value.fullPath = "/home";
  postSpy = vi.spyOn(api, "post");
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

    await expect(attemptRefresh()).rejects.toMatchObject({ status: 401 });

    expect(mockClearSession).toHaveBeenCalledTimes(1);
    expect(mockPush).toHaveBeenCalledWith({
      name: "auth",
      query: { redirect: "/home" },
    });
  });

  it("does not clear the session for a server error", async () => {
    postSpy.mockRejectedValueOnce(httpError(500));

    await expect(attemptRefresh()).rejects.toMatchObject({ status: 500 });

    expect(mockClearSession).not.toHaveBeenCalled();
  });

  it("does not clear the session for a network error", async () => {
    postSpy.mockRejectedValueOnce(networkError());

    await expect(attemptRefresh()).rejects.toMatchObject({ kind: "network" });

    expect(mockClearSession).not.toHaveBeenCalled();
  });

  it("does not clear the session after a successful refresh", async () => {
    postSpy.mockResolvedValueOnce({});

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

    resolveRefresh?.({});

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

    await expect(pending).rejects.toMatchObject({ status: 401 });

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
    resolveRefresh?.({});

    await expect(pending).rejects.toThrow("Session changed during token refresh");
    expect(mockClearSession).not.toHaveBeenCalled();
  });
});
