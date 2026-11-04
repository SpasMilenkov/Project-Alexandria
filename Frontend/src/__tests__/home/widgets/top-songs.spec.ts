import { PiniaColada } from "@pinia/colada";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, ref } from "vue";

import type { TopSong, TopSongsQuery, TopSongsResponse } from "@/api/listening-rankings";

import HomeTopSongsWidget from "@/components/dashboard/home/widgets/HomeTopSongsWidget.vue";
import { OnboardingStep } from "@/enums";
import { HomeWidgetType } from "@/enums/home-widget";
import { homeTopSongs } from "@/queries/home-top-songs";
import { useAuthStore } from "@/stores/auth";
import { useHomeStore } from "@/stores/home";
import { useSettingsStore } from "@/stores/settings";
import { usePlayerStore } from "@/stores/stream-player";
import { createHomeWidget, defaultHomeLayout } from "@/utils/home-layout";
import { homeTopSongBar, homeTopSongTitle, homeTopSongsPeriod } from "@/utils/home-top-songs";

const api = vi.hoisted(() => ({ getTopSongs: vi.fn() }));

vi.mock("@/api/listening-rankings", () => ({ listeningRankingsApi: api }));
vi.mock("@/composables/useHomeWidgetRows", () => ({ useHomeWidgetRows: () => ref(5) }));

const song = (id = "a", plays = 12): TopSong => ({
  file: {
    fileId: id,
    fileName: `track-${id}.mp3`,
    mimeType: "audio/mpeg",
    currentVersionId: "current-version",
    playbackVersionId: "playing-version",
    duration: 240,
    artist: "M83",
    album: "Hurry Up",
    title: `Midnight City ${id}`,
    genre: null,
    year: null,
    transpilationJobId: `job-${id}`,
    isVideo: false,
    segmentPrefix: null,
  },
  qualifiedPlayCount: plays,
  totalListenedSeconds: plays * 240,
});

const result = (items: TopSong[], range: TopSongsQuery): TopSongsResponse => ({
  from: range.from,
  to: range.to,
  items,
});

const Button = defineComponent({
  props: { disabled: Boolean },
  setup:
    (props, { slots }) =>
    () =>
      h("button", { disabled: props.disabled }, slots.default?.()),
});

const mountWidget = () => {
  const pinia = createPinia();
  const home = useHomeStore(pinia);
  const auth = useAuthStore(pinia);
  auth.user = {
    success: true,
    user: { id: "alice", name: "alice", email: "alice@example.test" },
    userRoles: ["User"],
    onboardingStep: OnboardingStep.Done,
  };
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

  const wrapper = mount(HomeTopSongsWidget, {
    props: { widget: createHomeWidget(HomeWidgetType.TopSongs, "songs") },
    global: { plugins: [pinia, PiniaColada], stubs: { Button, UIcon: true } },
  });

  return { wrapper, home, player, settings, auth, pinia };
};

const cleanups: (() => void)[] = [];

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date("2026-10-08T03:12:45.678Z"));
  vi.stubEnv("VITE_STREAMING_ENABLED", "true");
  api.getTopSongs
    .mockReset()
    .mockImplementation((range: TopSongsQuery) =>
      Promise.resolve(result([song(), song("b", 3)], range)),
    );
});

afterEach(() => {
  cleanups.splice(0).forEach((cleanup) => cleanup());
  vi.restoreAllMocks();
  vi.useRealTimers();
  vi.unstubAllEnvs();
});

