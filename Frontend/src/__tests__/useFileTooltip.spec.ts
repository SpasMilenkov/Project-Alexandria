import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ref } from "vue";

import type { FileResult } from "@/api/file";

import { useFileTooltip } from "@/composables/useFileTooltip";

const makeFile = (fileId: string): FileResult => ({
  createdAt: "2026-01-01T00:00:00.000Z",
  currentVersion: {
    id: "v1",
    isDeleted: false,
    isEncrypted: false,
    mimeType: "text/plain",
    size: "1024",
    versionNumber: 1,
  },
  deletedAt: null,
  directoryId: null,
  fileId,
  fileName: `${fileId}.txt`,
  mimeType: "text/plain",
  owner: { email: "owner@test.com", id: "owner", name: "Owner" },
  tags: [],
  updatedAt: null,
});

const anchors: HTMLElement[] = [];

const makeAnchor = (): HTMLElement => {
  const anchor = document.createElement("button");
  document.body.appendChild(anchor);
  anchors.push(anchor);
  return anchor;
};

describe("useFileTooltip", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    anchors.splice(0).forEach((anchor) => anchor.remove());
  });

  it("opens after the pointer delay", () => {
    const tooltip = useFileTooltip();
    tooltip.enter(makeFile("f1"), makeAnchor(), "pointer");
    expect(tooltip.open.value).toBe(false);
    vi.advanceTimersByTime(599);
    expect(tooltip.open.value).toBe(false);
    vi.advanceTimersByTime(1);
    expect(tooltip.open.value).toBe(true);
    expect(tooltip.file.value?.fileId).toBe("f1");
  });

  it("cancels a pending open when the pointer leaves early", () => {
    const tooltip = useFileTooltip();
    tooltip.enter(makeFile("f1"), makeAnchor(), "pointer");
    vi.advanceTimersByTime(300);
    tooltip.leave("pointer");
    vi.advanceTimersByTime(1000);
    expect(tooltip.open.value).toBe(false);
  });

  it("opens immediately on keyboard focus", () => {
    const tooltip = useFileTooltip();
    tooltip.enter(makeFile("f1"), makeAnchor(), "focus");
    expect(tooltip.open.value).toBe(true);
  });

  it("closes immediately on blur", () => {
    const tooltip = useFileTooltip();
    tooltip.enter(makeFile("f1"), makeAnchor(), "focus");
    tooltip.leave("focus");
    expect(tooltip.open.value).toBe(false);
  });

  it("swaps content without closing on rapid target changes", () => {
    const tooltip = useFileTooltip();
    const first = makeAnchor();
    tooltip.enter(makeFile("f1"), first, "pointer");
    vi.advanceTimersByTime(600);
    expect(tooltip.open.value).toBe(true);
    tooltip.leave("pointer");
    tooltip.enter(makeFile("f2"), makeAnchor(), "pointer");
    expect(tooltip.open.value).toBe(true);
    expect(tooltip.file.value?.fileId).toBe("f2");
    vi.advanceTimersByTime(1000);
    expect(tooltip.open.value).toBe(true);
  });

  it("closes after the grace period when the pointer leaves", () => {
    const tooltip = useFileTooltip();
    tooltip.enter(makeFile("f1"), makeAnchor(), "pointer");
    vi.advanceTimersByTime(600);
    tooltip.leave("pointer");
    expect(tooltip.open.value).toBe(true);
    vi.advanceTimersByTime(199);
    expect(tooltip.open.value).toBe(true);
    vi.advanceTimersByTime(1);
    expect(tooltip.open.value).toBe(false);
  });

  it("keeps the tooltip open while the pointer is over its content", () => {
    const tooltip = useFileTooltip();
    tooltip.enter(makeFile("f1"), makeAnchor(), "pointer");
    vi.advanceTimersByTime(600);
    tooltip.leave("pointer");
    tooltip.contentEnter();
    vi.advanceTimersByTime(1000);
    expect(tooltip.open.value).toBe(true);
    tooltip.contentLeave();
    expect(tooltip.open.value).toBe(false);
  });

  it("dismiss cancels pending work and closes", () => {
    const tooltip = useFileTooltip();
    tooltip.enter(makeFile("f1"), makeAnchor(), "pointer");
    tooltip.dismiss();
    vi.advanceTimersByTime(1000);
    expect(tooltip.open.value).toBe(false);
  });

  it("releases anchor and item references", () => {
    const tooltip = useFileTooltip();
    tooltip.enter(makeFile("f1"), makeAnchor(), "pointer");
    vi.advanceTimersByTime(600);
    tooltip.release();
    expect(tooltip.open.value).toBe(false);
    expect(tooltip.file.value).toBeNull();
    expect(tooltip.anchor.value).toBeNull();
  });

  it("aborts a pending open when the anchor disconnects", () => {
    const tooltip = useFileTooltip();
    const anchor = makeAnchor();
    document.body.appendChild(anchor);
    tooltip.enter(makeFile("f1"), anchor, "pointer");
    anchor.remove();
    vi.advanceTimersByTime(600);
    expect(tooltip.open.value).toBe(false);
  });

  it("ignores intent while disabled", () => {
    const disabled = ref(true);
    const tooltip = useFileTooltip({ disabled });
    tooltip.enter(makeFile("f1"), makeAnchor(), "pointer");
    tooltip.enter(makeFile("f1"), makeAnchor(), "focus");
    vi.advanceTimersByTime(1000);
    expect(tooltip.open.value).toBe(false);
    expect(tooltip.file.value).toBeNull();
  });

  it("closing through the open setter cancels pending work", () => {
    const tooltip = useFileTooltip();
    tooltip.enter(makeFile("f1"), makeAnchor(), "pointer");
    tooltip.open.value = false;
    vi.advanceTimersByTime(1000);
    expect(tooltip.open.value).toBe(false);
  });
});
