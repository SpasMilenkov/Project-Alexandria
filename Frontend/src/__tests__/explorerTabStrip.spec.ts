import { mount } from "@vue/test-utils";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import ExplorerTabStrip from "@/components/dashboard/file-system/ExplorerTabStrip.vue";

const tabs = ["a", "b", "c"].map((id) => ({ activeDirId: null, id, title: `Folder ${id}` }));
const frames = new Map<number, FrameRequestCallback>();
const disconnect = vi.fn();
const observe = vi.fn();
let frameId = 0;

const flushFrames = () => {
  const callbacks = [...frames.values()];
  frames.clear();
  callbacks.forEach((callback) => callback(0));
};

const mountStrip = () =>
  mount(ExplorerTabStrip, {
    attachTo: document.body,
    global: { stubs: { Button: { template: "<button><slot /></button>" } } },
    props: { activeTabId: "b", panelId: "explorer-panel", tabs },
  });

beforeEach(() => {
  frames.clear();
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
    frameId += 1;
    frames.set(frameId, callback);
    return frameId;
  });
  vi.stubGlobal("cancelAnimationFrame", (id: number) => frames.delete(id));
  vi.stubGlobal(
    "ResizeObserver",
    class {
      disconnect = disconnect;
      observe = observe;
      unobserve = vi.fn();
    },
  );
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.clearAllMocks();
});

describe("ExplorerTabStrip keyboard and ownership", () => {
  it("uses sibling controls, one roving tab stop and an active panel relationship", () => {
    const wrapper = mountStrip();
    expect(wrapper.findAll('button[role="tab"][tabindex="0"]')).toHaveLength(1);
    expect(wrapper.get('[aria-selected="true"]').attributes("aria-controls")).toBe(
      "explorer-panel",
    );
    expect(wrapper.findAll("button button")).toHaveLength(0);
    expect(wrapper.get('[data-tab-id="a"] button[title="Close tab"]').attributes("tabindex")).toBe(
      "-1",
    );
    expect(wrapper.get('[data-tab-id="b"] button[title="Close tab"]').attributes("tabindex")).toBe(
      "0",
    );
    wrapper.unmount();
  });

  it.each([
    ["ArrowLeft", "a"],
    ["ArrowRight", "c"],
    ["Home", "a"],
    ["End", "c"],
  ])("%s focuses and activates %s without native scrolling", async (key, id) => {
    const wrapper = mountStrip();
    const button = wrapper.get(`[data-tab-id="${id}"] [role="tab"]`).element as HTMLButtonElement;
    const focus = vi.spyOn(button, "focus");
    await wrapper.get('[aria-selected="true"]').trigger("keydown", { key });
    expect(wrapper.emitted("activate")).toEqual([[id]]);
    expect(focus).toHaveBeenCalledWith({ preventScroll: true });
    wrapper.unmount();
  });

  it("wraps from the last tab to the first", async () => {
    const wrapper = mountStrip();
    await wrapper.get('[data-tab-id="c"] [role="tab"]').trigger("keydown", { key: "ArrowRight" });
    expect(wrapper.emitted("activate")).toEqual([["a"]]);
    wrapper.unmount();
  });

  it("consumes Delete before explorer commands and closes only the focused tab", () => {
    const wrapper = mountStrip();
    const listener = vi.fn();
    window.addEventListener("keydown", listener);
    const event = new KeyboardEvent("keydown", { bubbles: true, cancelable: true, key: "Delete" });
    wrapper.get('[data-tab-id="a"] [role="tab"]').element.dispatchEvent(event);
    expect(wrapper.emitted("close")).toEqual([["a"]]);
    expect(wrapper.emitted("activate")).toBeUndefined();
    expect(event.defaultPrevented).toBe(true);
    expect(listener).not.toHaveBeenCalled();
    window.removeEventListener("keydown", listener);
    wrapper.unmount();
  });

  it("keeps the only tab on Delete and hides close controls", async () => {
    const wrapper = mountStrip();
    await wrapper.setProps({ activeTabId: "a", tabs: tabs.slice(0, 1) });
    await wrapper.get('[role="tab"]').trigger("keydown", { key: "Delete" });
    expect(wrapper.emitted("close")).toBeUndefined();
    expect(wrapper.find('[title="Close tab"]').exists()).toBe(false);
    wrapper.unmount();
  });

  it("restores focus after removing its tab but does not steal focus on title changes", async () => {
    const wrapper = mountStrip();
    (wrapper.get('[data-tab-id="b"] button[title="Close tab"]').element as HTMLElement).focus();
    await wrapper.setProps({ activeTabId: "a", tabs: tabs.filter((tab) => tab.id !== "b") });
    flushFrames();
    expect(document.activeElement).toBe(wrapper.get('[data-tab-id="a"] [role="tab"]').element);
    const input = document.createElement("input");
    document.body.append(input);
    input.focus();
    await wrapper.setProps({ tabs: [{ ...tabs[0]!, title: "Renamed" }, tabs[2]!] });
    flushFrames();
    expect(document.activeElement).toBe(input);
    input.remove();
    wrapper.unmount();
  });

  it.each([1, 2])("reveals horizontally at %sx scale without vertical scrolling", (scale) => {
    const wrapper = mountStrip();
    const viewport = wrapper.get("[data-tab-viewport]").element as HTMLElement;
    const group = wrapper.get('[data-tab-id="b"]').element;
    vi.spyOn(viewport, "getBoundingClientRect").mockReturnValue({
      left: 100 * scale,
      width: 400 * scale,
    } as DOMRect);
    vi.spyOn(group, "getBoundingClientRect").mockReturnValue({
      left: 450 * scale,
      right: 642 * scale,
    } as DOMRect);
    Object.defineProperty(viewport, "clientWidth", { value: 400 });
    Object.defineProperty(viewport, "offsetWidth", { value: 400 });
    viewport.scrollTop = 24;
    flushFrames();
    expect(viewport.scrollLeft).toBe(142);
    expect(viewport.scrollTop).toBe(24);
    wrapper.unmount();
  });

  it("disconnects its observer and cancels queued frames on disposal", async () => {
    const wrapper = mountStrip();
    await wrapper.vm.$nextTick();
    expect(frames.size).toBe(1);
    expect(observe).toHaveBeenCalledTimes(2);
    wrapper.unmount();
    expect(disconnect).toHaveBeenCalled();
    expect(frames.size).toBe(0);
  });
});
