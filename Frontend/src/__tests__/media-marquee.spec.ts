import { flushPromises, mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";

import MediaMarquee from "@/components/streaming/overview/MediaMarquee.vue";

const stubWidths = (scrollWidth: number, clientWidth: number) => {
  vi.spyOn(HTMLElement.prototype, "scrollWidth", "get").mockReturnValue(scrollWidth);
  vi.spyOn(HTMLElement.prototype, "clientWidth", "get").mockReturnValue(clientWidth);
};

afterEach(() => {
  vi.restoreAllMocks();
});

describe("MediaMarquee", () => {
  it("scrolls an overflowing title by its overflow", async () => {
    stubWidths(500, 300);
    const wrapper = mount(MediaMarquee, { props: { text: "A very long title" } });
    await flushPromises();

    const inner = wrapper.get("span > span");
    expect(inner.classes()).toContain("marquee");
    expect(inner.attributes("style")).toContain("--marquee-distance: -200px");
    expect(inner.attributes("style")).toContain("animation-delay: -1.35s");
    expect(wrapper.get("span").attributes("title")).toBe("A very long title");
    wrapper.unmount();
  });

  it("stops and resets to the start while hovered", async () => {
    stubWidths(500, 300);
    const wrapper = mount(MediaMarquee, { props: { text: "A very long title" } });
    await flushPromises();

    await wrapper.get("span").trigger("mouseenter");
    await flushPromises();
    expect(wrapper.find(".marquee").exists()).toBe(false);
    expect(wrapper.get("span").classes()).toContain("text-ellipsis");

    await wrapper.get("span").trigger("mouseleave");
    await flushPromises();
    expect(wrapper.find(".marquee").exists()).toBe(true);
    wrapper.unmount();
  });

  it("keeps a title that fits still", async () => {
    stubWidths(200, 300);
    const wrapper = mount(MediaMarquee, { props: { text: "Short" } });
    await flushPromises();

    expect(wrapper.find(".marquee").exists()).toBe(false);
    wrapper.unmount();
  });

  it("falls back to an ellipsis when reduced motion is requested", async () => {
    stubWidths(500, 300);
    vi.stubGlobal(
      "matchMedia",
      vi.fn((query: string) => ({
        matches: query.includes("reduce"),
        media: query,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      })),
    );
    const wrapper = mount(MediaMarquee, { props: { text: "A very long title" } });
    await flushPromises();

    expect(wrapper.find(".marquee").exists()).toBe(false);
    expect(wrapper.get("span").classes()).toContain("text-ellipsis");
    wrapper.unmount();
    vi.unstubAllGlobals();
  });
});