describe("Home Top songs period and ranking", () => {
  it("uses seven UTC calendar days including today without querying future hours", () => {
    expect(homeTopSongsPeriod(new Date("2026-10-08T03:12:45.678Z"))).toEqual({
      from: "2026-10-02T00:00:00.000Z",
      to: "2026-10-08T03:12:00.000Z",
      limit: 5,
    });
    expect(homeTopSongsPeriod(new Date("2026-01-03T00:00:00Z")).from).toBe(
      "2025-12-28T00:00:00.000Z",
    );
  });

  it("keeps bars relative to the leading play count and falls back to the file name", () => {
    expect(homeTopSongBar(song("a", 3), 12)).toBe(25);
    expect(homeTopSongBar(song("a", 12), 0)).toBe(0);
    expect(homeTopSongBar(song("a", 12), Number.NaN)).toBe(0);

    const unnamed = song();
    unnamed.file.title = " ";
    expect(homeTopSongTitle(unnamed)).toBe("track-a.mp3");
  });

  it("isolates cached results by owner, authentication epoch and captured period", () => {
    const range = homeTopSongsPeriod(new Date("2026-10-08T03:00:00Z"));
    const first = homeTopSongs({ userId: "alice", epoch: 1, ...range });

    expect(first.key).not.toEqual(homeTopSongs({ userId: "bob", epoch: 1, ...range }).key);
    expect(first.key).not.toEqual(homeTopSongs({ userId: "alice", epoch: 2, ...range }).key);
    expect(homeTopSongs({ userId: "", epoch: 1, ...range }).enabled).toBe(false);
  });
});

