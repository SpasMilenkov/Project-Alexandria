import { mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import HomePlayerWidget from "@/components/dashboard/home/widgets/HomePlayerWidget.vue";
import { HomePlayerStyle, HomeWidgetType } from "@/enums/home-widget";
import { useHomeStore } from "@/stores/home";
import { useSettingsStore } from "@/stores/settings";
import { usePlayerStore } from "@/stores/stream-player";
import { createHomeWidget, defaultHomeLayout } from "@/utils/home-layout";
import { homeWidgetMinimum } from "@/utils/home-widget-sizing";

const media = (isVideo = false): MediaFileDto => ({
  fileId: "media-a",
  fileName: "track.mp3",
  mimeType: isVideo ? "video/mp4" : "audio/mpeg",
  currentVersionId: "current-version",
  playbackVersionId: "playing-version",
  duration: 240,
  artist: "M83",
  album: "Hurry Up",
  title: "Midnight City",
  genre: null,
  year: null,
  transpilationJobId: "job-a",
  isVideo,
  segmentPrefix: null,
});

const Button = defineComponent({
  props: { disabled: Boolean, loading: Boolean },
  setup:
    (props, { slots }) =>
    () =>
      h("button", { disabled: props.disabled }, slots.default?.()),
});

const mountPlayer = (isVideo = false, empty = false) => {
  const pinia = createPinia();
  const home = useHomeStore(pinia);
  const player = usePlayerStore(pinia);
  const settings = useSettingsStore(pinia);
  home.setOwner("alice");
  home.hydrate("alice", {
    initialized: true,
    showWidgetTitles: false,
    showHeaderGreeting: true,
    useSeparateMobileLayout: false,
    desktop: defaultHomeLayout(),
    mobile: null,
  });
  player.playbackOwnerId = "alice";
  player.nowPlaying = empty
    ? null
    : { instanceId: 1, file: media(isVideo), origin: "queue", ended: false, restored: false };
  player.setEngineReady(true);
  player.setDuration(240);
  player.setCurrentTime(60);
  const engine = {
    play: vi.fn(),
    pause: vi.fn(),
    seek: vi.fn(),
    setVolume: vi.fn(),
    selectVariant: vi.fn(),
    setPlaybackRate: vi.fn(),
  };
  const dispose = player.registerEngine(isVideo ? "video" : "audio", engine);
  const wrapper = mount(HomePlayerWidget, {
    props: { widget: createHomeWidget(HomeWidgetType.Player, "player") },
    global: { plugins: [pinia], stubs: { Button, Icon: true } },
  });

  return { wrapper, pinia, player, home, settings, engine, dispose };
};

beforeEach(() => vi.stubEnv("VITE_STREAMING_ENABLED", "true"));

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllEnvs();
});

