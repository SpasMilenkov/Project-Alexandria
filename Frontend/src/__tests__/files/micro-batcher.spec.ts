import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { ApiError } from "@/api/errors";
import {
  type MicroBatcherOptions,
  createMicroBatcher,
  isRetriableBatchFailure,
} from "@/features/upload/micro-batcher";

const httpError = (status: number) => new ApiError("failed", { kind: "http", status });

const setup = (overrides: Partial<MicroBatcherOptions<string, string>> = {}) => {
  const send = vi.fn((items: string[]): Promise<(string | Error)[]> =>
    Promise.resolve(items.map((item) => `done:${item}`)),
  );

  const batcher = createMicroBatcher<string, string>({
    lingerMs: 50,
    maxInFlight: 1,
    maxItems: 3,
    maxWeight: 100,
    minIntervalMs: 1000,
    retryDelaysMs: [1000, 3000],
    send,
    ...overrides,
  });

  return { batcher, send };
};

describe("createMicroBatcher", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("groups items that arrive within the linger window", async () => {
    const { batcher, send } = setup();

    const results = Promise.all([batcher.submit("a"), batcher.submit("b")]);

    await vi.advanceTimersByTimeAsync(49);
    expect(send).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(1);
    expect(send).toHaveBeenCalledExactlyOnceWith(["a", "b"]);
    await expect(results).resolves.toEqual(["done:a", "done:b"]);
  });

  it("sends a full batch without waiting for the linger", async () => {
    const { batcher, send } = setup();

    batcher.submit("a");
    batcher.submit("b");
    batcher.submit("c");

    await vi.advanceTimersByTimeAsync(0);

    expect(send).toHaveBeenCalledExactlyOnceWith(["a", "b", "c"]);
  });

  it("flushes as soon as the queued weight reaches the limit", async () => {
    const { batcher, send } = setup();

    batcher.submit("a", { weight: 60 });
    batcher.submit("b", { weight: 40 });

    await vi.advanceTimersByTimeAsync(0);

    expect(send).toHaveBeenCalledExactlyOnceWith(["a", "b"]);
  });

  it("does not let a batch go over the weight limit", async () => {
    const { batcher, send } = setup({ maxItems: 10 });

    batcher.submit("a", { weight: 60 });
    batcher.submit("b", { weight: 60 });

    await vi.advanceTimersByTimeAsync(0);
    expect(send).toHaveBeenLastCalledWith(["a"]);

    await vi.advanceTimersByTimeAsync(1000);
    expect(send).toHaveBeenLastCalledWith(["b"]);
  });

  it("sends an oversized item as a batch of one", async () => {
    const { batcher, send } = setup();

    const result = batcher.submit("huge", { weight: 5000 });

    await vi.advanceTimersByTimeAsync(0);

    expect(send).toHaveBeenCalledExactlyOnceWith(["huge"]);
    await expect(result).resolves.toBe("done:huge");
  });

  it("never sends faster than the minimum interval, even with full batches waiting", async () => {
    const { batcher, send } = setup({ maxInFlight: 2 });

    for (const item of ["a", "b", "c", "d", "e", "f", "g", "h", "i"]) batcher.submit(item);

    await vi.advanceTimersByTimeAsync(0);
    expect(send).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(999);
    expect(send).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1);
    expect(send).toHaveBeenCalledTimes(2);

    await vi.advanceTimersByTimeAsync(1000);
    expect(send).toHaveBeenCalledTimes(3);
  });

  it("holds the next batch until the request in flight finishes", async () => {
    let finishFirst: (value: string[]) => void = () => {};

    const send = vi.fn<(items: string[]) => Promise<(string | Error)[]>>();

    send.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finishFirst = resolve;
        }),
    );
    send.mockImplementation((items) => Promise.resolve(items));

    const { batcher } = setup({ send });

    batcher.submit("a");
    batcher.submit("b");
    batcher.submit("c");
    await vi.advanceTimersByTimeAsync(0);

    batcher.submit("d");
    await vi.advanceTimersByTimeAsync(5000);
    expect(send).toHaveBeenCalledTimes(1);

    finishFirst(["a", "b", "c"]);
    await vi.advanceTimersByTimeAsync(0);

    expect(send).toHaveBeenCalledTimes(2);
    expect(send).toHaveBeenLastCalledWith(["d"]);
  });

  it("allows a second request while the first is in flight when configured", async () => {
    const send = vi.fn<(items: string[]) => Promise<(string | Error)[]>>(
      () => new Promise(() => {}),
    );

    const { batcher } = setup({ maxInFlight: 2, send });

    batcher.submit("a");
    await vi.advanceTimersByTimeAsync(50);

    batcher.submit("b");
    await vi.advanceTimersByTimeAsync(1000);

    expect(send).toHaveBeenCalledTimes(2);
  });

  it("fails only the item whose result is an error", async () => {
    const send = vi.fn(() => Promise.resolve(["done:a", new Error("bad file"), "done:c"]));
    const { batcher } = setup({ send });

    const outcomes = Promise.allSettled([
      batcher.submit("a"),
      batcher.submit("b"),
      batcher.submit("c"),
    ]);

    await vi.advanceTimersByTimeAsync(0);

    const [first, second, third] = await outcomes;

    expect(first).toEqual({ status: "fulfilled", value: "done:a" });
    expect(second).toMatchObject({ reason: { message: "bad file" }, status: "rejected" });
    expect(third).toEqual({ status: "fulfilled", value: "done:c" });
  });

  it("rejects an item the server returned no result for", async () => {
    const send = vi.fn(() => Promise.resolve(["done:a"]));
    const { batcher } = setup({ send });

    const outcomes = Promise.allSettled([batcher.submit("a"), batcher.submit("b")]);

    await vi.advanceTimersByTimeAsync(50);

    const [first, second] = await outcomes;

    expect(first.status).toBe("fulfilled");
    expect(second.status).toBe("rejected");
  });

  it("drops an item aborted before its batch is sent", async () => {
    const { batcher, send } = setup();
    const controller = new AbortController();

    const dropped = batcher.submit("dropped", { signal: controller.signal });
    const kept = batcher.submit("kept");

    controller.abort();

    await expect(dropped).rejects.toMatchObject({ name: "AbortError" });

    await vi.advanceTimersByTimeAsync(50);

    expect(send).toHaveBeenCalledExactlyOnceWith(["kept"]);
    await expect(kept).resolves.toBe("done:kept");
  });

  it("rejects straight away when already aborted", async () => {
    const { batcher, send } = setup();

    await expect(batcher.submit("a", { signal: AbortSignal.abort() })).rejects.toMatchObject({
      name: "AbortError",
    });

    await vi.advanceTimersByTimeAsync(5000);

    expect(send).not.toHaveBeenCalled();
  });

  it("still delivers the result to an item aborted after its batch was sent", async () => {
    const { batcher } = setup();
    const controller = new AbortController();

    const result = batcher.submit("a", { signal: controller.signal });

    await vi.advanceTimersByTimeAsync(50);
    controller.abort();

    await expect(result).resolves.toBe("done:a");
  });

  it("retries a rate-limited batch with the same items", async () => {
    const send = vi.fn<(items: string[]) => Promise<(string | Error)[]>>();

    send.mockRejectedValueOnce(httpError(503));
    send.mockImplementation((items) => Promise.resolve(items));

    const { batcher } = setup({ send });
    const result = batcher.submit("a");

    await vi.advanceTimersByTimeAsync(50);
    expect(send).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1000);

    expect(send).toHaveBeenCalledTimes(2);
    expect(send).toHaveBeenLastCalledWith(["a"]);
    await expect(result).resolves.toBe("a");
  });

  it("gives up after the configured retries and rejects the whole batch", async () => {
    const send = vi.fn(() => Promise.reject(httpError(503)));

    const { batcher } = setup({ send });
    const outcomes = Promise.allSettled([batcher.submit("a"), batcher.submit("b")]);

    await vi.advanceTimersByTimeAsync(50 + 1000 + 3000);

    expect(send).toHaveBeenCalledTimes(3);

    const settled = await outcomes;

    expect(settled.map((outcome) => outcome.status)).toEqual(["rejected", "rejected"]);
  });

  it("does not retry a failure that is not transient", async () => {
    const send = vi.fn(() => Promise.reject(httpError(500)));

    const { batcher } = setup({ send });
    const result = batcher.submit("a");

    const assertion = expect(result).rejects.toMatchObject({ status: 500 });

    await vi.advanceTimersByTimeAsync(5000);
    await assertion;

    expect(send).toHaveBeenCalledTimes(1);
  });

  it("keeps working after a batch fails", async () => {
    const send = vi.fn<(items: string[]) => Promise<(string | Error)[]>>();

    send.mockRejectedValueOnce(httpError(500));
    send.mockImplementation((items) => Promise.resolve(items));

    const { batcher } = setup({ send });

    const failed = batcher.submit("a");
    const assertion = expect(failed).rejects.toBeInstanceOf(ApiError);

    await vi.advanceTimersByTimeAsync(50);
    await assertion;

    const next = batcher.submit("b");

    await vi.advanceTimersByTimeAsync(1000);

    await expect(next).resolves.toBe("b");
  });
});

describe("isRetriableBatchFailure", () => {
  it.each([429, 502, 503, 504])("retries status %i", (status) => {
    expect(isRetriableBatchFailure(httpError(status))).toBe(true);
  });

  it.each([400, 401, 403, 404, 500])("does not retry status %i", (status) => {
    expect(isRetriableBatchFailure(httpError(status))).toBe(false);
  });

  it("retries a network failure", () => {
    expect(isRetriableBatchFailure(new ApiError("down", { kind: "network" }))).toBe(true);
  });

  it("does not retry a timeout, because the server may still be working", () => {
    expect(isRetriableBatchFailure(new ApiError("slow", { kind: "timeout" }))).toBe(false);
  });

  it("does not retry an unrelated error", () => {
    expect(isRetriableBatchFailure(new Error("bug"))).toBe(false);
  });
});
