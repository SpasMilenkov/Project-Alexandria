import { afterEach, describe, expect, it, vi } from "vitest";

import { shuffleApi } from "@/api/shuffle";

describe("shuffle-api", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("creates a valid UUID when randomUUID is unavailable", () => {
    vi.stubGlobal("crypto", {
      getRandomValues: (bytes: Uint8Array) => {
        bytes.forEach((_, index) => {
          bytes[index] = index;
        });
        return bytes;
      },
    });

    expect(shuffleApi.newRequestId()).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
    );
  });
});
