import { describe, expect, it } from "vitest";

import { ApiError } from "@/api/errors";
import { describeAutoTagError, isAutoTaggingOffMessage } from "@/features/tags/auto-tag";

describe("describeAutoTagError", () => {
  it("joins validation messages from every field", () => {
    const error = new ApiError("Bad request", {
      data: { errors: { fileId: ["Not an audio file"], generic: ["Try later"] } },
      kind: "http",
      status: 400,
    });

    expect(describeAutoTagError(error)).toBe("Not an audio file · Try later");
  });

  it("falls back to the server's message, then its error field", () => {
    expect(describeAutoTagError({ data: { message: "Auto-tagging is disabled" } })).toBe(
      "Auto-tagging is disabled",
    );
    expect(describeAutoTagError({ data: { error: "Queue full" } })).toBe("Queue full");
  });

  it("uses the error's own message when the body says nothing", () => {
    expect(describeAutoTagError(new Error("Network error"))).toBe("Network error");
  });

  it("says something even for an unknown throw", () => {
    expect(describeAutoTagError(undefined)).toBe("Unknown error");
  });
});

describe("isAutoTaggingOffMessage", () => {
  it("recognises the switched-off answer in any case", () => {
    expect(isAutoTaggingOffMessage("Auto-tagging is Disabled")).toBe(true);
    expect(isAutoTaggingOffMessage("Queue full")).toBe(false);
  });
});
