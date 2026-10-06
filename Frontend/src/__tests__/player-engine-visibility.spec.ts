import { mount } from "@vue/test-utils";
import { expect, it, vi } from "vitest";
import { defineComponent, ref } from "vue";

import { usePlayerEngine } from "@/composables/usePlayerEngine";

const mocks = vi.hoisted(() => ({
  start: vi.fn(),
  close: vi.fn(),
  mediaKind: "audio" as "audio" | "video",
  onBuffering: null as ((event: { buffering: boolean }) => void) | null,
}));

vi.mock("@/api/client", () => ({ attemptRefresh: vi.fn() }));

vi.mock("@/api/streaming", () => ({
  streamingApi: {
    getManifest: vi.fn().mockResolvedValue("/test.mpd"),
    getHistoryByFile: vi.fn().mockResolvedValue(null),
    getSessions: vi.fn().mockResolvedValue({ items: [] }),
  },
}));

vi.mock("@/mutations/streaming", () => ({
  startSession: () => ({ mutateAsync: mocks.start }),
  closeSession: () => ({ mutateAsync: mocks.close }),
}));

vi.mock("@/stores/stream-player", () => ({
  usePlayerStore: () => {
    const buffering = ref(false);

    return {
      nowPlaying: {
        instanceId: 1,
        file: { fileId: "file-a", mimeType: `${mocks.mediaKind}/mp4`, currentVersionId: "v1" },
      },
      volume: 0.5,
      get engineBuffering() {
        return buffering.value;
      },
      setEngineBuffering: (value: boolean) => {
        buffering.value = value;
      },
      registerHistoryFlush: () => vi.fn(),
      registerEngine: () => vi.fn(),
      setEngineLoadError: vi.fn(),
      setVariantTracks: vi.fn(),
      setActiveVariantId: vi.fn(),
      setAbrEnabled: vi.fn(),
      setEngineReady: vi.fn(),
      setIsPlaying: vi.fn(),
      handleTrackEnded: vi.fn(),
      setVolume: vi.fn(),
    };
  },
}));

vi.mock("shaka-player/dist/shaka-player.ui.js", () => ({
  polyfill: { installAll: vi.fn() },
  Player: class {
    static isBrowserSupported = () => true;

    attach = vi.fn().mockResolvedValue(undefined);
    load = vi.fn().mockResolvedValue(undefined);
    destroy = vi.fn().mockResolvedValue(undefined);
    configure = vi.fn();
    addEventListener = vi.fn((name: string, listener: (event: { buffering: boolean }) => void) => {
      if (name === "buffering") mocks.onBuffering = listener;
    });
    getVariantTracks = vi.fn().mockReturnValue([]);
  },
}));

const createEngine = (mediaKind: "audio" | "video") => {
  vi.useFakeTimers();
  mocks.mediaKind = mediaKind;
  mocks.start.mockReset().mockResolvedValue({ id: "session-a" });
  mocks.close.mockReset().mockResolvedValue({});
  mocks.onBuffering = null;

  const hidden = vi.spyOn(document, "hidden", "get").mockReturnValue(false);
  const media = document.createElement("video");
  let paused = false;

  Object.defineProperty(media, "paused", { configurable: true, get: () => paused });
  vi.spyOn(media, "play").mockResolvedValue(undefined);
  vi.spyOn(media, "pause").mockImplementation(vi.fn());

  const wrapper = mount(
    defineComponent({
      setup: () => {
        usePlayerEngine(ref(media), ref<HTMLElement | null>(null), { headless: true, mediaKind });

        return () => null;
      },
    }),
  );

  return {
    wrapper,
    media,
    hidden,
    pause: () => {
      paused = true;
      media.dispatchEvent(new Event("pause"));
    },
    resume: () => {
      paused = false;
      media.dispatchEvent(new Event("playing"));
    },
  };
};

const cleanupEngine = async (wrapper: { unmount: () => void }) => {
  wrapper.unmount();

  await vi.advanceTimersByTimeAsync(0);

  vi.restoreAllMocks();
  vi.useRealTimers();
};

it.each(["audio", "video"] as const)(
  "keeps the %s session counting across browser visibility changes until pause",
  async (mediaKind) => {
    const { wrapper, media, hidden, pause } = createEngine(mediaKind);

    try {
      await vi.advanceTimersByTimeAsync(100);
      media.dispatchEvent(new Event("playing"));
      await vi.advanceTimersByTimeAsync(10_000);

      hidden.mockReturnValue(true);
      document.dispatchEvent(new Event("visibilitychange"));
      await vi.advanceTimersByTimeAsync(20_000);

      expect(mocks.close).not.toHaveBeenCalled();

      hidden.mockReturnValue(false);
      document.dispatchEvent(new Event("visibilitychange"));
      await vi.advanceTimersByTimeAsync(10_000);

      media.currentTime = 40;
      pause();
      await vi.advanceTimersByTimeAsync(0);

      expect(mocks.start).toHaveBeenCalledTimes(1);
      expect(mocks.close).toHaveBeenCalledExactlyOnceWith({
        sessionId: "session-a",
        req: { endPositionSeconds: 40, listenedSeconds: 40, playbackFinished: false },
      });
    } finally {
      await cleanupEngine(wrapper);
    }
  },
);

