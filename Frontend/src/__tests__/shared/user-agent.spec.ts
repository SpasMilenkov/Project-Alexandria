import { describe, expect, it } from "vitest";

import { describeUserAgent } from "@/utils/user-agent";

describe("describeUserAgent", () => {
  it.each([
    [
      "Mozilla/5.0 (X11; Linux x86_64; rv:132.0) Gecko/20100101 Firefox/132.0",
      "Firefox on Linux",
      false,
    ],
    [
      "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0",
      "Edge on Windows",
      false,
    ],
    [
      "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36",
      "Chrome on Android",
      true,
    ],
    [
      "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1",
      "Safari on iOS",
      true,
    ],
  ])("reads %s", (userAgent, label, isMobile) => {
    expect(describeUserAgent(userAgent)).toEqual({ isMobile, label });
  });

  it("copes with a missing user agent", () => {
    expect(describeUserAgent(null).label).toBe("Unknown device");
    expect(describeUserAgent("curl/8.0").label).toBe("Unknown browser");
  });
});
