import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { WorkerInMessage, WorkerOutMessage } from "@/workers/blake3.worker";

class FakeWorker {
  static instances: FakeWorker[] = [];

  onerror: ((event: ErrorEvent) => void) | null = null;
  onmessage: ((event: MessageEvent<WorkerOutMessage>) => void) | null = null;
  posted: WorkerInMessage[] = [];
  terminated = false;

  constructor() {
    FakeWorker.instances.push(this);
  }

  postMessage(message: WorkerInMessage) {
    this.posted.push(message);
  }

  terminate() {
    this.terminated = true;
  }

  reply(data: WorkerOutMessage) {
    this.onmessage?.({ data } as MessageEvent<WorkerOutMessage>);
  }

  finishLast(hash = "hash") {
    const { id } = this.posted.at(-1)!;

    this.reply({ hash, id, type: "done" });
  }
}

const loadPool = async () => (await import("@/features/upload/hash-pool")).hashFile;

const blob = () => new Blob(["x"]);

const noop = () => {};

describe("hash worker pool", () => {
  beforeEach(() => {
    vi.resetModules();
    vi.useFakeTimers();
    FakeWorker.instances = [];
    vi.stubGlobal("Worker", FakeWorker);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it("reuses one worker for files hashed one after another", async () => {
    const hashFile = await loadPool();

    const first = hashFile(blob(), "a", noop, new AbortController().signal);
    FakeWorker.instances[0]!.finishLast("hash-a");
    await expect(first).resolves.toBe("hash-a");

    const second = hashFile(blob(), "b", noop, new AbortController().signal);
    FakeWorker.instances[0]!.finishLast("hash-b");
    await expect(second).resolves.toBe("hash-b");

    expect(FakeWorker.instances).toHaveLength(1);
  });

  it("caps the pool at four workers and queues the rest", async () => {
    const hashFile = await loadPool();

    const jobs = ["a", "b", "c", "d", "e"].map((id) =>
      hashFile(blob(), id, noop, new AbortController().signal),
    );

    expect(FakeWorker.instances).toHaveLength(4);

    FakeWorker.instances[1]!.finishLast("hash-b");
    await expect(jobs[1]).resolves.toBe("hash-b");

    expect(FakeWorker.instances).toHaveLength(4);
    expect(FakeWorker.instances[1]!.posted.at(-1)!.id).toBe("e");
  });

  it("forwards progress for the job a worker is running", async () => {
    const hashFile = await loadPool();
    const onProgress = vi.fn();

    void hashFile(blob(), "a", onProgress, new AbortController().signal);
    FakeWorker.instances[0]!.reply({ id: "a", percent: 40, type: "progress" });

    expect(onProgress).toHaveBeenCalledWith(40);
  });

  it("replaces a worker that is cancelled mid-file", async () => {
    const hashFile = await loadPool();
    const controller = new AbortController();

    const job = hashFile(blob(), "a", noop, controller.signal);
    controller.abort();

    await expect(job).rejects.toMatchObject({ name: "AbortError" });
    expect(FakeWorker.instances[0]!.terminated).toBe(true);

    void hashFile(blob(), "b", noop, new AbortController().signal);

    expect(FakeWorker.instances).toHaveLength(2);
    expect(FakeWorker.instances[1]!.posted[0]!.id).toBe("b");
  });

  it("drops a queued file that is cancelled before it starts", async () => {
    const hashFile = await loadPool();
    const controller = new AbortController();

    for (const id of ["a", "b", "c", "d"]) {
      void hashFile(blob(), id, noop, new AbortController().signal);
    }

    const queued = hashFile(blob(), "e", noop, controller.signal);
    controller.abort();

    await expect(queued).rejects.toMatchObject({ name: "AbortError" });

    const postedIds = FakeWorker.instances.flatMap((worker) => worker.posted.map((m) => m.id));

    expect(postedIds).not.toContain("e");
  });

  it("rejects with the worker's message when hashing fails", async () => {
    const hashFile = await loadPool();

    const job = hashFile(blob(), "a", noop, new AbortController().signal);
    FakeWorker.instances[0]!.reply({ id: "a", message: "read failed", type: "error" });

    await expect(job).rejects.toThrow("read failed");
  });

  it("releases idle workers after a quiet period", async () => {
    const hashFile = await loadPool();

    const job = hashFile(blob(), "a", noop, new AbortController().signal);
    FakeWorker.instances[0]!.finishLast();
    await job;

    vi.advanceTimersByTime(29_000);
    expect(FakeWorker.instances[0]!.terminated).toBe(false);

    vi.advanceTimersByTime(1_000);
    expect(FakeWorker.instances[0]!.terminated).toBe(true);
  });
});
