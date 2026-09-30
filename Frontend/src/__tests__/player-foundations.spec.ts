import { afterEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";

import { RANGE_SIZE, SHUFFLE_BATCH_LIMIT, conflictField, shuffleApi } from "@/api/shuffle";
import { type MediaFileDto, streamingApi } from "@/api/streaming";
import { createAutoplayCountdown } from "@/stores/player/autoplay-countdown";
import { createEngineBridge } from "@/stores/player/engine-bridge";
import { createHistory } from "@/stores/player/history";
import { createRangeBuffer } from "@/stores/player/order/range-buffer";
import { locateAnchor } from "@/stores/player/order/sequential";
import { openShuffle } from "@/stores/player/order/shuffle";
import { createQueue } from "@/stores/player/queue";
import { Stale, createTransitions } from "@/stores/player/transitions";
import type { RangeResult } from "@/stores/player/types";
import { LIBRARY_PAGE_SIZE, indexOfAnchor, positionOf } from "@/utils/player-source";
import { SHUFFLE_RANGE_SIZE } from "@/utils/player-shuffle-buffer";

const file = (fileId: string, playlistItemId: string | null = null): MediaFileDto => ({
  fileId,
  fileName: `${fileId}.mp3`,
  mimeType: "audio/mpeg",
  currentVersionId: "v1",
  duration: 180,
  artist: null,
  album: null,
  title: null,
  genre: null,
  year: null,
  transpilationJobId: "job",
  playlistItemId,
  isVideo: false,
  segmentPrefix: null,
});

const range = (offset: number, ids: string[], total: number): RangeResult => ({
  offset,
  scannedCount: 50,
  total,
  items: ids.map((id, index) => ({ position: offset + index, file: file(id) })),
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("player foundations", () => {
  it("shares one range size across sequential and shuffle paths", () => {
    expect(RANGE_SIZE).toBe(50);
    expect(SHUFFLE_BATCH_LIMIT).toBe(RANGE_SIZE);
    expect(SHUFFLE_RANGE_SIZE).toBe(RANGE_SIZE);
    expect(LIBRARY_PAGE_SIZE).toBe(RANGE_SIZE);
  });

  it("matches anchors by occurrence and derives absolute positions", () => {
    const items = [file("a", "i1"), file("a", "i2"), file("b", "i3")];
    expect(indexOfAnchor(items, { fileId: "a", playlistItemId: "i2" })).toBe(1);
    expect(indexOfAnchor(items, { fileId: "b" })).toBe(2);
    expect(indexOfAnchor(items, { fileId: "missing" })).toBe(-1);
    expect(positionOf(3, 12)).toBe(112);
  });

  it("propagates totalCount and currentPage from sequential pages", async () => {
    const spy = vi.spyOn(streamingApi, "getFilesForStreaming").mockResolvedValue({
      items: [file("a")],
      currentPage: 3,
      pageSize: 50,
      totalCount: 113,
      totalPages: 3,
      hasPrevious: true,
      hasNext: false,
    });
    const { fetchSequentialPage } = await import("@/utils/player-source");
    const page = await fetchSequentialPage({ isVideo: false, playlistId: null }, 3);
    expect(page.totalCount).toBe(113);
    expect(page.currentPage).toBe(3);
    expect(spy).toHaveBeenCalledOnce();
  });

  it("locates the anchor page and maps it to absolute positions", async () => {
    vi.spyOn(streamingApi, "getFilesForStreaming").mockResolvedValue({
      items: [file("x"), file("y", "item-y")],
      currentPage: 3,
      pageSize: 50,
      totalCount: 113,
      totalPages: 3,
      hasPrevious: true,
      hasNext: false,
    });
    const found = await locateAnchor({ isVideo: false, playlistId: null }, { fileId: "y" });
    expect(found?.position).toBe(101);
    expect(found?.total).toBe(113);
    expect(found?.items[1]?.position).toBe(101);
  });

  it("reads which field a 409 blames", () => {
    const request = { response: { status: 409, data: { errors: { RequestId: ["taken"] } } } };
    const anchor = { response: { status: 409, data: { errors: { anchorFileId: ["gone"] } } } };
    expect(conflictField(request)).toBe("requestId");
    expect(conflictField(anchor)).toBe("anchorFileId");
    expect(conflictField({ response: { status: 409, data: {} } })).toBeNull();
    expect(conflictField(new Error("boom"))).toBeNull();
  });

  it("treats scanned holes as null without refetching", async () => {
    let calls = 0;
    const order = createRangeBuffer(async (start) => {
      calls++;
      return { offset: start, scannedCount: 50, total: 100, items: [] };
    });
    expect(await order.ensure(10)).toBeNull();
    expect(await order.ensure(11)).toBeNull();
    expect(calls).toBe(1);
  });

  it("deduplicates concurrent range reads", async () => {
    let calls = 0;
    const order = createRangeBuffer(async (start) => {
      calls++;
      await new Promise((resolve) => setTimeout(resolve, 5));
      return range(start, ["a"], 100);
    });
    const [first, second] = await Promise.all([order.ensure(0), order.ensure(0)]);
    expect(first?.fileId).toBe("a");
    expect(second?.fileId).toBe("a");
    expect(calls).toBe(1);
  });

  it("evicts ranges far from the cursor", async () => {
    const order = createRangeBuffer(async (start) => range(start, [`f${start}`], 600));
    for (let position = 0; position <= 550; position += 50) {
      await order.ensure(position);
    }
    order.evictAround(550);
    expect(order.at(0)).toBeNull();
    expect(order.at(50)).toBeNull();
    expect(order.at(550)?.fileId).toBe("f550");
  });

  it("reuses a live session id and falls back on 404", async () => {
    const flush = vi.fn(async () => undefined);
    const live = {
      sessionId: "s1",
      source: { isVideo: false, playlistId: null },
      algorithmVersion: 1,
      createdAt: "t",
      expiresAt: "e1",
      totalCount: 2,
      anchorPosition: null,
      offset: 0,
      scannedCount: 2,
      nextOffset: null,
      items: [
        { position: 0, file: file("a") },
        { position: 1, file: file("b") },
      ],
    };
    const get = vi.spyOn(shuffleApi, "getSession").mockResolvedValueOnce(live);
    const create = vi.spyOn(shuffleApi, "createSession");
    const opened = await openShuffle({
      ref: { isVideo: false, playlistId: null },
      sessionId: "s1",
      flushHistory: flush,
    });
    expect(opened.created).toBe(false);
    expect(opened.session.expiresAt).toBe("e1");
    expect(opened.order.at(1)?.fileId).toBe("b");
    expect(create).not.toHaveBeenCalled();
    expect(flush).toHaveBeenCalledOnce();
    expect(get).toHaveBeenCalledOnce();
  });

  it("retries the same body once on a requestId 409", async () => {
    const conflict = { response: { status: 409, data: { errors: { requestId: ["taken"] } } } };
    const created = {
      sessionId: "s2",
      source: { isVideo: false, playlistId: null },
      algorithmVersion: 1,
      createdAt: "t",
      expiresAt: "e2",
      totalCount: 1,
      anchorPosition: null,
      offset: 0,
      scannedCount: 1,
      nextOffset: null,
      items: [{ position: 0, file: file("a") }],
    };
    const create = vi
      .spyOn(shuffleApi, "createSession")
      .mockRejectedValueOnce(conflict)
      .mockResolvedValueOnce(created);
    const opened = await openShuffle({
      ref: { isVideo: false, playlistId: null },
      requestId: "fixed",
      flushHistory: async () => undefined,
    });
    expect(opened.session.sessionId).toBe("s2");
    expect(create).toHaveBeenCalledTimes(2);
    expect(create.mock.calls[0]?.[0]).toMatchObject({ requestId: "fixed" });
    expect(create.mock.calls[1]?.[0]).not.toMatchObject({ requestId: "fixed" });
  });

  it("drops the anchor and mints a new id on an anchor 409", async () => {
    const conflict = { response: { status: 409, data: { errors: { anchorFileId: ["gone"] } } } };
    const created = {
      sessionId: "s3",
      source: { isVideo: false, playlistId: null },
      algorithmVersion: 1,
      createdAt: "t",
      expiresAt: "e3",
      totalCount: 1,
      anchorPosition: null,
      offset: 0,
      scannedCount: 1,
      nextOffset: null,
      items: [{ position: 0, file: file("a") }],
    };
    const create = vi
      .spyOn(shuffleApi, "createSession")
      .mockRejectedValueOnce(conflict)
      .mockResolvedValueOnce(created);
    const opened = await openShuffle({
      ref: { isVideo: false, playlistId: null },
      anchor: { fileId: "gone" },
      flushHistory: async () => undefined,
    });
    expect(opened.anchored).toBe(false);
    expect(create.mock.calls[1]?.[0]).toMatchObject({ anchorFileId: null });
  });

  it("walks history without mutating forward entries on peek", () => {
    const history = createHistory();
    history.push(file("a"), "context");
    history.push(file("b"), "queue");
    history.push(file("c"), "context");
    expect(history.stepBack()?.file.fileId).toBe("b");
    expect(history.stepBack()?.file.fileId).toBe("a");
    expect(history.stepBack()).toBeNull();
    expect(history.stepForward()?.file.fileId).toBe("b");
    expect(history.hasForward()).toBe(true);
    history.push(file("d"), "interrupt");
    expect(history.hasForward()).toBe(false);
  });

  it("plays queue entries in order and drops skipped ones", () => {
    const entries = ref<{ id: string; file: MediaFileDto; group: string | null }[]>([]);
    const queue = createQueue(entries);
    queue.add([file("a"), file("b")], "P", false);
    queue.add([file("head")], null, true);
    expect(entries.value.map((entry) => entry.file.fileId)).toEqual(["head", "a", "b"]);
    expect(entries.value[1]?.group).toBe("P");
    expect(queue.takeHead()?.file.fileId).toBe("head");
    queue.dropThrough(0);
    expect(entries.value.map((entry) => entry.file.fileId)).toEqual(["b"]);
  });

  it("keeps engine slots per kind and flushes every tracker", async () => {
    const bridge = createEngineBridge();
    const audio = { play: vi.fn(), pause: vi.fn(), seek: vi.fn(), setVolume: vi.fn(), selectVariant: vi.fn(), setPlaybackRate: vi.fn() };
    const video = { play: vi.fn(), pause: vi.fn(), seek: vi.fn(), setVolume: vi.fn(), selectVariant: vi.fn(), setPlaybackRate: vi.fn() };
    const disposeAudio = bridge.registerEngine("audio", audio);
    bridge.registerEngine("video", video);
    bridge.play("audio");
    expect(audio.play).toHaveBeenCalledOnce();
    expect(video.play).not.toHaveBeenCalled();
    disposeAudio();
    bridge.pause("audio");
    expect(audio.pause).not.toHaveBeenCalled();
    const first = vi.fn(async () => undefined);
    const second = vi.fn(async () => undefined);
    const disposeFlush = bridge.registerHistoryFlush(first);
    bridge.registerHistoryFlush(second);
    disposeFlush();
    await bridge.flushHistory();
    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledOnce();
  });

  it("supersedes in-flight work and releases its session", async () => {
    const transitions = createTransitions();
    let released: string | null = null;
    let committed: string | null = null;
    let resolvePending!: (value: string) => void;
    const pending = new Promise<string>((resolve) => {
      resolvePending = resolve;
    });
    const first = transitions.run(async (guard) => {
      const session = await guard(pending, (value) => {
        released = value;
      });
      committed = session;
    });
    await new Promise((resolve) => setTimeout(resolve, 0));
    transitions.supersede();
    resolvePending("stale-session");
    const second = transitions.run(async () => {
      committed = "fresh";
    });
    await Promise.all([first, second]);
    expect(released).toBe("stale-session");
    expect(committed).toBe("fresh");
    const failure = transitions.run(async () => {
      throw new Stale();
    });
    await expect(failure).resolves.toBeUndefined();
  });

  it("cancels the video countdown before it fires", () => {
    vi.useFakeTimers();
    try {
      const done = vi.fn();
      const countdown = createAutoplayCountdown(done);
      countdown.start(5);
      expect(countdown.seconds.value).toBe(5);
      countdown.clear();
      vi.advanceTimersByTime(6000);
      expect(done).not.toHaveBeenCalled();
      expect(countdown.seconds.value).toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });
});