describe("Home Player uses the shared engine", () => {
  it("switches presentation without creating or interrupting playback engines", async () => {
    const { wrapper, engine, player, dispose } = mountPlayer();

    expect(wrapper.find(".record-disc").exists()).toBe(true);
    const widget = createHomeWidget(HomeWidgetType.Player, "player");
    widget.options.playerStyle = HomePlayerStyle.Traditional;

    await wrapper.setProps({ widget });

    expect(wrapper.find(".record-disc").exists()).toBe(false);
    expect(wrapper.get("img").attributes("src")).toContain("playing-version");
    expect(engine.pause).not.toHaveBeenCalled();
    expect(player.activeFile?.fileId).toBe("media-a");
    wrapper.unmount();
    dispose();
  });

  it.each([false, true])(
    "routes play, pause and seek to the existing engine (video: %s)",
    async (video) => {
      const { wrapper, player, engine, dispose } = mountPlayer(video);

      await wrapper.get('[aria-label="Play"]').trigger("click");

      expect(engine.play).toHaveBeenCalledOnce();
      player.setIsPlaying(true);
      await wrapper.vm.$nextTick();
      await wrapper.get('[aria-label="Pause"]').trigger("click");
      await wrapper.get('input[type="range"]').setValue(120);

      expect(engine.pause).toHaveBeenCalledOnce();
      expect(engine.seek).toHaveBeenCalledWith(120);
      expect(wrapper.find("video, audio").exists()).toBe(false);
      wrapper.unmount();
      dispose();
    },
  );

  it("dispatches next/previous through the existing navigation commands", async () => {
    const { wrapper, player, dispose } = mountPlayer();
    player.repeatMode = "all";
    player.setCurrentTime(10);
    const next = vi.spyOn(player, "next").mockResolvedValue(undefined);
    const previous = vi.spyOn(player, "previous").mockResolvedValue(undefined);

    await wrapper.vm.$nextTick();
    await wrapper.get('[aria-label="Next"]').trigger("click");
    await wrapper.get('[aria-label="Previous"]').trigger("click");

    expect(next).toHaveBeenCalledOnce();
    expect(previous).toHaveBeenCalledOnce();
    wrapper.unmount();
    dispose();
  });

  it("keeps live position shared across duplicate widgets without controlling playback on removal", async () => {
    const { wrapper, pinia, player, engine, dispose } = mountPlayer();
    const duplicate = mount(HomePlayerWidget, {
      props: { widget: createHomeWidget(HomeWidgetType.Player, "duplicate") },
      global: {
        plugins: [pinia],
        stubs: { Button, Icon: true },
      },
    });
    player.setCurrentTime(95);

    await wrapper.vm.$nextTick();

    expect(wrapper.get("input").attributes("aria-valuetext")).toBe("1:35 of 4:00");
    expect(duplicate.get("input").attributes("aria-valuetext")).toBe("1:35 of 4:00");
    wrapper.unmount();
    duplicate.unmount();
    expect(engine.pause).not.toHaveBeenCalled();
    expect(player.activeFile?.fileId).toBe("media-a");
    dispose();
  });

  it("makes the card inert during editing without pausing its engine", async () => {
    const { wrapper, home, player, engine, dispose } = mountPlayer();
    home.beginEditing("desktop");

    await wrapper.vm.$nextTick();
    await wrapper.get('[aria-label="Play"]').trigger("click");
    await wrapper.get("input").setValue(150);

    expect(wrapper.get('[aria-label="Play"]').attributes("disabled")).toBeDefined();
    expect(engine.play).not.toHaveBeenCalled();
    expect(engine.seek).not.toHaveBeenCalled();
    expect(engine.pause).not.toHaveBeenCalled();
    expect(player.activeFile?.fileId).toBe("media-a");
    wrapper.unmount();
    dispose();
  });

  it("uses playback-version artwork, follows preferences and clears it when the owner changes", async () => {
    const { wrapper, home, settings, dispose } = mountPlayer();

    expect(wrapper.get("img").attributes("src")).toContain("/versions/playing-version/thumbnail");
    await wrapper.get("img").trigger("load");
    expect(wrapper.get(".player-tint").attributes("style")).toContain("playing-version");
    settings.thumbnailsEnabled = false;

    await wrapper.vm.$nextTick();

    expect(wrapper.find("img").exists()).toBe(false);
    expect(wrapper.get(".player-tint").attributes("style")).toBeUndefined();
    home.setOwner("bob");

    await wrapper.vm.$nextTick();

    expect(wrapper.text()).toContain("Nothing playing");
    expect(wrapper.text()).not.toContain("Midnight City");
    wrapper.unmount();
    dispose();
  });

  it("handles unknown duration, buffering and engine errors without invalid progress or seek commands", async () => {
    const { wrapper, player, engine, dispose } = mountPlayer();
    player.setDuration(Number.NaN);
    player.setCurrentTime(Number.POSITIVE_INFINITY);
    player.setEngineBuffering(true);

    await wrapper.vm.$nextTick();

    expect(wrapper.get("input").attributes("disabled")).toBeDefined();
    expect(wrapper.get("input").attributes("aria-valuetext")).toBe("0:00 of unknown duration");
    player.setEngineLoadError("Playback unavailable");
    player.setEngineReady(false);

    await wrapper.vm.$nextTick();

    expect(wrapper.get('[role="alert"]').text()).toBe("Playback unavailable");
    expect(wrapper.get('[aria-label="Play"]').attributes("disabled")).toBeDefined();
    expect(engine.seek).not.toHaveBeenCalled();
    wrapper.unmount();
    dispose();
  });

  it("lets a ready engine retry playback after an autoplay rejection", async () => {
    const { wrapper, player, engine, dispose } = mountPlayer();
    player.setEngineLoadError("Playback was blocked. Press play to retry.");

    await wrapper.vm.$nextTick();
    await wrapper.get('[aria-label="Play"]').trigger("click");

    expect(engine.play).toHaveBeenCalledOnce();
    wrapper.unmount();
    dispose();
  });

  it("offers library links when empty and a noninteractive state when streaming is disabled", () => {
    const empty = mountPlayer(false, true);

    expect(empty.wrapper.text()).toContain("Nothing playing");
    expect(empty.wrapper.findAll("button").map((button) => button.attributes("to"))).toEqual([
      "/streaming/music",
      "/streaming/videos",
    ]);
    empty.wrapper.unmount();
    empty.dispose();
    vi.stubEnv("VITE_STREAMING_ENABLED", "false");
    const disabled = mountPlayer();

    expect(disabled.wrapper.text()).toContain("Streaming is unavailable");
    expect(disabled.wrapper.find("button").exists()).toBe(false);
    expect(homeWidgetMinimum(HomeWidgetType.Player, 12)).toEqual({ width: 2, height: 2 });
    expect(homeWidgetMinimum(HomeWidgetType.Player, 4)).toEqual({ width: 2, height: 2 });
    disabled.wrapper.unmount();
    disabled.dispose();
  });
});
