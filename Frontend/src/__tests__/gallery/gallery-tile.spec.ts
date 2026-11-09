import { mount } from "@vue/test-utils";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { GalleryPhoto } from "@/api/gallery";

import GalleryTile from "@/components/gallery/GalleryTile.vue";

const photo: GalleryPhoto = {
  fileId: "file-1",
  height: 3000,
  mimeType: "image/jpeg",
  name: "IMG_0001.jpg",
  takenAt: "2019-06-12T18:15:42.000Z",
  uploadedAt: "2026-11-08T10:00:00.000Z",
  versionId: "version-1",
  width: 4000,
};

const stubImage = (complete: boolean, naturalWidth: number) => {
  vi.spyOn(HTMLImageElement.prototype, "complete", "get").mockReturnValue(complete);
  vi.spyOn(HTMLImageElement.prototype, "naturalWidth", "get").mockReturnValue(naturalWidth);
};

const mountTile = (state: { selected?: boolean; selecting?: boolean } = {}) =>
  mount(GalleryTile, {
    global: { stubs: { UIcon: true } },
    props: {
      height: 150,
      photo,
      selected: state.selected ?? false,
      selecting: state.selecting ?? false,
      width: 200,
    },
  });

const photoButton = (wrapper: ReturnType<typeof mountTile>) =>
  wrapper.get(`button[aria-label="${photo.name}"]`);

// The test DOM has no PointerEvent, so a mouse event stands in with the pointer type attached.
const touch = async (
  wrapper: ReturnType<typeof mountTile>,
  type: string,
  at: { x: number; y: number } = { x: 10, y: 10 },
) => {
  const event = new MouseEvent(type, { bubbles: true, clientX: at.x, clientY: at.y });
  Object.defineProperty(event, "pointerType", { value: "touch" });

  photoButton(wrapper).element.dispatchEvent(event);
  await wrapper.vm.$nextTick();
};

afterEach(() => {
  vi.restoreAllMocks();
});

describe("GalleryTile", () => {
  it("shows an image the browser already holds without fading it in", async () => {
    stubImage(true, 320);

    const wrapper = mountTile();
    await wrapper.vm.$nextTick();
    const classes = wrapper.get("img").classes();

    expect(classes).not.toContain("opacity-0");
    expect(classes).not.toContain("transition-opacity");
    wrapper.unmount();
  });

  it("fades in an image that is still loading once it arrives", async () => {
    stubImage(false, 0);

    const wrapper = mountTile();
    await wrapper.vm.$nextTick();

    expect(wrapper.get("img").classes()).toEqual(
      expect.arrayContaining(["opacity-0", "transition-opacity"]),
    );

    await wrapper.get("img").trigger("load");

    expect(wrapper.get("img").classes()).not.toContain("opacity-0");
    expect(wrapper.get("img").classes()).toContain("transition-opacity");
    wrapper.unmount();
  });

  it("opens the photo on a plain click", async () => {
    stubImage(true, 320);
    const wrapper = mountTile();

    await photoButton(wrapper).trigger("click");

    expect(wrapper.emitted("open")).toEqual([[photo]]);
    expect(wrapper.emitted("pick")).toBeUndefined();
    wrapper.unmount();
  });

  it("starts selecting on a ctrl-click instead of opening", async () => {
    stubImage(true, 320);
    const wrapper = mountTile();

    await photoButton(wrapper).trigger("click", { ctrlKey: true });

    expect(wrapper.emitted("pick")).toEqual([[photo, "toggle"]]);
    expect(wrapper.emitted("open")).toBeUndefined();
    wrapper.unmount();
  });

  it("narrows to just this photo on a plain mouse click while selecting", async () => {
    stubImage(true, 320);
    const wrapper = mountTile({ selecting: true });

    await photoButton(wrapper).trigger("click");

    expect(wrapper.emitted("pick")).toEqual([[photo, "only"]]);
    wrapper.unmount();
  });

  it("asks for a range on shift and an added range on ctrl+shift", async () => {
    stubImage(true, 320);
    const wrapper = mountTile({ selecting: true });

    await photoButton(wrapper).trigger("click", { shiftKey: true });
    await photoButton(wrapper).trigger("click", { ctrlKey: true, shiftKey: true });

    expect(wrapper.emitted("pick")).toEqual([
      [photo, "range"],
      [photo, "add-range"],
    ]);
    wrapper.unmount();
  });

  it("starts selecting on a long press without opening the photo", async () => {
    vi.useFakeTimers();
    stubImage(true, 320);
    const wrapper = mountTile();

    await touch(wrapper, "pointerdown");
    vi.advanceTimersByTime(500);
    await touch(wrapper, "pointerup");
    await photoButton(wrapper).trigger("click");

    expect(wrapper.emitted("pick")).toEqual([[photo, "toggle"]]);
    expect(wrapper.emitted("open")).toBeUndefined();
    vi.useRealTimers();
    wrapper.unmount();
  });

  it("toggles on a tap while selecting, since touch has no modifier keys", async () => {
    vi.useFakeTimers();
    stubImage(true, 320);
    const wrapper = mountTile({ selecting: true });

    await touch(wrapper, "pointerdown");
    await touch(wrapper, "pointerup");
    await photoButton(wrapper).trigger("click");

    expect(wrapper.emitted("pick")).toEqual([[photo, "toggle"]]);
    vi.useRealTimers();
    wrapper.unmount();
  });

  it("treats a finger that moves as a scroll, not a press", async () => {
    vi.useFakeTimers();
    stubImage(true, 320);
    const wrapper = mountTile();

    await touch(wrapper, "pointerdown");
    await touch(wrapper, "pointermove", { x: 10, y: 60 });
    vi.advanceTimersByTime(500);

    expect(wrapper.emitted("pick")).toBeUndefined();
    vi.useRealTimers();
    wrapper.unmount();
  });

  it("only adds or removes from the check, never narrowing to one", async () => {
    stubImage(true, 320);
    const wrapper = mountTile({ selecting: true });
    const check = wrapper.get(`button[aria-label="Select ${photo.name}"]`);

    await check.trigger("click");
    await check.trigger("click", { shiftKey: true });

    expect(wrapper.emitted("pick")).toEqual([
      [photo, "toggle"],
      [photo, "range"],
    ]);
    expect(wrapper.emitted("open")).toBeUndefined();
    wrapper.unmount();
  });
});
