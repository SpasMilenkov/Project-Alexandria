import { afterEach, describe, expect, it, vi } from "vitest";
import { effectScope } from "vue";

import {
  type MoveRecord,
  ROOT_DROP_ID,
  dropInvalidReason,
  fromDropId,
  toDropId,
  useExplorerDragSession,
} from "@/composables/useExplorerDragSession";
import { createDwellTimer } from "@/features/explorer/drag/dwell-timer";
import { describeMove, executeExplorerMove, reverseMove } from "@/features/explorer/move";

const record = (overrides: Partial<MoveRecord> = {}): MoveRecord => ({
  destinationId: "photos",
  destinationName: "Photos",
  directoryIds: ["d1"],
  fileIds: ["f1", "f2"],
  leadName: "beach.jpg",
  originId: null,
  originName: "Home",
  ...overrides,
});

describe("explorer drag session", () => {
  afterEach(() => {
    useExplorerDragSession().end();
    vi.useRealTimers();
  });

  it("maps the root folder to a sentinel drop id and back", () => {
    expect(toDropId(null)).toBe(ROOT_DROP_ID);
    expect(fromDropId(ROOT_DROP_ID)).toBeNull();
    expect(fromDropId("abc")).toBe("abc");
  });

  it("rejects dropping a folder into itself or back into its origin", () => {
    const session = { directoryIds: ["d1"], originId: "parent" };

    expect(dropInvalidReason("d1", session)).toBe("self");
    expect(dropInvalidReason("parent", session)).toBe("origin");
    expect(dropInvalidReason(null, session)).toBeNull();
    expect(dropInvalidReason(null, { directoryIds: [], originId: null })).toBe("origin");
  });

  it("shares one session across every caller", () => {
    const first = useExplorerDragSession();
    const second = useExplorerDragSession();

    first.start({
      directoryIds: ["d1"],
      fileIds: ["f1"],
      lead: { icon: "mdi:folder", id: "d1", kind: "directory", name: "Docs" },
      originId: null,
      originName: "Home",
      point: { x: 0, y: 0 },
      pointerId: 1,
      pointerType: "mouse",
    });

    expect(second.isDragging.value).toBe(true);
    expect(second.session.value?.count).toBe(2);
    expect(second.session.value?.sourceIds.has("f1")).toBe(true);

    second.end();
    expect(first.isDragging.value).toBe(false);
  });

  it("stops notifying move listeners when their scope is disposed", () => {
    const listener = vi.fn();
    const scope = effectScope();

    scope.run(() => useExplorerDragSession().onMoved(listener));
    useExplorerDragSession().notifyMoved(record());
    scope.stop();
    useExplorerDragSession().notifyMoved(record());

    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("tracks pending move counts without going negative", () => {
    const dragSession = useExplorerDragSession();

    dragSession.beginMove(3);
    expect(dragSession.pendingMoveCount.value).toBe(3);
    dragSession.settleMove(3);
    dragSession.settleMove(1);
    expect(dragSession.pendingMoveCount.value).toBe(0);
  });

  it("fires a dwell only when the same key is held long enough", () => {
    vi.useFakeTimers();
    const onDwell = vi.fn();
    const dwell = createDwellTimer<string>(600, onDwell);

    dwell.track("a", "a");
    vi.advanceTimersByTime(400);
    dwell.track("a", "a");
    vi.advanceTimersByTime(200);
    expect(onDwell).toHaveBeenCalledWith("a");

    dwell.track("b", "b");
    vi.advanceTimersByTime(300);
    dwell.track(null);
    vi.advanceTimersByTime(600);
    expect(onDwell).toHaveBeenCalledTimes(1);

    dwell.track("b", "b");
    dwell.reset();
    vi.advanceTimersByTime(600);
    expect(onDwell).toHaveBeenCalledTimes(1);
  });
});

describe("explorer move execution", () => {
  it("moves files and folders through their own endpoints in parallel", async () => {
    const moveFiles = vi.fn().mockResolvedValue(undefined);
    const moveDirectories = vi.fn().mockResolvedValue(undefined);

    const outcome = await executeExplorerMove(record(), { moveDirectories, moveFiles });

    expect(outcome.errors).toEqual([]);
    expect(moveFiles).toHaveBeenCalledWith({
      destinationId: "photos",
      fileIds: ["f1", "f2"],
      originId: null,
    });
    expect(moveDirectories).toHaveBeenCalledWith({
      destinationId: "photos",
      directoryIds: ["d1"],
      originId: null,
    });
  });

  it("skips an endpoint with nothing to move and reports failures", async () => {
    const failure = new Error("Files with the same names already exist in destination");
    const moveFiles = vi.fn().mockRejectedValue(failure);
    const moveDirectories = vi.fn();

    const outcome = await executeExplorerMove(record({ directoryIds: [] }), {
      moveDirectories,
      moveFiles,
    });

    expect(moveDirectories).not.toHaveBeenCalled();
    expect(outcome.errors).toEqual([failure]);
  });

  it("undoes a move by swapping origin and destination", () => {
    const undo = reverseMove(record());

    expect(undo.originId).toBe("photos");
    expect(undo.destinationId).toBeNull();
    expect(undo.destinationName).toBe("Home");
    expect(undo.fileIds).toEqual(["f1", "f2"]);
  });

  it("describes single and multi-item moves", () => {
    expect(describeMove(record())).toBe("Moved 3 items to Photos");
    expect(describeMove(record({ directoryIds: [], fileIds: ["f1"] }))).toBe(
      'Moved "beach.jpg" to Photos',
    );
  });
});
