import { describe, expect, it } from "vitest";

import type { AccountAccessSummary } from "@/api/adminAudit";

import { LoginFailureReason } from "@/enums";
import {
  FAILURE_REASON_LABELS,
  failedTrendCaption,
  isLockedOut,
  isWithinDays,
  sessionDeviceLabels,
} from "@/features/audit/access";

const NOW = Date.UTC(2026, 5, 15, 12);

const FIREFOX_LINUX = "Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0";
const FIREFOX_LINUX_NEWER =
  "Mozilla/5.0 (X11; Linux x86_64; rv:141.0) Gecko/20100101 Firefox/141.0";
const CHROME_ANDROID =
  "Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Mobile Safari/537.36";

const account = (lockoutEnd: string | null): AccountAccessSummary => ({
  email: "alice@example.test",
  failedSignIns: 0,
  lastSignInAt: null,
  liveSessions: 0,
  lockoutEnd,
  name: "Alice",
  newNetworks: 0,
  sessionUserAgents: [],
  signIns: 0,
  userId: "00000000-0000-0000-0000-00000000000a",
});

describe("access overview helpers", () => {
  it("labels every failure reason", () => {
    const reasons = Object.values(LoginFailureReason).filter(
      (value): value is LoginFailureReason => typeof value === "number",
    );

    for (const reason of reasons) expect(FAILURE_REASON_LABELS[reason]).toBeTruthy();
  });

  it("merges sessions from the same browser and system into one device", () => {
    expect(sessionDeviceLabels([FIREFOX_LINUX, FIREFOX_LINUX_NEWER, CHROME_ANDROID])).toEqual([
      "Firefox on Linux",
      "Chrome on Android",
    ]);
  });

  it("treats only lockouts ending in the future as locked", () => {
    expect(isLockedOut(account(new Date(NOW + 60_000).toISOString()), NOW)).toBe(true);
    expect(isLockedOut(account(new Date(NOW - 60_000).toISOString()), NOW)).toBe(false);
    expect(isLockedOut(account(null), NOW)).toBe(false);
  });

  it("checks the window edge inclusively", () => {
    const edge = new Date(NOW - 30 * 24 * 60 * 60 * 1000).toISOString();

    expect(isWithinDays(edge, 30, NOW)).toBe(true);
    expect(isWithinDays(new Date(NOW - 31 * 24 * 60 * 60 * 1000).toISOString(), 30, NOW)).toBe(
      false,
    );
  });

  it("describes the change against the previous window", () => {
    expect(failedTrendCaption(5, 3, 30)).toBe("2 more than the 30 days before");
    expect(failedTrendCaption(1, 4, 90)).toBe("3 fewer than the 90 days before");
    expect(failedTrendCaption(2, 2, 30)).toBe("Same as the 30 days before");
  });
});