it.each(["audio", "video"] as const)(
  "keeps one %s session through buffering without counting stalled time",
  async (mediaKind) => {
    const { wrapper, media, pause } = createEngine(mediaKind);

    try {
      await vi.advanceTimersByTimeAsync(100);
      media.dispatchEvent(new Event("playing"));
      await vi.advanceTimersByTimeAsync(10_000);

      for (let recovery = 0; recovery < 3; recovery++) {
        expect(mocks.onBuffering).not.toBeNull();
        mocks.onBuffering!({ buffering: true });
        await vi.advanceTimersByTimeAsync(5_000);
        mocks.onBuffering!({ buffering: false });
        media.dispatchEvent(new Event("playing"));
        await vi.advanceTimersByTimeAsync(10_000);
      }

      media.currentTime = 40;
      pause();
      await vi.advanceTimersByTimeAsync(0);

      expect(mocks.start).toHaveBeenCalledTimes(1);
      expect(mocks.close).toHaveBeenCalledExactlyOnceWith({
        sessionId: "session-a",
        req: { endPositionSeconds: 40, listenedSeconds: 40, playbackFinished: false },
      });
    } finally {
      await cleanupEngine(wrapper);
    }
  },
);

it("keeps seeking in the same session without crediting stalled time or the seek jump", async () => {
  const { wrapper, media, pause } = createEngine("audio");
  const seeking = vi.spyOn(media, "seeking", "get").mockReturnValue(false);

  try {
    await vi.advanceTimersByTimeAsync(100);
    media.dispatchEvent(new Event("playing"));
    await vi.advanceTimersByTimeAsync(10_000);

    seeking.mockReturnValue(true);
    media.dispatchEvent(new Event("seeking"));
    await vi.advanceTimersByTimeAsync(5_000);
    seeking.mockReturnValue(false);
    media.currentTime = 150;
    media.dispatchEvent(new Event("playing"));
    media.dispatchEvent(new Event("seeked"));
    await vi.advanceTimersByTimeAsync(10_000);

    media.currentTime = 160;
    pause();
    await vi.advanceTimersByTimeAsync(0);

    expect(mocks.start).toHaveBeenCalledTimes(1);
    expect(mocks.close).toHaveBeenCalledExactlyOnceWith({
      sessionId: "session-a",
      req: { endPositionSeconds: 160, listenedSeconds: 20, playbackFinished: false },
    });
  } finally {
    await cleanupEngine(wrapper);
  }
});

it("opens another session after a deliberate pause and resume", async () => {
  const { wrapper, media, pause, resume } = createEngine("audio");
  mocks.start.mockResolvedValueOnce({ id: "first" }).mockResolvedValueOnce({ id: "second" });

  try {
    await vi.advanceTimersByTimeAsync(100);
    media.dispatchEvent(new Event("playing"));
    await vi.advanceTimersByTimeAsync(40_000);
    media.currentTime = 40;
    pause();
    await vi.advanceTimersByTimeAsync(5_000);

    resume();
    await vi.advanceTimersByTimeAsync(40_000);
    media.currentTime = 80;
    pause();
    await vi.advanceTimersByTimeAsync(0);

    expect(mocks.start).toHaveBeenCalledTimes(2);
    expect(mocks.close.mock.calls.map(([call]) => call.req.listenedSeconds)).toEqual([40, 40]);
    expect(mocks.close.mock.calls.map(([call]) => call.sessionId)).toEqual(["first", "second"]);
  } finally {
    await cleanupEngine(wrapper);
  }
});

it.each(["audio", "video"] as const)(
  "records %s finishing once when pause and ended arrive before the start response",
  async (mediaKind) => {
    const { wrapper, media, pause } = createEngine(mediaKind);
    let resolveStart: (session: { id: string }) => void = () => undefined;

    mocks.start.mockReturnValueOnce(new Promise<{ id: string }>((resolve) => {
      resolveStart = resolve;
    }));

    try {
      await vi.advanceTimersByTimeAsync(100);
      media.dispatchEvent(new Event("playing"));
      await vi.advanceTimersByTimeAsync(10_000);

      media.currentTime = 600;
      vi.spyOn(media, "ended", "get").mockReturnValue(true);
      pause();
      media.dispatchEvent(new Event("ended"));
      resolveStart({ id: "session-a" });
      await vi.advanceTimersByTimeAsync(0);

      expect(mocks.close).toHaveBeenCalledExactlyOnceWith({
        sessionId: "session-a",
        req: { endPositionSeconds: 600, listenedSeconds: 10, playbackFinished: true },
      });
    } finally {
      await cleanupEngine(wrapper);
    }
  },
);
