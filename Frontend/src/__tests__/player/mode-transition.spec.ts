import { mount } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { computed, defineComponent, h, nextTick, ref } from "vue";

import { usePlayerModeTransition } from "@/composables/usePlayerModeTransition";

interface PendingAnimation {
  finish: () => void;
  cancel: ReturnType<typeof vi.fn>;
}

let pending: PendingAnimation[];
let reduced: boolean;
let preferenceChange: (() => void) | undefined;
let resizeLayout: ResizeObserverCallback;
const wrappers: ReturnType<typeof mount>[] = [];

const flush = async () => {
  for (let i = 0; i < 6; i++) await nextTick();
};

const createPlayer = () => {
  const beforeChange = vi.fn();
  const positionFloating = vi.fn();
  let transition: ReturnType<typeof usePlayerModeTransition>;
  const mode = ref<"pip" | "strip">("strip");
  const wrapper = mount(
    defineComponent({
      setup: () => {
        const card = ref<HTMLElement | null>(null);
        const layout = ref<HTMLElement | null>(null);
        transition = usePlayerModeTransition({
          card,
          layout,
          mode,
          beforeChange,
          positionFloating,
        });
        const content = computed(() =>
          h("section", { key: mode.value }, [
            h(
              "button",
              { "data-player-mode-toggle": mode.value, onClick: transition.toggle },
              mode.value,
            ),
            h("img", { "data-player-artwork": "", id: "track-art" }),
          ]),
        );
        return () =>
          h("div", { ref: layout, "data-layout": "" }, [
            h("div", { ref: card, class: ["player-card", mode.value] }, [content.value]),
          ]);
      },
    }),
    { attachTo: document.body },
  );
  wrappers.push(wrapper);
  return { wrapper, mode, beforeChange, positionFloating, transition: transition! };
};

beforeEach(() => {
  pending = [];
  reduced = false;
  preferenceChange = undefined;
  vi.stubGlobal(
    "ResizeObserver",
    class {
      constructor(callback: ResizeObserverCallback) {
        resizeLayout = callback;
      }
      observe = vi.fn();
      disconnect = vi.fn();
    },
  );
  vi.stubGlobal(
    "matchMedia",
    vi.fn(() => ({
      get matches() {
        return reduced;
      },
      addEventListener: (_: string, callback: () => void) => {
        preferenceChange = callback;
      },
      removeEventListener: vi.fn(),
    })),
  );
  vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(function () {
    if (this.classList.contains("pip")) return new DOMRect(700, 350, 320, 300);
    return new DOMRect(0, 650, 1024, 90);
  });
  vi.spyOn(HTMLElement.prototype, "getClientRects").mockReturnValue([
    new DOMRect(0, 0, 40, 40),
  ] as unknown as DOMRectList);
  vi.stubGlobal("Animation", class {});
  Object.defineProperty(HTMLElement.prototype, "animate", {
    configurable: true,
    value: vi.fn(() => {
      let finish!: () => void;
      let reject!: (reason: unknown) => void;
      const finished = new Promise<void>((resolve, rejectPromise) => {
        finish = resolve;
        reject = rejectPromise;
      });
      const cancel = vi.fn(() => reject(new DOMException("Cancelled", "AbortError")));
      pending.push({ finish, cancel });
      return { finished, cancel };
    }),
  });
});

afterEach(() => {
  wrappers.splice(0).forEach((wrapper) => wrapper.unmount());
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  Reflect.deleteProperty(HTMLElement.prototype, "animate");
  document.body.innerHTML = "";
});

describe("player mode transition", () => {
  it("ignores descendant transition events and settles only after its owned animations finish", async () => {
    const { wrapper, transition, mode } = createPlayer();
    const finished = transition.toggle();
    await flush();
    wrapper.get("button").element.dispatchEvent(new Event("transitionend", { bubbles: true }));
    expect(transition.isSwitching.value).toBe(true);
    expect(mode.value).toBe("pip");
    expect(wrapper.element.style.height).toBe("90px");
    expect(document.querySelectorAll("#track-art")).toHaveLength(1);
    expect(wrapper.find(".player-motion-snapshot").attributes("aria-hidden")).toBe("true");
    pending.forEach((animation) => animation.finish());
    await finished;
    expect(transition.isSwitching.value).toBe(false);
    expect(wrapper.find(".player-motion-surface").exists()).toBe(false);
    expect(wrapper.element.style.height).toBe("");
  });

  it("reverses without allowing cancelled completion to remove the new animation", async () => {
    const { wrapper, transition, mode } = createPlayer();
    const first = transition.toggle();
    await flush();
    const oldAnimations = [...pending];
    const second = transition.toggle();
    await flush();
    await first;
    expect(mode.value).toBe("strip");
    expect(oldAnimations.every((animation) => animation.cancel.mock.calls.length === 1)).toBe(true);
    expect(transition.isSwitching.value).toBe(true);
    expect(wrapper.findAll(".player-motion-surface")).toHaveLength(1);
    pending.slice(oldAnimations.length).forEach((animation) => animation.finish());
    await second;
    expect(transition.isSwitching.value).toBe(false);
    expect(wrapper.find(".player-motion-surface").exists()).toBe(false);
  });

  it("handles a second toggle before the first Vue layout update", async () => {
    const { wrapper, transition, mode } = createPlayer();
    const first = transition.toggle();
    const second = transition.toggle();
    await flush();
    pending.forEach((animation) => animation.finish());
    await Promise.all([first, second]);
    expect(mode.value).toBe("strip");
    expect(wrapper.find(".player-motion-surface").exists()).toBe(false);
    expect(transition.isSwitching.value).toBe(false);
  });

  it("moves keyboard focus to the destination toggle", async () => {
    const { wrapper, transition } = createPlayer();
    const oldButton = wrapper.get("button").element;
    oldButton.focus();
    const finished = transition.toggle();
    await flush();
    expect(document.activeElement).toBe(wrapper.get("button").element);
    expect(document.activeElement).not.toBe(oldButton);
    pending.forEach((animation) => animation.finish());
    await finished;
  });

  it("skips motion but positions the floating player when reduced motion is enabled", async () => {
    reduced = true;
    const { wrapper, transition, positionFloating } = createPlayer();
    await transition.toggle();
    expect(pending).toHaveLength(0);
    expect(positionFloating).toHaveBeenCalledOnce();
    expect(transition.isSwitching.value).toBe(false);
    expect(wrapper.element.style.height).toBe("");
  });

  it.each(["window", "layout", "preference"])(
    "settles an active animation on %s changes",
    async (change) => {
      const { wrapper, transition } = createPlayer();
      const finished = transition.toggle();
      await flush();
      if (change === "window") window.dispatchEvent(new Event("resize"));
      if (change === "layout")
        resizeLayout(
          [{ contentRect: { width: 900 } } as ResizeObserverEntry],
          {} as ResizeObserver,
        );
      if (change === "preference") {
        reduced = true;
        preferenceChange?.();
      }
      await flush();
      await finished;
      expect(transition.isSwitching.value).toBe(false);
      expect(wrapper.find(".player-motion-surface").exists()).toBe(false);
      expect(wrapper.element.style.height).toBe("");
    },
  );

  it("cancels and removes temporary layers when the player unmounts", async () => {
    const { wrapper, transition } = createPlayer();
    const finished = transition.toggle();
    await flush();
    wrapper.unmount();
    await finished;
    expect(pending.every((animation) => animation.cancel.mock.calls.length === 1)).toBe(true);
    expect(document.querySelector(".player-motion-surface")).toBeNull();
  });
});
