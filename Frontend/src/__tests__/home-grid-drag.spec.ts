import { afterEach, describe, expect, it, vi } from "vitest";
import { createApp, defineComponent, nextTick, ref } from "vue";

import type { HomeResizeEdge } from "@/utils/home-grid-input";

import { useHomeGridDrag } from "@/composables/useHomeGridDrag";
import { defaultHomeLayout } from "@/utils/home-layout";

const pointer = (type: string, x: number, y: number) => {
  const event = new MouseEvent(type, { bubbles: true, cancelable: true, clientX: x, clientY: y });
  Object.defineProperties(event, { pointerId: { value: 1 }, isPrimary: { value: true } });

  return event as PointerEvent;
};

const mountDrag = (edge: HomeResizeEdge | null = null, initial = defaultHomeLayout()) => {
  const layout = ref(initial);
  const enabled = ref(true);
  const board = document.createElement("div");
  document.body.appendChild(board);
  board.getBoundingClientRect = () =>
    ({ left: 0, top: 0, right: 1200, bottom: 800, width: 1200, height: 800 }) as DOMRect;
  const place = vi.fn();
  const announce = vi.fn();
  let drag = null as ReturnType<typeof useHomeGridDrag> | null;
  const app = createApp(
    defineComponent({
      setup: () => {
        drag = useHomeGridDrag({
          board: () => board,
          layout: () => layout.value,
          enabled: () => enabled.value,
          select: vi.fn(),
          place,
          announce,
        });

        return () => null;
      },
    }),
  );
  app.mount(board);
  const begin = pointer("pointerdown", 20, 20);
  Object.defineProperty(begin, "currentTarget", { value: board });
  if (edge) drag!.startResize(layout.value.widgets[0]!, edge, begin);
  else drag!.start(layout.value.widgets[0]!, begin);

  return {
    drag: drag!,
    enabled,
    layout,
    place,
    announce,
    close: () => {
      app.unmount();
      board.remove();
    },
  };
};

describe("Home grid drag draft boundary", () => {
  const cleanups: (() => void)[] = [];
  afterEach(() => cleanups.splice(0).forEach((close) => close()));

  it("previews pointer movement without writes and commits only on a valid release", () => {
    const state = mountDrag();
    cleanups.push(state.close);
    window.dispatchEvent(pointer("pointermove", 628, 20));

    expect(state.drag.preview.value).not.toBeNull();
    expect(state.layout.value.widgets[0]!.x).toBe(0);
    expect(state.place).not.toHaveBeenCalled();
    window.dispatchEvent(pointer("pointerup", 628, 20));
    expect(state.place).toHaveBeenCalledWith(expect.objectContaining({ x: 6, y: 0 }));
    expect(state.drag.active.value).toBe(false);
  });

  it.each(["pointercancel", "blur"])("cancels %s without applying a preview", (event) => {
    const state = mountDrag();
    cleanups.push(state.close);
    window.dispatchEvent(pointer("pointermove", 628, 20));
    window.dispatchEvent(new Event(event));
    window.dispatchEvent(pointer("pointerup", 628, 20));

    expect(state.place).not.toHaveBeenCalled();
    expect(state.drag.active.value).toBe(false);
  });

  it("Escape cancels placement before an exit handler can see it", () => {
    const state = mountDrag();
    cleanups.push(state.close);
    window.dispatchEvent(pointer("pointermove", 628, 20));
    const escape = new KeyboardEvent("keydown", { key: "Escape", cancelable: true });
    window.dispatchEvent(escape);
    window.dispatchEvent(pointer("pointerup", 628, 20));

    expect(escape.defaultPrevented).toBe(true);
    expect(state.place).not.toHaveBeenCalled();
    expect(state.announce).toHaveBeenCalledWith("Placement cancelled.");
  });

  it("rejects a release outside the visible canvas", () => {
    const state = mountDrag();
    cleanups.push(state.close);
    window.dispatchEvent(pointer("pointermove", 628, 20));
    window.dispatchEvent(pointer("pointerup", -20, 20));

    expect(state.place).not.toHaveBeenCalled();
    expect(state.announce).toHaveBeenCalledWith(expect.stringContaining("does not fit"));
  });

  it("disabling editing cancels an active drag", async () => {
    const state = mountDrag();
    cleanups.push(state.close);
    window.dispatchEvent(pointer("pointermove", 628, 20));
    state.enabled.value = false;
    await nextTick();
    window.dispatchEvent(pointer("pointerup", 628, 20));

    expect(state.place).not.toHaveBeenCalled();
  });
  it("resizing previews content and collisions without changing the draft before release", () => {
    const state = mountDrag("bottom");
    cleanups.push(state.close);
    window.dispatchEvent(pointer("pointermove", 20, 180));

    expect(state.drag.resizing.value).toBe(true);
    expect(state.drag.preview.value!.widgets[0]!.height).toBe(6);
    expect(state.drag.preview.value!.widgets[1]!.y).toBe(6);
    expect(state.layout.value.widgets[0]!.height).toBe(4);
    expect(state.layout.value.widgets[1]!.y).toBe(4);
    window.dispatchEvent(pointer("pointerup", 20, 180));
    expect(state.place).toHaveBeenCalledWith(expect.objectContaining({ height: 6 }));
  });

  it.each(["pointercancel", "blur", "resize", "Escape"])(
    "cancels a resize on %s without committing",
    (event) => {
      const state = mountDrag("right");
      cleanups.push(state.close);
      window.dispatchEvent(pointer("pointermove", 120, 20));
      if (event === "Escape") window.dispatchEvent(new KeyboardEvent("keydown", { key: event }));
      else window.dispatchEvent(new Event(event));
      window.dispatchEvent(pointer("pointerup", 120, 20));

      expect(state.place).not.toHaveBeenCalled();
      expect(state.drag.active.value).toBe(false);
      expect(state.layout.value.widgets[0]!.width).toBe(6);
    },
  );

  it("rejects a resize whose collision cascade overflows the canvas", () => {
    const initial = defaultHomeLayout();
    initial.widgets[0]!.y = 250;
    initial.widgets[1]!.y = 254;
    initial.widgets[1]!.height = 2;
    const state = mountDrag("bottom", initial);
    cleanups.push(state.close);
    window.dispatchEvent(pointer("pointermove", 20, 100));

    expect(state.drag.preview.value).toBeNull();
    window.dispatchEvent(pointer("pointerup", 20, 100));
    expect(state.place).not.toHaveBeenCalled();
    expect(state.layout.value.widgets[0]!.height).toBe(4);
    expect(state.layout.value.widgets[1]!.y).toBe(254);
  });
});
