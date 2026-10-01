import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import piniaPluginPersistedstate from "pinia-plugin-persistedstate";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";

import { shuffleApi } from "@/api/shuffle";
import type { MediaFileDto, PaginatedResponse } from "@/api/streaming";
import { streamingApi } from "@/api/streaming";
import { useStreamingMediaContext } from "@/composables/useStreamingMediaContext";
import type { OnboardingStep } from "@/enums";
import { useAuthStore } from "@/stores/auth";
import { usePlayerStore } from "@/stores/stream-player";

const libFile = (index: number): MediaFileDto => ({
  fileId: `lib-${index}`,
  fileName: `lib-${index}.mp3`,
  mimeType: "audio/mpeg",
  currentVersionId: "v1",
  duration: 180,
  artist: null,
  album: null,
  title: null,
  genre: null,
  year: null,
  transpilationJobId: "job",
  playlistItemId: null,
  isVideo: false,
  segmentPrefix: null,
});

const LIBRARY = Array.from({ length: 113 }, (_, i) => libFile(i + 1));
const libraryRef = { isVideo: false, playlistId: null };

const installFakes = () => {
  vi.spyOn(streamingApi, "getFilesForStreaming").mockImplementation(async (query: {
    page: number;
    pageSize: number;
    anchorFileId?: string | null;
    anchorPlaylistItemId?: string | null;
  }): Promise<PaginatedResponse<MediaFileDto>> => {
    const all = [...LIBRARY];
    let page = query.page;
    if (query.anchorFileId) {
      const index = all.findIndex((f) => f.fileId === query.anchorFileId);
      if (index === -1) throw { response: { status: 404, data: {} } };
      page = Math.floor(index / query.pageSize) + 1;
    }
    return {
      items: all.slice((page - 1) * query.pageSize, page * query.pageSize),
      currentPage: page,
      pageSize: query.pageSize,
      totalCount: all.length,
      totalPages: Math.ceil(all.length / query.pageSize),
      hasPrevious: page > 1,
      hasNext: page < Math.ceil(all.length / query.pageSize),
    };
  });
  vi.spyOn(shuffleApi, "createSession").mockRejectedValue(new Error("no shuffle in this spec"));
  vi.spyOn(shuffleApi, "getSession").mockRejectedValue(new Error("no shuffle in this spec"));
  vi.spyOn(shuffleApi, "deleteSession").mockResolvedValue(undefined);
};

const seedPersisted = () => {
  localStorage.setItem(
    "player-v2",
    JSON.stringify({
      nowPlaying: {
        instanceId: 3,
        file: libFile(6),
        origin: "context",
        ended: false,
        restored: false,
      },
      context: {
        ref: libraryRef,
        label: "Music library",
        shuffled: false,
        anchor: { fileId: "lib-6" },
        cursor: 5,
        sessionId: null,
        expiresAt: null,
        total: 113,
      },
      queueEntries: [],
      snapshot: null,
      shufflePreference: { audio: false, video: false },
      afterContextEnds: "library",
      repeatMode: "off",
      videoAutoplay: true,
      volume: 0.5,
      playerMode: "pip",
      snapCorner: "br",
      playerDismissed: false,
      playbackOwnerId: "owner-1",
    }),
  );
};

const Host = defineComponent({
  setup: () => {
    useStreamingMediaContext();
    return () => h("div");
  },
});

describe("player restore on auth settle", () => {
  beforeEach(() => {
    const pinia = createPinia();
    pinia.use(piniaPluginPersistedstate);
    setActivePinia(pinia);
    localStorage.clear();
    installFakes();
  });

  it("rebuilds a persisted context when auth resolves after mount, then plays on click", async () => {
    seedPersisted();
    const pinia = createPinia();
    pinia.use(piniaPluginPersistedstate);
    setActivePinia(pinia);
    mount(Host, { global: { plugins: [pinia] } });

    const auth = useAuthStore();
    const player = usePlayerStore();
    expect(player.context?.cursor).toBe(5);

    await player.restore(null);
    auth.user = {
      success: true,
      user: { id: "owner-1", email: "a@b.c", name: "A" },
      userRoles: [],
      onboardingStep: "done" as OnboardingStep,
    };
    await vi.waitFor(() => {
      expect(player.nowPlaying?.restored).toBe(true);
    });
    expect(player.context?.cursor).toBe(5);

    await player.playTrackInContext(LIBRARY[100], libraryRef, "Music library");
    expect(player.nowPlaying?.file.fileId).toBe("lib-101");
    expect(player.context?.cursor).toBe(100);
  });
});
