import { describe, expect, it } from "vitest";

import { ApiError } from "@/api/errors";
import { usernameFieldError } from "@/features/account/profile-errors";

const httpError = (status: number, data?: unknown) =>
  new ApiError("Request failed", { data, kind: "http", status });

describe("usernameFieldError", () => {
  it("explains a taken name", () => {
    expect(usernameFieldError(httpError(409))).toBe("That username is already taken.");
  });

  it("surfaces the first validation message", () => {
    const err = httpError(400, { errors: { userName: ["Username is too short."] } });

    expect(usernameFieldError(err)).toBe("Username is too short.");
  });

  it("leaves server failures to the toast", () => {
    expect(usernameFieldError(httpError(500))).toBeNull();
    expect(usernameFieldError(new Error("offline"))).toBeNull();
  });
});
