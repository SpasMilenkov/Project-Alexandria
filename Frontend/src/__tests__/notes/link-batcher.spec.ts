import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { createBatcher } from "@/features/notes/link-batcher";

interface Row {
  id: string;
  name: string;
}

const setup = (maxBatch = 10) => {
  const loadMany = vi.fn(
    (keys: string[]): Promise<Row[]> =>
      Promise.resolve(
        keys
          .filter((key) => key !== "missing")
          .map((key) => ({ id: key, name: key.toUpperCase() })),
      ),
  );

  const batcher = createBatcher<string, Row>({
    keyOf: (key) => key,
    loadMany,
    maxBatch,
    valueKeyOf: (row) => row.id,
    waitMs: 10,
  });

  return { batcher, loadMany };
};

describe("createBatcher", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("sends loads made together as one request, each key once", async () => {
    const { batcher, loadMany } = setup();

    const results = Promise.all([batcher.load("a"), batcher.load("b"), batcher.load("a")]);
    await vi.advanceTimersByTimeAsync(10);

    expect(await results).toEqual([
      { id: "a", name: "A" },
      { id: "b", name: "B" },
      { id: "a", name: "A" },
    ]);
    expect(loadMany).toHaveBeenCalledTimes(1);
    expect(loadMany).toHaveBeenCalledWith(["a", "b"]);
  });

  it("splits large batches", async () => {
    const { batcher, loadMany } = setup(2);

    const results = Promise.all(["a", "b", "c"].map((key) => batcher.load(key)));
    await vi.advanceTimersByTimeAsync(10);
    await results;

    expect(loadMany.mock.calls).toEqual([[["a", "b"]], [["c"]]]);
  });

  it("rejects only the key the response leaves out", async () => {
    const { batcher } = setup();

    const found = batcher.load("a");
    const missing = batcher.load("missing");
    const missingResult = expect(missing).rejects.toThrow();
    await vi.advanceTimersByTimeAsync(10);

    await expect(found).resolves.toEqual({ id: "a", name: "A" });
    await missingResult;
  });

  it("rejects every caller when the request fails", async () => {
    const { batcher, loadMany } = setup();
    loadMany.mockRejectedValueOnce(new Error("offline"));

    const first = expect(batcher.load("a")).rejects.toThrow("offline");
    const second = expect(batcher.load("b")).rejects.toThrow("offline");
    await vi.advanceTimersByTimeAsync(10);

    await first;
    await second;
  });

  it("starts a new batch after sending", async () => {
    const { batcher, loadMany } = setup();

    const first = batcher.load("a");
    await vi.advanceTimersByTimeAsync(10);
    await first;

    const second = batcher.load("b");
    await vi.advanceTimersByTimeAsync(10);
    await second;

    expect(loadMany).toHaveBeenCalledTimes(2);
  });
});
