import { afterEach, describe, expect, it, vi } from "vitest";
import { effectScope, nextTick } from "vue";

import { useLiveRefresh } from "@/composables/useLiveRefresh";
import { useRollingNow } from "@/composables/useRollingNow";

const scopes: ReturnType<typeof effectScope>[] = [];

const inScope = <T>(fn: () => T): T => {
  const scope = effectScope();
  scopes.push(scope);
  return scope.run(fn) as T;
};

afterEach(() => {
  scopes.splice(0).forEach((scope) => scope.stop());
  vi.useRealTimers();
});

describe("useLiveRefresh", () => {
  it("polls on every interval while the document is visible", () => {
    vi.useFakeTimers();
    const refresh = vi.fn();

    inScope(() => useLiveRefresh(refresh, 30_000));

    vi.advanceTimersByTime(30_000);
    vi.advanceTimersByTime(30_000);

    expect(refresh).toHaveBeenCalledTimes(2);
  });

  it("stops polling once its scope is disposed", () => {
    vi.useFakeTimers();
    const refresh = vi.fn();

    inScope(() => useLiveRefresh(refresh, 10_000));
    scopes.splice(0).forEach((scope) => scope.stop());
    vi.advanceTimersByTime(60_000);

    expect(refresh).not.toHaveBeenCalled();
  });

  it("pauses while the document is hidden", async () => {
    vi.useFakeTimers();
    const refresh = vi.fn();
    const visibility = vi.spyOn(document, "visibilityState", "get").mockReturnValue("visible");

    inScope(() => useLiveRefresh(refresh, 10_000));
    visibility.mockReturnValue("hidden");
    document.dispatchEvent(new Event("visibilitychange"));
    await nextTick();
    vi.advanceTimersByTime(60_000);

    expect(refresh).not.toHaveBeenCalled();
    visibility.mockRestore();
  });
});

describe("useRollingNow", () => {
  it("rounds up to the step so the window covers the present", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-10-30T12:00:30Z"));

    const { now } = inScope(() => useRollingNow(60_000));

    expect(now.value).toBe(Date.parse("2026-10-30T12:01:00Z"));
  });

  it("stays stable within a step and advances on the next tick", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-10-30T12:00:10Z"));

    const { now, tick } = inScope(() => useRollingNow(60_000));
    const first = now.value;

    vi.setSystemTime(new Date("2026-10-30T12:00:50Z"));
    tick();
    expect(now.value).toBe(first);

    vi.advanceTimersByTime(60_000);
    expect(now.value).toBe(first + 60_000);
  });
});
