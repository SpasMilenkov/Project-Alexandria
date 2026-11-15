import { describe, expect, it, vi } from "vitest";
import { effectScope, nextTick, ref } from "vue";

import { useListNavigation } from "@/composables/useListNavigation";

const setup = (initial: string[]) => {
  const items = ref(initial);
  const scope = effectScope();
  const nav = scope.run(() =>
    useListNavigation(
      () => items.value,
      (item) => item,
    ),
  )!;

  return { items, nav };
};

const key = (name: string) => new KeyboardEvent("keydown", { cancelable: true, key: name });

describe("useListNavigation", () => {
  it("starts with nothing active and enters from either end", () => {
    const { nav } = setup(["a", "b", "c"]);

    expect(nav.activeItem.value).toBeNull();

    nav.move(1);
    expect(nav.activeItem.value).toBe("a");

    nav.clear();
    nav.move(-1);
    expect(nav.activeItem.value).toBe("c");
  });

  it("wraps around at both ends", () => {
    const { nav } = setup(["a", "b"]);

    nav.move(1);
    nav.move(1);
    nav.move(1);
    expect(nav.activeItem.value).toBe("a");

    nav.move(-1);
    expect(nav.activeItem.value).toBe("b");
  });

  it("follows the active item when the list refreshes", async () => {
    const { items, nav } = setup(["a", "b", "c"]);

    nav.setActive(1);
    items.value = ["z", "a", "b"];
    await nextTick();

    expect(nav.activeItem.value).toBe("b");
  });

  it("resets once the active item is gone", async () => {
    const { items, nav } = setup(["a", "b"]);

    nav.setActive(1);
    items.value = ["a", "c"];
    await nextTick();

    expect(nav.activeIndex.value).toBe(-1);
  });

  it("opens the active item on Enter and drops it on Escape", () => {
    const { nav } = setup(["a", "b"]);
    const open = vi.fn();

    nav.handleKey(key("ArrowDown"), open);
    const enter = key("Enter");
    expect(nav.handleKey(enter, open)).toBe(true);
    expect(open).toHaveBeenCalledWith("a");
    expect(enter.defaultPrevented).toBe(true);

    const escape = key("Escape");
    expect(nav.handleKey(escape, open)).toBe(true);
    expect(escape.defaultPrevented).toBe(true);
    expect(nav.activeItem.value).toBeNull();
  });

  it("leaves Enter and Escape alone with nothing active, so the modal can close", () => {
    const { nav } = setup(["a"]);
    const escape = key("Escape");

    expect(nav.handleKey(key("Enter"), vi.fn())).toBe(false);
    expect(nav.handleKey(escape, vi.fn())).toBe(false);
    expect(escape.defaultPrevented).toBe(false);
  });

  it("leaves the arrows to the input on an empty list", () => {
    const { nav } = setup([]);
    const down = key("ArrowDown");

    expect(nav.handleKey(down, vi.fn())).toBe(false);
    expect(down.defaultPrevented).toBe(false);
  });
});
