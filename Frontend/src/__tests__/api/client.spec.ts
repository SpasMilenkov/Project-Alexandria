import { createPinia, setActivePinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { API_BASE_URL, api, resetRefreshState } from "@/api/client";
import { ApiError, isApiError } from "@/api/errors";
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

const fetchMock = vi.fn<typeof fetch>();

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    headers: { "Content-Type": "application/json" },
    status,
  });

const emptyResponse = (status: number) => new Response(null, { status });

const requestedUrl = (call = 0) => String(fetchMock.mock.calls[call]?.[0]);

const requestInit = (call = 0) => fetchMock.mock.calls[call]?.[1] as RequestInit;

const requestedUrls = () => fetchMock.mock.calls.map(([url]) => String(url));

const catchError = async (request: Promise<unknown>): Promise<ApiError> => {
  const outcome = await request.then(
    () => null,
    (err: unknown) => err,
  );

  if (!isApiError(outcome)) throw new Error("Expected the request to reject with an ApiError");

  return outcome;
};

beforeEach(() => {
  stopSessionBoundaryWatcher();
  localStorage.clear();
  resetRefreshState();
  setActivePinia(createPinia());
  fetchMock.mockReset();
  mockClearSession.mockReset();
  mockPush.mockReset();
  mockCurrentRoute.value.fullPath = "/home";
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  resetRefreshState();
  stopSessionBoundaryWatcher();
  localStorage.clear();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("request URLs", () => {
  it("joins the base URL with and without a leading slash", async () => {
    fetchMock.mockImplementation(() => Promise.resolve(emptyResponse(200)));

    await api.get("/users/profile");
    await api.post("directories/move", {});

    expect(requestedUrl(0)).toBe(`${API_BASE_URL}/users/profile`);
    expect(requestedUrl(1)).toBe(`${API_BASE_URL}/directories/move`);
  });

  it("keeps a trailing slash on the path", async () => {
    fetchMock.mockResolvedValueOnce(emptyResponse(200));

    await api.delete("/files/");

    expect(requestedUrl()).toBe(`${API_BASE_URL}/files/`);
  });

  it("skips null and undefined params and keeps falsy values", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse([]));

    await api.get("/files/search", {
      params: { active: false, empty: "", gone: null, missing: undefined, page: 0, q: "a b&c" },
    });

    const { searchParams } = new URL(requestedUrl(), "http://localhost");

    expect(Object.fromEntries(searchParams)).toEqual({
      active: "false",
      empty: "",
      page: "0",
      q: "a b&c",
    });
  });

  it("leaves off the query string when every param is empty", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse([]));

    await api.get("/files", { params: { gone: null } });

    expect(requestedUrl()).toBe(`${API_BASE_URL}/files`);
  });

  it("sends Date params in ISO format", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse([]));

    await api.get("/activity/user", {
      params: { after: new Date("2026-11-07T10:30:00.000Z") },
    });

    const { searchParams } = new URL(requestedUrl(), "http://localhost");

    expect(searchParams.get("after")).toBe("2026-11-07T10:30:00.000Z");
  });

  it("repeats the key for every array item", async () => {
    fetchMock.mockResolvedValueOnce(emptyResponse(200));

    await api.get("/activity/export", { params: { eventCodes: [1, 4], format: 0 } });

    const { searchParams } = new URL(requestedUrl(), "http://localhost");

    expect(searchParams.getAll("eventCodes")).toEqual(["1", "4"]);
    expect(searchParams.get("format")).toBe("0");
  });

  it("sends no key for an empty array", async () => {
    fetchMock.mockResolvedValueOnce(emptyResponse(200));

    await api.get("/activity/export", { params: { eventCodes: [] } });

    expect(requestedUrl()).toBe(`${API_BASE_URL}/activity/export`);
  });

  it.each([
    ["an object", { nested: true }],
    ["an array of objects", [{ nested: true }]],
    ["a nested array", [["a"]]],
  ])("rejects %s without sending the request", async (_label, value) => {
    const request = api.get("/files/search/tags", { params: { tagIds: value } });

    await expect(request).rejects.toThrow(TypeError);
    await expect(request).rejects.toThrow('Query param "tagIds"');
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("does not report a bad param as a network failure", async () => {
    const outcome = await api
      .get("/thing", { params: { ids: { nested: true } } })
      .catch((err: unknown) => err);

    expect(isApiError(outcome)).toBe(false);
  });
});

