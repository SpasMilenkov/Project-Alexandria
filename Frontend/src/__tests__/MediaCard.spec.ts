import { enableAutoUnmount, shallowMount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { nextTick } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { attemptRefresh } from "@/api/client";
import MediaCard from "@/components/streaming/MediaCard.vue";

vi.mock("@/api/client", () => ({ attemptRefresh: vi.fn().mockResolvedValue(undefined) }));
vi.mock("@/stores/stream-player", async () => {
  const { defineStore } = await import("pinia");
  return {
    usePlayerStore: defineStore("test-player", {
      state: () => ({ activeFile: null, userQueue: [] }),
    }),
  };
});

describe("MediaCard grid keyboard actions", () => {
  const mountCard = () =>
    shallowMount(MediaCard, {
      props: { file: makeFile("first"), viewMode: "grid" },
      global: { plugins: [createPinia()], renderStubDefaultSlot: true },
    });

  it.each(["Enter", " "])("plays with %s and selects instead in selection mode", async (key) => {
    const wrapper = mountCard();
    const card = wrapper.get('[role="button"]');
    await card.trigger("keydown", { key });
    expect(wrapper.emitted("select")).toHaveLength(1);

    await wrapper.setProps({ selectionMode: true });
    await card.trigger("keydown", { key, shiftKey: true });
    expect(wrapper.emitted("select")).toHaveLength(1);
    expect(wrapper.emitted("toggle")?.[0]?.[1]).toMatchObject({ shiftKey: true });
  });

  it("keeps analysis button keyboard events and clicks separate from playback", async () => {
    const wrapper = mountCard();
    const analysis = wrapper.get('[aria-label="View audio analysis"]');
    await analysis.trigger("keydown", { key: "Enter" });
    await analysis.trigger("click");
    expect(wrapper.emitted("select")).toBeUndefined();
    expect(wrapper.emitted("info")).toHaveLength(1);
    expect(wrapper.find("button button").exists()).toBe(false);
  });
});

const makeFile = (fileId: string, currentVersionId = "version-1"): MediaFileDto => ({
  fileId,
  currentVersionId,
  fileName: `${fileId}.mp3`,
  mimeType: "audio/mpeg",
  duration: 180,
  artist: "Artist",
  album: null,
  title: fileId,
  genre: null,
  year: null,
  transpilationJobId: `job-${fileId}`,
  isVideo: false,
  segmentPrefix: null,
});

enableAutoUnmount(afterEach);

describe.each(["list", "grid"] as const)("MediaCard %s artwork", (viewMode) => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
  });

  afterEach(() => vi.useRealTimers());

  const mountCard = () =>
    shallowMount(MediaCard, {
      props: { file: makeFile("first"), viewMode },
      global: { plugins: [createPinia()], renderStubDefaultSlot: true },
    });

  it("replaces loaded artwork when the row is recycled and hides the pending replacement", async () => {
    const wrapper = mountCard();
    const cardInstance = wrapper.vm;
    const previous = wrapper.get("img");
    await previous.trigger("load");
    expect(previous.classes()).not.toContain("opacity-0");
    expect(wrapper.find('[aria-label="Loading artwork"]').exists()).toBe(false);

    await wrapper.setProps({ file: makeFile("second") });
    const current = wrapper.get("img");
    expect(wrapper.vm).toBe(cardInstance);
    expect(current.element).not.toBe(previous.element);
    expect(current.attributes("src")).toContain("/files/second/versions/version-1/thumbnail");
    expect(current.classes()).toContain("opacity-0");
    expect(wrapper.find('[aria-label="Loading artwork"]').exists()).toBe(true);

    await current.trigger("load");
    expect(current.classes()).not.toContain("opacity-0");
    expect(wrapper.find('[aria-label="Loading artwork"]').exists()).toBe(false);
  });

  it("replaces artwork for a new version but preserves it for a metadata-only edit", async () => {
    const wrapper = mountCard();
    const previous = wrapper.get("img");
    await previous.trigger("load");
    await wrapper.setProps({ file: { ...makeFile("first"), title: "Renamed" } });
    expect(wrapper.get("img").element).toBe(previous.element);
    expect(wrapper.get("img").classes()).not.toContain("opacity-0");

    await wrapper.setProps({ file: makeFile("first", "version-2") });
    expect(wrapper.get("img").element).not.toBe(previous.element);
    expect(wrapper.get("img").attributes("src")).toContain("/versions/version-2/thumbnail");
    expect(wrapper.get("img").classes()).toContain("opacity-0");
  });

  it("ignores late load and error events after rapid file changes", async () => {
    const wrapper = mountCard();
    const first = wrapper.get("img");
    await wrapper.setProps({ file: makeFile("second") });
    const second = wrapper.get("img");
    await wrapper.setProps({ file: makeFile("third") });

    await first.trigger("load");
    await second.trigger("error");
    expect(wrapper.get("img").classes()).toContain("opacity-0");
    expect(attemptRefresh).not.toHaveBeenCalled();
    vi.advanceTimersByTime(2_000);
    await nextTick();
    expect(wrapper.get("img").attributes("src")).toBe(
      "/api/files/third/versions/version-1/thumbnail",
    );

    await wrapper.get("img").trigger("load");
    expect(wrapper.get("img").classes()).not.toContain("opacity-0");
  });

  it("keeps retries and fallback working and resets a failed row for the next file", async () => {
    const wrapper = mountCard();
    for (const delay of [2_000, 5_000, 10_000, 30_000, 60_000]) {
      await wrapper.get("img").trigger("error");
      vi.advanceTimersByTime(delay);
      await nextTick();
    }
    expect(attemptRefresh).toHaveBeenCalledTimes(1);
    await wrapper.get("img").trigger("error");
    expect(wrapper.find("img").exists()).toBe(false);
    expect(wrapper.find('[aria-label="Loading artwork"]').exists()).toBe(false);

    await wrapper.setProps({ file: makeFile("second") });
    expect(wrapper.get("img").classes()).toContain("opacity-0");
    await wrapper.get("img").trigger("load");
    expect(wrapper.get("img").classes()).not.toContain("opacity-0");
  });
});
