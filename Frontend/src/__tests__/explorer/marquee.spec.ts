import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { type EffectScope, effectScope, nextTick, ref } from "vue";

import { useExplorerMarquee } from "@/composables/useExplorerMarquee";

const rect = (left: number, top: number, width: number, height: number) =>
  ({
    bottom: top + height,
    height,
    left,
    right: left + width,
    top,
    width,
    x: left,
    y: top,
  }) as DOMRect;

const pointer = (type: string, x: number, y: number, init: PointerEventInit = {}) =>
  new PointerEvent(type, {
    bubbles: true,
    button: 0,
    clientX: x,
    clientY: y,
    isPrimary: true,
    pointerId: 1,
    pointerType: "mouse",
    ...init,
  });

const buildListing = () => {
  const container = document.createElement("div");

  container.getBoundingClientRect = () => rect(0, 0, 800, 600);
  Object.defineProperty(container, "clientWidth", { value: 800 });
  Object.defineProperty(container, "clientHeight", { value: 600 });

  const addItem = (
    attribute: "data-file-id" | "data-dir-id",
    id: string,
    left: number,
    top: number,
  ) => {
    const element = document.createElement("div");

    element.setAttribute(attribute, id);
    element.getBoundingClientRect = () => rect(left, top, 100, 40);
    container.append(element);
  };

  addItem("data-dir-id", "d1", 20, 20);
  addItem("data-file-id", "f1", 20, 100);
  addItem("data-file-id", "f2", 300, 100);
  document.body.append(container);

  return container;
};

describe("useExplorerMarquee", () => {
  const state: { scope: EffectScope | null; container: HTMLElement } = {
    container: document.createElement("div"),
    scope: null,
  };

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["requestAnimationFrame", "cancelAnimationFrame", "setTimeout"] });
    state.container = buildListing();
  });

  afterEach(() => {
    state.scope?.stop();
    state.container.remove();
    vi.useRealTimers();
  });

  const setup = (initialFiles: string[] = []) => {
    const selectedFiles = ref(new Set(initialFiles));
    const selectedDirectories = ref(new Set<string>());

    const scope = effectScope();

    state.scope = scope;

    const marquee = scope.run(() =>
      useExplorerMarquee({ container: ref(state.container), selectedDirectories, selectedFiles }),
    )!;

    return { marquee, selectedDirectories, selectedFiles };
  };

  it("selects items crossed by the rectangle once the pointer moves", async () => {
    const { marquee, selectedDirectories, selectedFiles } = setup();
    await nextTick();

    state.container.dispatchEvent(pointer("pointerdown", 5, 5));
    window.dispatchEvent(pointer("pointermove", 7, 6));
    expect(marquee.isSelecting.value).toBe(false);

    window.dispatchEvent(pointer("pointermove", 150, 130));
    vi.advanceTimersToNextFrame();

    expect(marquee.isSelecting.value).toBe(true);
    expect(marquee.marqueeBox.value).toEqual({ bottom: 130, left: 5, right: 150, top: 5 });
    expect([...selectedDirectories.value]).toEqual(["d1"]);
    expect([...selectedFiles.value]).toEqual(["f1"]);

    window.dispatchEvent(pointer("pointerup", 150, 130));
    expect(marquee.marqueeBox.value).toBeNull();
    expect([...selectedFiles.value]).toEqual(["f1"]);
  });

  it("keeps the previous selection with ctrl and restores it on Escape", async () => {
    const { selectedFiles } = setup(["f2"]);
    await nextTick();

    state.container.dispatchEvent(pointer("pointerdown", 5, 90, { ctrlKey: true }));
    window.dispatchEvent(pointer("pointermove", 150, 150));
    vi.advanceTimersToNextFrame();
    expect([...selectedFiles.value].sort()).toEqual(["f1", "f2"]);

    window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape" }));
    expect([...selectedFiles.value]).toEqual(["f2"]);
  });

  it("ignores presses that start on an item or come from touch", async () => {
    const { marquee } = setup();
    await nextTick();

    state.container.querySelector("[data-file-id]")!.dispatchEvent(pointer("pointerdown", 30, 110));
    window.dispatchEvent(pointer("pointermove", 200, 200));
    vi.advanceTimersToNextFrame();
    expect(marquee.isSelecting.value).toBe(false);

    state.container.dispatchEvent(pointer("pointerdown", 5, 5, { pointerType: "touch" }));
    window.dispatchEvent(pointer("pointermove", 200, 200, { pointerType: "touch" }));
    vi.advanceTimersToNextFrame();
    expect(marquee.isSelecting.value).toBe(false);
  });
});