describe("request bodies", () => {
  it("sends cookies with every request", async () => {
    fetchMock.mockResolvedValueOnce(emptyResponse(200));

    await api.get("/users/profile");

    expect(requestInit().credentials).toBe("include");
  });

  it("sends JSON bodies with a JSON content type", async () => {
    fetchMock.mockResolvedValueOnce(emptyResponse(200));

    await api.post("/files/move", { fileIds: ["a"] });

    expect(requestInit().method).toBe("POST");
    expect(requestInit().body).toBe('{"fileIds":["a"]}');
    expect(requestInit().headers).toEqual({ "Content-Type": "application/json" });
  });

  it("sends an empty object as a body", async () => {
    fetchMock.mockResolvedValueOnce(emptyResponse(200));

    await api.patch("/users/finish-tour", {});

    expect(requestInit().body).toBe("{}");
  });

  it("does not claim JSON when there is no body", async () => {
    fetchMock.mockResolvedValueOnce(emptyResponse(200));

    await api.post("/auth/logout");

    expect(requestInit().body).toBeUndefined();
    expect(requestInit().headers).toBeUndefined();
  });

  it("passes FormData through so the browser sets the multipart boundary", async () => {
    fetchMock.mockResolvedValueOnce(emptyResponse(200));
    const form = new FormData();
    form.append("playlistId", "p1");

    await api.post("/streaming/playlists/cover", form);

    expect(requestInit().body).toBe(form);
    expect(requestInit().headers).toBeUndefined();
  });

  it("sends a DELETE body", async () => {
    fetchMock.mockResolvedValueOnce(emptyResponse(200));

    await api.delete("/users", { body: { userIds: ["u1"] } });

    expect(requestInit().method).toBe("DELETE");
    expect(requestInit().body).toBe('{"userIds":["u1"]}');
  });

  it.each(["patch", "post", "put"] as const)("uses the %s method", async (method) => {
    fetchMock.mockResolvedValueOnce(emptyResponse(200));

    await api[method]("/thing", { a: 1 }, { params: { page: 2 } });

    expect(requestInit().method).toBe(method.toUpperCase());
    expect(requestedUrl()).toBe(`${API_BASE_URL}/thing?page=2`);
  });
});

describe("response bodies", () => {
  it("resolves to the parsed JSON directly", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ id: "f1" }));

    await expect(api.get("/files/f1")).resolves.toEqual({ id: "f1" });
  });

  it("parses JSON numbers", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(42));

    await expect(api.get<number>("/users/file-count")).resolves.toBe(42);
  });

  it("unquotes a JSON string, which is how the manifest URL arrives", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse("https://cdn/manifest.mpd"));

    await expect(api.get<string>("/streaming/manifest/1")).resolves.toBe(
      "https://cdn/manifest.mpd",
    );
  });

  it("parses JSON sent with a problem+json content type", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response('{"title":"Bad"}', {
        headers: { "Content-Type": "application/problem+json; charset=utf-8" },
      }),
    );

    await expect(api.get("/thing")).resolves.toEqual({ title: "Bad" });
  });

  it("returns the raw body as a blob when asked", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response("a,b\n1,2", { headers: { "Content-Type": "text/csv" } }),
    );

    const body = await api.get<Blob>("/activity/export", { responseType: "blob" });

    // fetch and jsdom each bring their own Blob class, so the shape is checked instead.
    expect(body.type).toBe("text/csv");
    await expect(body.text()).resolves.toBe("a,b\n1,2");
  });

  it("still parses a JSON error body when a blob was asked for", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ errors: { from: ["Too early"] } }, 400));

    const err = await catchError(api.get("/activity/export", { responseType: "blob" }));

    expect(err.status).toBe(400);
    expect(err.data).toEqual({ errors: { from: ["Too early"] } });
  });

  it("resolves to undefined for an empty body", async () => {
    fetchMock.mockResolvedValueOnce(emptyResponse(204));

    await expect(api.delete("/files/1")).resolves.toBeUndefined();
  });

  it("returns text that is not marked as JSON", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response("plain", { headers: { "Content-Type": "text/plain" } }),
    );

    await expect(api.get("/thing")).resolves.toBe("plain");
  });
});

