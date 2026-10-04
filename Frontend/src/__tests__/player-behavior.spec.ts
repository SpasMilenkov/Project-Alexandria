import { createPinia, setActivePinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { MediaFileDto, PaginatedResponse } from "@/api/streaming";

import { shuffleApi } from "@/api/shuffle";
import { streamingApi } from "@/api/streaming";
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

const playlistFile = (index: number): MediaFileDto => ({
  fileId: `pf-${index}`,
  fileName: `pf-${index}.mp3`,
  mimeType: "audio/mpeg",
  currentVersionId: "v1",
  duration: 180,
  artist: null,
  album: null,
  title: null,
  genre: null,
  year: null,
  transpilationJobId: "job",
  playlistItemId: `item-${index}`,
  isVideo: false,
  segmentPrefix: null,
});

const videoFile = (id: string): MediaFileDto => ({
  ...libFile(1),
  fileId: id,
  mimeType: "video/mp4",
  isVideo: true,
});

const LIBRARY = Array.from({ length: 113 }, (_, i) => libFile(i + 1));
const PLAYLIST = Array.from({ length: 10 }, (_, i) => playlistFile(i + 1));

interface FakeSession {
  source: { isVideo: boolean; playlistId: string | null };
  order: MediaFileDto[];
}

const sessions = new Map<string, FakeSession>();
let sessionSequence = 0;
let createdSessions = 0;
let deletedSessions = 0;
let failCreate: unknown = null;
let failGet: unknown = null;
const failSessionIds = new Set<string>();

const notFound = () => ({ response: { status: 404, data: {} } });
const conflictOn = (field: string) => ({
  response: { status: 409, data: { errors: { [field]: ["conflict"] } } },
});

const sourceFiles = (source: { isVideo: boolean; playlistId: string | null }): MediaFileDto[] => {
  if (source.playlistId === "dup") return [playlistFile(1), playlistFile(1)];
  if (source.playlistId) return [...PLAYLIST];
  return [...LIBRARY];
};

const installFakes = () => {
  sessions.clear();
  sessionSequence = 0;
  createdSessions = 0;
  deletedSessions = 0;
  failCreate = null;
  failGet = null;
  failSessionIds.clear();

  vi.spyOn(streamingApi, "getFilesForStreaming").mockImplementation(
    async (query: {
      page: number;
      pageSize: number;
      playlistId?: string | null;
      anchorFileId?: string | null;
      anchorPlaylistItemId?: string | null;
    }): Promise<PaginatedResponse<MediaFileDto>> => {
      if (query.playlistId === "gone") throw notFound();
      const all = query.playlistId
        ? sourceFiles({ isVideo: false, playlistId: query.playlistId })
        : [...LIBRARY];
      let page = query.page;
      if (query.anchorFileId || query.anchorPlaylistItemId) {
        const index = all.findIndex((f) =>
          query.anchorPlaylistItemId
            ? f.playlistItemId === query.anchorPlaylistItemId
            : f.fileId === query.anchorFileId,
        );
        if (index === -1) throw notFound();
        page = Math.floor(index / query.pageSize) + 1;
      }
      const totalCount = all.length;
      const totalPages = Math.max(1, Math.ceil(totalCount / query.pageSize));
      return {
        items: all.slice((page - 1) * query.pageSize, page * query.pageSize),
        currentPage: page,
        pageSize: query.pageSize,
        totalCount,
        totalPages,
        hasPrevious: page > 1,
        hasNext: page < totalPages,
      };
    },
  );

  vi.spyOn(shuffleApi, "createSession").mockImplementation(
    async (req: {
      requestId: string;
      source: { isVideo: boolean; playlistId: string | null };
      anchorFileId?: string | null;
      anchorPlaylistItemId?: string | null;
      avoidFirstFileId?: string | null;
    }) => {
      if (failCreate) {
        const err = failCreate;
        failCreate = null;
        throw err;
      }
      if (req.source.playlistId === "gone") throw notFound();
      const all = sourceFiles(req.source);
      if (req.anchorFileId) {
        const present = all.some((f) =>
          req.anchorPlaylistItemId
            ? f.playlistItemId === req.anchorPlaylistItemId && f.fileId === req.anchorFileId
            : f.fileId === req.anchorFileId,
        );
        if (!present) throw conflictOn("anchorFileId");
      }
      let order = [...all];
      const seen = new Set<string>();
      order = order.filter((f) => {
        if (seen.has(f.fileId)) return false;
        seen.add(f.fileId);
        return true;
      });
      if (req.anchorFileId) {
        const anchor = order.find((f) =>
          req.anchorPlaylistItemId
            ? f.playlistItemId === req.anchorPlaylistItemId
            : f.fileId === req.anchorFileId,
        );
        order = [anchor as MediaFileDto, ...order.filter((f) => f !== anchor)];
      }
      if (req.avoidFirstFileId && order.length > 1 && order[0]?.fileId === req.avoidFirstFileId) {
        const first = order.shift() as MediaFileDto;
        order.push(first);
      }
      sessionSequence += 1;
      createdSessions += 1;
      const sessionId = `session-${sessionSequence}`;
      sessions.set(sessionId, { source: req.source, order });
      return {
        sessionId,
        source: req.source,
        algorithmVersion: 1,
        createdAt: new Date().toISOString(),
        expiresAt: new Date(Date.now() + 24 * 3600 * 1000).toISOString(),
        totalCount: order.length,
        anchorPosition: req.anchorFileId ? 0 : null,
        offset: 0,
        scannedCount: Math.min(50, order.length),
        nextOffset: order.length > 50 ? 50 : null,
        items: order.slice(0, 50).map((file, index) => ({ position: index, file })),
      };
    },
  );

  vi.spyOn(shuffleApi, "getSession").mockImplementation(
    async (sessionId: string, offset: number) => {
      if (failGet) {
        const err = failGet;
        failGet = null;
        throw err;
      }
      if (failSessionIds.has(sessionId)) throw notFound();
      const session = sessions.get(sessionId);
      if (!session) throw notFound();
      const items = session.order.slice(offset, offset + 50).map((file, index) => ({
        position: offset + index,
        file,
      }));
      return {
        sessionId,
        source: session.source,
        algorithmVersion: 1,
        createdAt: new Date().toISOString(),
        expiresAt: new Date(Date.now() + 24 * 3600 * 1000).toISOString(),
        totalCount: session.order.length,
        anchorPosition: null,
        offset,
        scannedCount: Math.min(50, session.order.length - offset),
        nextOffset: offset + 50 < session.order.length ? offset + 50 : null,
        items,
      };
    },
  );

  vi.spyOn(shuffleApi, "deleteSession").mockImplementation(async (sessionId: string) => {
    sessions.delete(sessionId);
    deletedSessions += 1;
  });
};

const playlistRef = { isVideo: false, playlistId: "pl-1" };
const libraryRef = { isVideo: false, playlistId: null };

beforeEach(() => {
  setActivePinia(createPinia());
  localStorage.clear();
  installFakes();
});

afterEach(() => {
  vi.restoreAllMocks();
});

const playingIds = () => {
  const store = usePlayerStore();
  return store.nowPlaying?.file.fileId ?? null;
};

describe("player behavior", () => {
  it("S1 plays a sequential playlist in order", async () => {
    const store = usePlayerStore();
    await store.startContext(playlistRef, { label: "P" });
    expect(playingIds()).toBe("pf-1");
    await store.next();
    expect(playingIds()).toBe("pf-2");
    await store.next();
    expect(playingIds()).toBe("pf-3");
    expect(store.context?.cursor).toBe(2);
  });

  it("S5 pins a track inside a shuffled playlist without touching the session", async () => {
    const store = usePlayerStore();
    await store.startContext(playlistRef, { label: "P", shuffle: true });
    const firstSession = store.context?.sessionId;
    expect(firstSession).not.toBeNull();
    for (let i = 0; i < 4; i++) await store.next();
    expect(store.context?.cursor).toBe(4);
    await store.playTrackInContext(PLAYLIST[8], playlistRef, "P");
    expect(playingIds()).toBe("pf-9");
    expect(store.context?.cursor).toBe(4);
    expect(store.context?.sessionId).toBe(firstSession);
    expect(createdSessions).toBe(1);
    expect(store.snapshot).toBeNull();
    await store.next();
    expect(playingIds()).not.toBe("pf-9");
  });

  it("S7 keeps the queue across a context replace with a Clear notice", async () => {
    const store = usePlayerStore();
    await store.addToQueue([libFile(1), libFile(2)]);
    expect(playingIds()).toBe("lib-1");
    await store.startContext(playlistRef, { label: "P" });
    expect(playingIds()).toBe("pf-1");
    await store.next();
    expect(playingIds()).toBe("lib-2");
    await store.next();
    expect(playingIds()).toBe("pf-2");
    const notice = store.notices.find((n) => n.message.includes("Queue kept"));
    expect(notice?.actionLabel).toBe("Clear");
    notice?.onAction?.();
    expect(store.queueEntries.length).toBe(0);
  });

  it("S10 resumes the shuffle at its cursor after an interrupt", async () => {
    const store = usePlayerStore();
    await store.startContext(libraryRef, { label: "Library", shuffle: true });
    for (let i = 0; i < 10; i++) await store.next();
    expect(store.context?.cursor).toBe(10);
    await store.playNow([libFile(200)]);
    expect(playingIds()).toBe("lib-200");
    expect(store.nowPlaying?.origin).toBe("interrupt");
    await store.next();
    expect(playingIds()).toBe("lib-12");
    expect(store.context?.cursor).toBe(11);
  });

  it("S12 stops with Play again and disabled Next under the stop policy", async () => {
    const store = usePlayerStore();
    store.setAfterContextEnds("stop");
    await store.startContext(playlistRef, { label: "P" });
    for (let i = 0; i < 9; i++) await store.next();
    expect(playingIds()).toBe("pf-10");
    await store.next();
    expect(store.queueEnded).toBe(true);
    expect(store.hasNext).toBe(false);
    await store.playAgain();
    expect(playingIds()).toBe("pf-1");
  });

  it("S16 excludes sequentially heard tracks when shuffle is enabled mid-cycle", async () => {
    const store = usePlayerStore();
    await store.startContext(playlistRef, { label: "P" });
    for (let i = 0; i < 4; i++) await store.next();
    expect(playingIds()).toBe("pf-5");
    await store.enableShuffle();
    expect(playingIds()).toBe("pf-5");
    const heard = new Set(["pf-5"]);
    for (let i = 0; i < 5; i++) {
      await store.next();
      const id = playingIds() as string;
      expect(heard.has(id)).toBe(false);
      heard.add(id);
    }
    expect(heard.size).toBe(6);
  });

  it("S19 replays on natural end with repeat one but moves on explicit Next", async () => {
    const store = usePlayerStore();
    store.toggleLoop();
    store.toggleLoop();
    expect(store.repeatMode).toBe("one");
    await store.startContext(playlistRef, { label: "P" });
    const instance = store.nowPlaying?.instanceId ?? -1;
    await store.handleTrackEnded(instance);
    expect(playingIds()).toBe("pf-1");
    await store.next();
    expect(playingIds()).toBe("pf-2");
  });

  it("S20 walks history without touching queue or cursor", async () => {
    const store = usePlayerStore();
    await store.startContext(playlistRef, { label: "P" });
    await store.next();
    await store.addToQueue([libFile(50)]);
    await store.next();
    expect(playingIds()).toBe("lib-50");
    await store.next();
    expect(playingIds()).toBe("pf-3");
    await store.previous();
    expect(playingIds()).toBe("lib-50");
    await store.previous();
    expect(playingIds()).toBe("pf-2");
    await store.next();
    expect(playingIds()).toBe("lib-50");
    expect(store.context?.cursor).toBe(2);
  });

  it("S25 lets the last of two rapid plays win without leaking sessions", async () => {
    const store = usePlayerStore();
    const first = store.startContext(playlistRef, { label: "A", shuffle: true });
    await Promise.resolve();
    const second = store.startContext(
      { isVideo: false, playlistId: "pl-2" },
      { label: "B", shuffle: true },
    );
    await Promise.all([first, second]);
    expect(store.context?.label).toBe("B");
    expect(sessions.size).toBe(1);
    expect(createdSessions).toBe(2);
    expect(deletedSessions).toBe(1);
  });

  it("S27 advances once for duplicate ended events", async () => {
    const store = usePlayerStore();
    await store.startContext(playlistRef, { label: "P" });
    const instance = store.nowPlaying?.instanceId ?? -1;
    await store.handleTrackEnded(instance);
    await store.handleTrackEnded(instance);
    expect(playingIds()).toBe("pf-2");
  });

  it("S29 changes only the preference when shuffling with no context", async () => {
    const store = usePlayerStore();
    await store.playNow([libFile(7)]);
    await store.toggleShuffle();
    expect(store.shufflePreference.audio).toBe(true);
    expect(store.context).toBeNull();
    expect(playingIds()).toBe("lib-7");
  });

  it("S33 replays video immediately under repeat one with no countdown", async () => {
    vi.useFakeTimers();
    try {
      const store = usePlayerStore();
      store.toggleLoop();
      store.toggleLoop();
      await store.playNow([videoFile("vid-1")]);
      const instance = store.nowPlaying?.instanceId ?? -1;
      await store.handleTrackEnded(instance);
      expect(playingIds()).toBe("vid-1");
      expect(store.autoplayCountdown).toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });

  it("S40 plays a duplicated file once per shuffled cycle", async () => {
    const store = usePlayerStore();
    await store.startContext(
      { isVideo: false, playlistId: "dup" },
      { label: "DUP", shuffle: true },
    );
    const total = store.context?.total ?? 0;
    expect(total).toBe(1);
    expect(playingIds()).toBe("pf-1");
  });

  it("S42 retries the same body once on a requestId conflict", async () => {
    const store = usePlayerStore();
    failCreate = conflictOn("requestId");
    await store.startContext(playlistRef, { label: "P", shuffle: true });
    expect(playingIds()).toBe("pf-1");
    expect(createdSessions).toBe(1);
  });

  it("S43 falls back without the anchor on an anchor conflict", async () => {
    const store = usePlayerStore();
    failCreate = conflictOn("anchorFileId");
    await store.startContext(playlistRef, { label: "P", shuffle: true, startAt: PLAYLIST[4] });
    expect(playingIds()).toBe("pf-5");
    expect(store.nowPlaying?.origin).toBe("interrupt");
    expect(store.notices.length).toBeGreaterThan(0);
  });

  it("S46 reconciles two fast toggles to the final preference", async () => {
    const store = usePlayerStore();
    await store.startContext(playlistRef, { label: "P", shuffle: true });
    expect(store.context?.shuffled).toBe(true);
    const first = store.toggleShuffle();
    const second = store.toggleShuffle();
    await Promise.all([first, second]);
    expect(store.shufflePreference.audio).toBe(true);
    expect(store.context?.shuffled).toBe(true);
  });

  it("S47 starts a library shuffle after an interrupt with no context", async () => {
    const store = usePlayerStore();
    await store.playNow([libFile(9)]);
    const instance = store.nowPlaying?.instanceId ?? -1;
    await store.handleTrackEnded(instance);
    expect(store.context?.ref.playlistId).toBeNull();
    expect(store.context?.shuffled).toBe(true);
    expect(playingIds()).not.toBe("lib-9");
  });

  it("S48 replays repeat-one three times in a row", async () => {
    const store = usePlayerStore();
    store.toggleLoop();
    store.toggleLoop();
    await store.playNow([libFile(3)]);
    for (let i = 0; i < 3; i++) {
      const instance = store.nowPlaying?.instanceId ?? -1;
      await store.handleTrackEnded(instance);
      expect(playingIds()).toBe("lib-3");
    }
  });

  it("S50 keeps the sequential context when enabling shuffle fails", async () => {
    const store = usePlayerStore();
    await store.startContext(playlistRef, { label: "P" });
    failCreate = new Error("offline");
    await store.toggleShuffle();
    expect(store.error).not.toBeNull();
    expect(store.context?.shuffled).toBe(false);
    expect(playingIds()).toBe("pf-1");
    failCreate = null;
    await store.next();
    expect(playingIds()).toBe("pf-2");
  });

  it("X5 rebuilds the session on a mid-advance 404 and keeps heard", async () => {
    const store = usePlayerStore();
    await store.startContext(libraryRef, { label: "Library", shuffle: true });
    for (let i = 0; i < 90; i++) await store.next();
    expect(store.context?.cursor).toBe(90);
    failSessionIds.add(store.context?.sessionId as string);
    for (let i = 0; i < 9; i++) await store.next();
    expect(store.context?.cursor).toBe(99);
    await store.next();
    expect(store.notices.some((n) => n.message.includes("Shuffle refreshed"))).toBe(true);
    expect(store.context?.cursor).toBeGreaterThan(99);
  });

  it("PE5 clears the context on playlist delete and applies the end policy later", async () => {
    const store = usePlayerStore();
    store.setAfterContextEnds("stop");
    await store.startContext(libraryRef, { label: "Library", shuffle: true });
    await store.startContext(playlistRef, { label: "P" });
    expect(store.snapshot?.label).toBe("Library");
    await store.startContext({ isVideo: false, playlistId: "pl-2" }, { label: "B" });
    expect(store.snapshot?.label).toBe("P");
    await store.contextSourceChanged("pl-1", { deleted: true });
    expect(store.context?.label).toBe("B");
    expect(store.snapshot).toBeNull();
    await store.startContext(playlistRef, { label: "P" });
    await store.contextSourceChanged("pl-1", { deleted: true });
    expect(store.context).toBeNull();
    expect(store.nowPlaying?.origin).toBe("interrupt");
    expect(store.notices.length).toBeGreaterThan(0);
  });

  it("E3 takes no snapshot when replaying the current source", async () => {
    const store = usePlayerStore();
    await store.startContext(libraryRef, { label: "Library" });
    await store.startContext(playlistRef, { label: "P" });
    expect(store.snapshot?.label).toBe("Library");
    await store.startContext(playlistRef, { label: "P" });
    expect(store.snapshot?.label).toBe("Library");
  });

  it("R5 does not restamp or reload on a second restore for the same owner", async () => {
    const store = usePlayerStore();
    await store.startContext(playlistRef, { label: "P", shuffle: true });
    const instance = store.nowPlaying?.instanceId ?? -1;
    await store.restore("owner-1");
    const restored = store.nowPlaying?.instanceId ?? -2;
    expect(restored).not.toBe(instance);
    expect(store.nowPlaying?.restored).toBe(true);
    await store.restore("owner-1");
    expect(store.nowPlaying?.instanceId).toBe(restored);
  });

  it("S32 wraps a 113-track library across a partial last page", async () => {
    const store = usePlayerStore();
    store.toggleLoop();
    expect(store.repeatMode).toBe("all");
    const clicked = LIBRARY[100];
    await store.playTrackInContext(clicked, libraryRef, "Library");
    expect(playingIds()).toBe("lib-101");
    for (let i = 0; i < 12; i++) await store.next();
    expect(playingIds()).toBe("lib-113");
    await store.next();
    expect(playingIds()).toBe("lib-1");
  });
});
