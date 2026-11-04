import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  SESSION_BOUNDARY_CHANNEL,
  SESSION_EPOCH_STORAGE_KEY,
  isSessionEpochCurrent,
  rotateSessionEpoch,
  startSessionBoundaryWatcher,
  stopSessionBoundaryWatcher,
} from "@/utils/sessionBoundary";

class FakeBroadcastChannel {
  static instances: FakeBroadcastChannel[] = [];

  readonly name: string;
  closed = false;
  messages: unknown[] = [];

  private listeners = new Set<(event: MessageEvent) => void>();

  constructor(name: string) {
    this.name = name;
    FakeBroadcastChannel.instances.push(this);
  }

  addEventListener(type: string, listener: EventListener): void {
    if (type !== "message") return;

    this.listeners.add(listener as (event: MessageEvent) => void);
  }

  removeEventListener(type: string, listener: EventListener): void {
    if (type !== "message") return;

    this.listeners.delete(listener as (event: MessageEvent) => void);
  }

  postMessage(message: unknown): void {
    this.messages.push(message);
  }

  close(): void {
    this.closed = true;
  }

  emit(data: unknown): void {
    const event = { data } as MessageEvent;

    this.listeners.forEach((listener) => listener(event));
  }
}

const setStoredEpoch = (epoch: string): void => {
  localStorage.setItem(SESSION_EPOCH_STORAGE_KEY, epoch);
};

beforeEach(() => {
  stopSessionBoundaryWatcher();
  localStorage.clear();
  FakeBroadcastChannel.instances = [];
  vi.stubGlobal("BroadcastChannel", FakeBroadcastChannel);
});

afterEach(() => {
  stopSessionBoundaryWatcher();
  localStorage.clear();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("session boundary watcher", () => {
  it("creates a baseline epoch and a dedicated channel", () => {
    const invalidate = vi.fn();

    startSessionBoundaryWatcher(invalidate);

    expect(localStorage.getItem(SESSION_EPOCH_STORAGE_KEY)).toBe("initial");
    expect(FakeBroadcastChannel.instances).toHaveLength(1);
    expect(FakeBroadcastChannel.instances[0]?.name).toBe(SESSION_BOUNDARY_CHANNEL);
    expect(invalidate).not.toHaveBeenCalled();
  });

  it("rotates the shared epoch and notifies peers", () => {
    const invalidate = vi.fn();

    startSessionBoundaryWatcher(invalidate);
    const previousEpoch = localStorage.getItem(SESSION_EPOCH_STORAGE_KEY);
    const nextEpoch = rotateSessionEpoch();

    expect(nextEpoch).toBeTruthy();
    expect(nextEpoch).not.toBe(previousEpoch);
    expect(localStorage.getItem(SESSION_EPOCH_STORAGE_KEY)).toBe(nextEpoch);
    expect(FakeBroadcastChannel.instances[0]?.messages).toContainEqual({
      type: "session-boundary",
    });
    expect(isSessionEpochCurrent()).toBe(true);
    expect(invalidate).not.toHaveBeenCalled();
  });

  it("adopts an external epoch from a storage event once", () => {
    const invalidate = vi.fn();

    startSessionBoundaryWatcher(invalidate);
    setStoredEpoch("remote-epoch");

    window.dispatchEvent(
      new StorageEvent("storage", {
        key: SESSION_EPOCH_STORAGE_KEY,
        newValue: "remote-epoch",
      }),
    );

    expect(invalidate).toHaveBeenCalledTimes(1);
    expect(isSessionEpochCurrent()).toBe(true);

    window.dispatchEvent(
      new StorageEvent("storage", {
        key: SESSION_EPOCH_STORAGE_KEY,
        newValue: "remote-epoch",
      }),
    );

    expect(invalidate).toHaveBeenCalledTimes(1);
  });

  it("checks the shared epoch after a channel notification", () => {
    const invalidate = vi.fn();

    startSessionBoundaryWatcher(invalidate);
    setStoredEpoch("remote-epoch");
    FakeBroadcastChannel.instances[0]?.emit({ type: "session-boundary" });

    expect(invalidate).toHaveBeenCalledTimes(1);
  });

  it("ignores a channel notification when the shared epoch is unchanged", () => {
    const invalidate = vi.fn();

    startSessionBoundaryWatcher(invalidate);
    FakeBroadcastChannel.instances[0]?.emit({ type: "unrelated" });
    FakeBroadcastChannel.instances[0]?.emit({ type: "session-boundary" });

    expect(invalidate).not.toHaveBeenCalled();
  });

  it("checks the shared epoch on focus, pageshow and visibility", () => {
    const invalidate = vi.fn();
    const hidden = vi.spyOn(document, "hidden", "get").mockReturnValue(false);

    startSessionBoundaryWatcher(invalidate);

    setStoredEpoch("focus-epoch");
    window.dispatchEvent(new Event("focus"));
    expect(invalidate).toHaveBeenCalledTimes(1);

    setStoredEpoch("pageshow-epoch");
    window.dispatchEvent(new Event("pageshow"));
    expect(invalidate).toHaveBeenCalledTimes(2);

    setStoredEpoch("visibility-epoch");
    document.dispatchEvent(new Event("visibilitychange"));
    expect(invalidate).toHaveBeenCalledTimes(3);

    hidden.mockRestore();
  });

  it("stops listening and closes the channel", () => {
    const invalidate = vi.fn();

    startSessionBoundaryWatcher(invalidate);
    const channel = FakeBroadcastChannel.instances[0];

    stopSessionBoundaryWatcher();
    setStoredEpoch("remote-epoch");
    window.dispatchEvent(new Event("focus"));

    expect(channel?.closed).toBe(true);
    expect(invalidate).not.toHaveBeenCalled();
  });
});
