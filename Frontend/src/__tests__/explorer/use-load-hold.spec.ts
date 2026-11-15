import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { effectScope, nextTick, ref } from "vue";

import { useLoadHold } from "@/composables/useLoadHold";

const setup = (enabled = true) => {
  const key = ref("a");
  const scope = effectScope();
  const hold = scope.run(() =>
    useLoadHold(
      () => key.value,
      () => enabled,
    ),
  )!;

  return { hold, key, scope };
};

describe("useLoadHold", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("does not hold before the first change", () => {
    const { hold } = setup();

    expect(hold.isHolding.value).toBe(false);
  });

  it("holds after the key changes and lets go after the window", async () => {
    const { hold, key } = setup();

    key.value = "b";
    await nextTick();
    expect(hold.isHolding.value).toBe(true);

    vi.advanceTimersByTime(250);
    expect(hold.isHolding.value).toBe(false);
  });

  it("restarts the window when the key changes again", async () => {
    const { hold, key } = setup();

    key.value = "b";
    await nextTick();
    vi.advanceTimersByTime(200);

    key.value = "c";
    await nextTick();
    vi.advanceTimersByTime(200);
    expect(hold.isHolding.value).toBe(true);

    vi.advanceTimersByTime(50);
    expect(hold.isHolding.value).toBe(false);
  });

  it("lets go early once the next item is ready", async () => {
    const { hold, key } = setup();

    key.value = "b";
    await nextTick();
    hold.release();

    expect(hold.isHolding.value).toBe(false);
  });

  it("never holds when disabled", async () => {
    const { hold, key } = setup(false);

    key.value = "b";
    await nextTick();

    expect(hold.isHolding.value).toBe(false);
  });

  it("clears its timer when the scope ends", async () => {
    const { hold, key, scope } = setup();

    key.value = "b";
    await nextTick();
    scope.stop();
    vi.advanceTimersByTime(250);

    expect(hold.isHolding.value).toBe(true);
  });
});