describe("failures", () => {
  it("rejects a non-2xx response with its status and parsed body", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ message: "Name taken" }, 409));

    const err = await catchError(api.post("/directories", {}));

    expect(err).toBeInstanceOf(ApiError);
    expect(err.kind).toBe("http");
    expect(err.status).toBe(409);
    expect(err.data).toEqual({ message: "Name taken" });
  });

  it("keeps the status when a proxy sends an HTML error page", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response("<html>Bad Gateway</html>", {
        headers: { "Content-Type": "text/html" },
        status: 502,
      }),
    );

    const err = await catchError(api.get("/thing"));

    expect(err.status).toBe(502);
    expect(err.data).toBe("<html>Bad Gateway</html>");
  });

  it("keeps the status when a JSON error body is malformed", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response("{not json", {
        headers: { "Content-Type": "application/json" },
        status: 500,
      }),
    );

    const err = await catchError(api.get("/thing"));

    expect(err.status).toBe(500);
    expect(err.data).toBe("{not json");
  });

  it("reports a failed connection as a network error", async () => {
    const cause = new TypeError("Failed to fetch");
    fetchMock.mockRejectedValueOnce(cause);

    const err = await catchError(api.get("/thing"));

    expect(err.kind).toBe("network");
    expect(err.status).toBeUndefined();
    expect(err.cause).toBe(cause);
  });

  it("reports a connection dropped while reading the body as a network error", async () => {
    fetchMock.mockResolvedValueOnce({
      headers: new Headers(),
      ok: true,
      status: 200,
      text: () => Promise.reject(new TypeError("network error")),
    } as unknown as Response);

    const err = await catchError(api.get("/thing"));

    expect(err.kind).toBe("network");
  });

  it("reports an expired timeout as a timeout error", async () => {
    fetchMock.mockImplementationOnce(
      (_url, init) =>
        new Promise((_resolve, reject) => {
          init?.signal?.addEventListener("abort", () => reject(init.signal?.reason));
        }),
    );

    const err = await catchError(api.get("/slow", { timeout: 1 }));

    expect(err.kind).toBe("timeout");
    expect(err.status).toBeUndefined();
  });

  it("uses the default timeout unless the request sets its own", async () => {
    const timeoutSpy = vi.spyOn(AbortSignal, "timeout");
    fetchMock.mockImplementation(() => Promise.resolve(emptyResponse(200)));

    await api.get("/thing");
    await api.get("/auth/session", { timeout: 10_000 });

    expect(timeoutSpy).toHaveBeenNthCalledWith(1, 100_000);
    expect(timeoutSpy).toHaveBeenNthCalledWith(2, 10_000);
  });
});

describe("session refresh on 401", () => {
  it("refreshes and retries the request once", async () => {
    fetchMock
      .mockResolvedValueOnce(emptyResponse(401))
      .mockResolvedValueOnce(emptyResponse(200))
      .mockResolvedValueOnce(jsonResponse({ id: "f1" }));

    await expect(api.get("/files/f1")).resolves.toEqual({ id: "f1" });

    expect(requestedUrls()).toEqual([
      `${API_BASE_URL}/files/f1`,
      `${API_BASE_URL}/auth/refresh`,
      `${API_BASE_URL}/files/f1`,
    ]);
  });

  it("gives the retry its own timeout and resends the body", async () => {
    const timeoutSpy = vi.spyOn(AbortSignal, "timeout");
    fetchMock
      .mockResolvedValueOnce(emptyResponse(401))
      .mockResolvedValueOnce(emptyResponse(200))
      .mockResolvedValueOnce(emptyResponse(200));

    await api.post("/files/move", { fileIds: ["a"] });

    expect(timeoutSpy).toHaveBeenCalledTimes(3);
    expect(requestInit(2).signal).not.toBe(requestInit(0).signal);
    expect(requestInit(2).body).toBe('{"fileIds":["a"]}');
  });

  it("rejects when the retry also returns 401, without refreshing again", async () => {
    fetchMock
      .mockResolvedValueOnce(emptyResponse(401))
      .mockResolvedValueOnce(emptyResponse(200))
      .mockResolvedValueOnce(emptyResponse(401));

    const err = await catchError(api.get("/files/f1"));

    expect(err.status).toBe(401);
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it("shares one refresh between concurrent 401s", async () => {
    const seen = new Set<string>();

    fetchMock.mockImplementation((input) => {
      const url = String(input);

      if (url.endsWith("/auth/refresh")) return Promise.resolve(emptyResponse(200));
      if (seen.has(url)) return Promise.resolve(jsonResponse({ url }));

      seen.add(url);

      return Promise.resolve(emptyResponse(401));
    });

    await Promise.all([api.get("/a"), api.get("/b")]);

    const refreshes = requestedUrls().filter((url) => url.endsWith("/auth/refresh"));

    expect(refreshes).toHaveLength(1);
    expect(fetchMock).toHaveBeenCalledTimes(5);
  });

  it("rejects the original error when the refresh fails", async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse({ message: "original" }, 401))
      .mockResolvedValueOnce(emptyResponse(500));

    const err = await catchError(api.get("/files/f1"));

    expect(err.status).toBe(401);
    expect(err.data).toEqual({ message: "original" });
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(mockClearSession).not.toHaveBeenCalled();
  });

  it("logs out and rejects the original error when the refresh returns 401", async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse({ message: "original" }, 401))
      .mockResolvedValueOnce(emptyResponse(401));

    const err = await catchError(api.get("/files/f1"));

    expect(err.data).toEqual({ message: "original" });
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(mockClearSession).toHaveBeenCalledTimes(1);
    expect(mockPush).toHaveBeenCalledWith({ name: "auth", query: { redirect: "/home" } });
  });

  it("does not refresh when the refresh endpoint itself returns 401", async () => {
    fetchMock.mockResolvedValueOnce(emptyResponse(401));

    const err = await catchError(api.post("/auth/refresh"));

    expect(err.status).toBe(401);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("does not refresh for other failure statuses", async () => {
    fetchMock.mockResolvedValueOnce(emptyResponse(403));

    const err = await catchError(api.get("/files/f1"));

    expect(err.status).toBe(403);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
