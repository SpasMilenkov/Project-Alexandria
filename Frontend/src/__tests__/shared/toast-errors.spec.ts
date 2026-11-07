import { afterEach, describe, expect, it, vi } from "vitest";

import { ApiError } from "@/api/errors";
import { describeToastError, isRetriableError } from "@/features/toast/errors";

const httpError = (status: number, data: unknown = {}) =>
  new ApiError(`Request failed with status ${status}`, { data, kind: "http", status });

const networkError = () => new ApiError("The server could not be reached", { kind: "network" });

const timeoutError = () => new ApiError("The request timed out", { kind: "timeout" });

describe("describeToastError", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("passes ready-made descriptions through untouched", () => {
    expect(describeToastError("Try again from your transcoding jobs.")).toBe(
      "Try again from your transcoding jobs.",
    );
  });

  it("returns nothing when there is no cause", () => {
    expect(describeToastError(undefined)).toBeUndefined();
    expect(describeToastError(null)).toBeUndefined();
    expect(describeToastError("")).toBeUndefined();
  });

  it("joins validation messages from the server", () => {
    const error = httpError(400, { errors: { name: ["Name is required"], size: ["Too big"] } });

    expect(describeToastError(error)).toBe("Name is required · Too big");
  });

  it("prefers the server's own explanation for conflicts", () => {
    expect(describeToastError(httpError(409, { message: "A folder with that name exists" }))).toBe(
      "A folder with that name exists",
    );
  });

  it("falls back to a status hint when the server says nothing useful", () => {
    expect(describeToastError(httpError(404))).toBe(
      "It no longer exists. It may have been moved or deleted.",
    );
    expect(describeToastError(httpError(413))).toBe(
      "It's too large for the server or your storage quota.",
    );
  });

  it("ignores generic framework text for permission and session failures", () => {
    expect(describeToastError(httpError(403, { message: "Forbidden" }))).toBe(
      "You don't have permission to do this.",
    );
    expect(describeToastError(httpError(401, { message: "Unauthorized" }))).toBe(
      "Your session expired. Sign in again to continue.",
    );
  });

  it("hides server internals behind one line for 5xx responses", () => {
    expect(describeToastError(httpError(500, { message: "NullReferenceException at ..." }))).toBe(
      "The server ran into a problem. Try again in a moment.",
    );
  });

  it("tells a dropped connection apart from a timeout", () => {
    expect(describeToastError(networkError())).toBe(
      "Can't reach the server. Check your connection and try again.",
    );
    expect(describeToastError(timeoutError())).toBe(
      "The server took too long to answer. Try again in a moment.",
    );
  });

  it("reports offline for non-HTTP failures while the browser is offline", () => {
    vi.stubGlobal("navigator", { onLine: false });

    expect(describeToastError(new Error("Failed to fetch"))).toBe(
      "Can't reach the server. Check your connection and try again.",
    );
  });

  it("uses a plain error's message otherwise", () => {
    expect(describeToastError(new Error("Encryption failed"))).toBe("Encryption failed");
  });
});

describe("isRetriableError", () => {
  it("allows retrying transient failures", () => {
    expect(isRetriableError(httpError(503))).toBe(true);
    expect(isRetriableError(httpError(429))).toBe(true);
    expect(isRetriableError(networkError())).toBe(true);
    expect(isRetriableError(new Error("boom"))).toBe(true);
  });

  it("refuses retrying failures that would only repeat", () => {
    for (const status of [400, 401, 403, 404, 409, 413, 422]) {
      expect(isRetriableError(httpError(status))).toBe(false);
    }
  });
});
