import { describe, expect, it, vi } from "vitest";

import {
  ListeningHistoryTracker,
  type HistorySessionCallbacks,
} from "@/utils/listening-history-tracker";

const setup = () => {
  const calls = {
    starts: [] as Array<{ fileId: string; startPositionSeconds: number }>,
    closes: [] as Array<{ sessionId: string; listenedSeconds: number; endPositionSeconds: number }>,
  };
  let currentFileId: string | null = null;
  let holdNext = false;
  let releaseGate: (() => void) | null = null;
  let failCloses = 0;
  let closedOnServer = false;

  const callbacks: HistorySessionCallbacks = {
    startSession: async (fileId, startPositionSeconds) => {
      calls.starts.push({ fileId, startPositionSeconds });
      if (holdNext) {
        holdNext = false;
        await new Promise<void>((resolve) => {
          releaseGate = resolve;
        });
      }
      return { id: `session-${fileId}` };
    },
    closeSession: async (sessionId, payload) => {
      if (failCloses > 0) {
        failCloses--;
        throw new Error("close-failed");
      }
      calls.closes.push({ sessionId, ...payload });
    },
    verifyClosed: async () => closedOnServer,
    currentFileId: () => currentFileId,
  };

  const tracker = new ListeningHistoryTracker(callbacks);
  return {
    tracker,
    calls,
    setCurrentFile: (fileId: string | null) => {
      currentFileId = fileId;
    },
    releaseStart: () => releaseGate?.(),
    holdStart: () => {
      holdNext = true;
    },
    failNextCloses: (count: number) => {
      failCloses = count;
    },
    setClosedOnServer: (value: boolean) => {
      closedOnServer = value;
    },
  };
};

describe("player-engine-history", () => {
  it("submits duplicate close events once with captured counters", async () => {
    const ctx = setup();
    ctx.setCurrentFile("a");
    ctx.tracker.openNew("a", 10);
    await vi.waitFor(() => expect(ctx.calls.starts).toHaveLength(1));

    ctx.tracker.closeActive({ endPositionSeconds: 42, listenedSeconds: 32 });
    ctx.tracker.closeActive({ endPositionSeconds: 42, listenedSeconds: 32 });
    ctx.tracker.closeActive({ endPositionSeconds: 42, listenedSeconds: 32 });
    await ctx.tracker.flush();

    expect(ctx.calls.closes).toHaveLength(1);
    expect(ctx.calls.closes[0]).toMatchObject({
      sessionId: "session-a",
      endPositionSeconds: 42,
      listenedSeconds: 32,
    });
  });

  it("closes a late start for abandoned playback instead of assigning it", async () => {
    const ctx = setup();
    ctx.holdStart();
    ctx.setCurrentFile("a");
    ctx.tracker.openNew("a", 0);
    await vi.waitFor(() => expect(ctx.calls.starts).toHaveLength(1));

    ctx.setCurrentFile("b");
    ctx.tracker.openNew("b", 0);
    ctx.releaseStart();
    await ctx.tracker.flush();

    expect(ctx.calls.closes.map((c) => c.sessionId)).toEqual(["session-a"]);
  });

  it("retains failed closes for retry and reports persistent failure", async () => {
    const ctx = setup();
    ctx.setCurrentFile("a");
    ctx.tracker.openNew("a", 0);
    await vi.waitFor(() => expect(ctx.calls.starts).toHaveLength(1));

    ctx.failNextCloses(1);
    ctx.tracker.closeActive({ endPositionSeconds: 10, listenedSeconds: 10 });
    await expect(ctx.tracker.flush()).rejects.toThrow();
    expect(ctx.tracker.hasFailed).toBe(true);

    await ctx.tracker.flush();
    expect(ctx.tracker.hasFailed).toBe(false);
    expect(ctx.calls.closes).toHaveLength(1);
  });

  it("treats a verified already-closed session as success", async () => {
    const ctx = setup();
    ctx.setCurrentFile("a");
    ctx.tracker.openNew("a", 0);
    await vi.waitFor(() => expect(ctx.calls.starts).toHaveLength(1));

    ctx.failNextCloses(1);
    ctx.setClosedOnServer(true);
    ctx.tracker.closeActive({ endPositionSeconds: 10, listenedSeconds: 10 });
    await ctx.tracker.flush();

    expect(ctx.tracker.hasFailed).toBe(false);
    expect(ctx.calls.closes).toHaveLength(0);
  });

  it("flush waits for pending starts and their closes before new cycles", async () => {
    const ctx = setup();
    ctx.holdStart();
    ctx.setCurrentFile("a");
    ctx.tracker.openNew("a", 0);
    await vi.waitFor(() => expect(ctx.calls.starts).toHaveLength(1));

    ctx.setCurrentFile("b");
    const flushed = ctx.tracker.flush();
    ctx.releaseStart();
    await flushed;

    expect(ctx.calls.closes.map((c) => c.sessionId)).toContain("session-a");
  });

  it("closes a session that starts after playback was paused", async () => {
    const ctx = setup();
    ctx.holdStart();
    ctx.setCurrentFile("a");
    ctx.tracker.openNew("a", 4);
    await vi.waitFor(() => expect(ctx.calls.starts).toHaveLength(1));
    ctx.tracker.closeActive({ endPositionSeconds: 14, listenedSeconds: 10 });
    ctx.releaseStart();
    await ctx.tracker.flush();

    expect(ctx.calls.closes).toEqual([
      { sessionId: "session-a", endPositionSeconds: 14, listenedSeconds: 10 },
    ]);
  });

  it("does not assign an older same-file start over the current generation", async () => {
    const ctx = setup();
    ctx.holdStart();
    ctx.setCurrentFile("a");
    ctx.tracker.openNew("a", 0);
    await vi.waitFor(() => expect(ctx.calls.starts).toHaveLength(1));
    ctx.tracker.openNew("a", 20);
    ctx.releaseStart();
    await ctx.tracker.flush();

    expect(ctx.calls.closes).toContainEqual({
      sessionId: "session-a",
      endPositionSeconds: 0,
      listenedSeconds: 0,
    });
  });
});