describe("Home Top songs shared playback", () => {
  it("shows ranked plays and dispatches a row through the existing player command", async () => {
    const { wrapper, player } = mountWidget();
    cleanups.push(() => wrapper.unmount());
    const playNow = vi.spyOn(player, "playNow").mockResolvedValue(undefined);
    await flushPromises();

    expect(wrapper.text()).toContain("Last 7 days · UTC");
    expect(wrapper.text()).toContain("12 plays");
    expect(wrapper.text()).toContain("3 plays");
    expect(wrapper.findAll(".song-row").map((row) => row.attributes("aria-label"))).toEqual([
      "Play Midnight City a by M83, ranked 1, 12 plays",
      "Play Midnight City b by M83, ranked 2, 3 plays",
    ]);
    expect(wrapper.findAll(".song-row > span:last-child > span")[1]?.attributes("style")).toContain(
      "25%",
    );
    await wrapper.get(".song-row").trigger("click");

    expect(playNow).toHaveBeenCalledWith([song().file]);
    expect(wrapper.find("audio, video").exists()).toBe(false);
    expect(api.getTopSongs).toHaveBeenCalledTimes(1);
  });

  it("shares one rankings request across duplicate widgets with the same minute window", async () => {
    const { wrapper, pinia } = mountWidget();
    cleanups.push(() => wrapper.unmount());
    await flushPromises();
    const duplicate = mount(HomeTopSongsWidget, {
      props: { widget: createHomeWidget(HomeWidgetType.TopSongs, "duplicate") },
      global: { plugins: [pinia, PiniaColada], stubs: { Button, UIcon: true } },
    });
    cleanups.push(() => duplicate.unmount());
    await flushPromises();

    expect(api.getTopSongs).toHaveBeenCalledTimes(1);
    expect(duplicate.text()).toContain("Midnight City");
  });

  it("makes the widget inert while editing and when the playback owner differs", async () => {
    const { wrapper, home, player } = mountWidget();
    cleanups.push(() => wrapper.unmount());
    const playNow = vi.spyOn(player, "playNow").mockResolvedValue(undefined);
    await flushPromises();
    home.beginEditing("desktop");
    await wrapper.vm.$nextTick();
    await wrapper.get(".song-row").trigger("click");

    expect(wrapper.get(".song-row").attributes("disabled")).toBeDefined();
    expect(playNow).not.toHaveBeenCalled();
    home.cancelEditing();
    player.playbackOwnerId = "bob";
    await wrapper.vm.$nextTick();
    await wrapper.get(".song-row").trigger("click");

    expect(playNow).not.toHaveBeenCalled();
    expect(wrapper.get(".song-row").attributes("disabled")).toBeDefined();
  });

  it("uses playback-version artwork and respects the thumbnail preference", async () => {
    const { wrapper, settings } = mountWidget();
    cleanups.push(() => wrapper.unmount());
    await flushPromises();

    expect(wrapper.get("img").attributes("src")).toContain("/versions/playing-version/thumbnail");
    settings.thumbnailsEnabled = false;
    await wrapper.vm.$nextTick();
    expect(wrapper.find("img").exists()).toBe(false);
  });

  it("discards a late response from the previous account", async () => {
    let finish: (response: TopSongsResponse) => void = () => {};
    let oldRange = homeTopSongsPeriod();
    api.getTopSongs.mockImplementationOnce((range: TopSongsQuery) => {
      oldRange = range;

      return new Promise<TopSongsResponse>((resolve) => {
        finish = resolve;
      });
    });
    api.getTopSongs.mockImplementationOnce((range: TopSongsQuery) =>
      Promise.resolve(result([], range)),
    );
    const { wrapper, home, auth } = mountWidget();
    cleanups.push(() => wrapper.unmount());
    await flushPromises();
    home.setOwner("bob");
    if (auth.user) auth.user.user.id = "bob";
    await flushPromises();
    finish(result([song()], oldRange));
    await flushPromises();

    expect(wrapper.text()).not.toContain("Midnight City");
    expect(wrapper.text()).toContain("No songs played yet");
  });

  it("suppresses transient authentication errors and retries transport errors", async () => {
    api.getTopSongs.mockRejectedValueOnce({ response: { status: 401 } });
    const pending = mountWidget();
    cleanups.push(() => pending.wrapper.unmount());
    await flushPromises();

    expect(pending.wrapper.find('[aria-label="Loading widget data"]').exists()).toBe(true);
    expect(pending.wrapper.find('[role="alert"]').exists()).toBe(false);
    api.getTopSongs.mockRejectedValueOnce(new Error("Connection failed"));
    const failed = mountWidget();
    cleanups.push(() => failed.wrapper.unmount());
    await flushPromises();

    expect(failed.wrapper.get('[role="alert"]').text()).toContain("Could not load this widget.");
    await failed.wrapper.get("button").trigger("click");
    await flushPromises();
    expect(failed.wrapper.text()).toContain("Midnight City");
  });

  it("refreshes the captured time window on retry without creating two requests", async () => {
    api.getTopSongs.mockRejectedValueOnce(new Error("Connection failed"));
    const { wrapper } = mountWidget();
    cleanups.push(() => wrapper.unmount());
    await flushPromises();
    vi.setSystemTime(new Date("2026-10-08T03:14:01Z"));
    await wrapper.get("button").trigger("click");
    await flushPromises();

    expect(api.getTopSongs).toHaveBeenCalledTimes(2);
    expect(api.getTopSongs.mock.calls[1]?.[0].to).toBe("2026-10-08T03:14:00.000Z");
    expect(wrapper.text()).toContain("Midnight City");
  });

  it("does not fetch or offer playback when streaming is disabled", async () => {
    vi.stubEnv("VITE_STREAMING_ENABLED", "false");
    const { wrapper } = mountWidget();
    cleanups.push(() => wrapper.unmount());
    await flushPromises();

    expect(wrapper.text()).toContain("Streaming is unavailable");
    expect(api.getTopSongs).not.toHaveBeenCalled();
    expect(wrapper.find("button").exists()).toBe(false);
  });

  it("offers the music library for an empty period", async () => {
    api.getTopSongs.mockImplementationOnce((range: TopSongsQuery) =>
      Promise.resolve(result([], range)),
    );
    const { wrapper } = mountWidget();
    cleanups.push(() => wrapper.unmount());
    await flushPromises();

    expect(wrapper.text()).toContain("No songs played yet");
    expect(wrapper.get("button").attributes("to")).toBe("/streaming/music");
  });
});
