import { mount } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, nextTick, ref } from "vue";

import { useHomeGridMotion } from "@/composables/useHomeGridMotion";

interface PendingAnimation {
  element: HTMLElement;
  frames: Keyframe[];
  cancel: ReturnType<typeof vi.fn>;
  onfinish: (() => void) | null;
}

const wrappers: ReturnType<typeof mount>[] = [];
let pending: PendingAnimation[] = [];
let reduced = false;
let preferenceChange: ((event: { matches: boolean }) => void) | undefined = undefined;
let boardTop = 100;
let visible: DOMRect | null = null;

const createGrid = () => {
  const enabled = ref(true);
  const context = ref("desktop:12");
  const position = ref("0:0:6:4");
  const hidden = ref(false);
  const ghost = ref<string | null>(null);
  const tick = ref(0);
  const width = ref(1200);
  const wrapper = mount(
    defineComponent({
      setup: () => {
        const board = ref<HTMLElement | null>(null);
        useHomeGridMotion({
          board: () => board.value,
          enabled: () => enabled.value,
          context: () => context.value,
          active: () => Boolean(ghost.value),
        });

        return () =>
          h("div", { ref: board, "data-board-width": width.value, "data-tick": tick.value }, [
            h("div", {
              "data-home-motion": "clock",
              "data-motion-position": position.value,
              "data-motion-hidden": hidden.value,
            }),
            ghost.value &&
              h("div", {
                "data-home-motion": "placement",
                "data-motion-widget": "clock",
                "data-motion-position": ghost.value,
              }),
          ]);
      },
    }),
    { attachTo: document.body },
  );
  wrappers.push(wrapper);

  return { wrapper, enabled, context, position, hidden, ghost, tick, width };
};

beforeEach(() => {
  pending = [];
  reduced = false;
  boardTop = 100;
  visible = null;
  preferenceChange = undefined;
  vi.stubGlobal(
    "ResizeObserver",
    class {
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
      media: "(prefers-reduced-motion: reduce)",
      addEventListener: (_: string, callback: (event: { matches: boolean }) => void) => {
        preferenceChange = callback;
      },
      removeEventListener: vi.fn(),
    })),
  );
  vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(
    function rectangleStub() {
      if (this.dataset.boardWidth)
        return new DOMRect(0, boardTop, Number(this.dataset.boardWidth), 1000);
      if (visible && this.dataset.homeMotion === "clock") return visible;

      const [x = 0, y = 0, width = 0, height = 0] = (this.dataset.motionPosition ?? "")
        .split(":")
        .map(Number);

      return new DOMRect(x * 100, boardTop + y * 80, width * 100 - 16, height * 80 - 16);
    },
  );
  Object.defineProperty(HTMLElement.prototype, "animate", {
    configurable: true,
    value: vi.fn(function animateStub(this: HTMLElement, frames: Keyframe[]) {
      const animation: PendingAnimation = {
        element: this,
        frames,
        cancel: vi.fn(() => {
          visible = null;
        }),
        onfinish: null,
      };
      pending.push(animation);

      return animation;
    }),
  });
});

afterEach(() => {
  wrappers.splice(0).forEach((wrapper) => wrapper.unmount());
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  Reflect.deleteProperty(HTMLElement.prototype, "animate");
});

describe("Home grid motion", () => {
  it("retargets from the visible rectangle and releases finished effects", async () => {
    const state = createGrid();
    state.position.value = "3:0:8:4";
    await nextTick();
    const first = pending[0]!;
    visible = new DOMRect(150, 100, 684, 304);
    state.position.value = "6:0:9:4";
    await nextTick();

    expect(first.cancel).toHaveBeenCalledOnce();
    expect(pending[1]!.frames[0]).toMatchObject({
      transform: "translate(-450px, 0px)",
      width: "684px",
      height: "304px",
    });
    first.onfinish!();
    expect(pending[1]!.cancel).not.toHaveBeenCalled();
    pending[1]!.onfinish!();
    expect(pending[1]!.cancel).toHaveBeenCalledOnce();
  });

  it("does not restart movement for unrelated updates or board scrolling", async () => {
    const state = createGrid();
    state.position.value = "3:0:6:4";
    await nextTick();
    boardTop = -100;
    state.tick.value++;
    await nextTick();

    expect(pending).toHaveLength(1);
    expect(pending[0]!.cancel).not.toHaveBeenCalled();
  });

  it("restores a dropped card from its placement outline", async () => {
    const state = createGrid();
    state.hidden.value = true;
    state.ghost.value = "3:0:6:4";
    await nextTick();
    state.hidden.value = false;
    state.ghost.value = null;
    state.position.value = "6:0:6:4";
    await nextTick();

    expect(pending[0]!.frames[0]!.transform).toBe("translate(-300px, 0px)");
  });

  it("disables and cancels motion when the system preference changes", async () => {
    const state = createGrid();
    state.position.value = "3:0:6:4";
    await nextTick();
    reduced = true;
    preferenceChange!({ matches: true });
    state.position.value = "6:0:6:4";
    await nextTick();

    expect(pending[0]!.cancel).toHaveBeenCalledOnce();
    expect(pending).toHaveLength(1);
  });

  it.each(["enabled", "context", "width", "unmount"])(
    "cleans up active effects on %s changes",
    async (change) => {
      const state = createGrid();
      state.position.value = "3:0:6:4";
      await nextTick();
      if (change === "enabled") state.enabled.value = false;
      else if (change === "context") {
        state.context.value = "mobile:4";
        state.position.value = "0:4:4:6";
      } else if (change === "width") state.width.value = 900;
      else state.wrapper.unmount();
      await nextTick();

      expect(pending[0]!.cancel).toHaveBeenCalledOnce();
      expect(pending).toHaveLength(1);
    },
  );
});
