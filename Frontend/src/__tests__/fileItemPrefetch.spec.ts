import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { FileResult } from "@/api/file";

import FileItem from "@/components/dashboard/file-system/FileItem.vue";

const refresh = vi.fn();
const ensure = vi.fn((query: unknown) => query);
vi.mock("@pinia/colada", () => ({
  defineQueryOptions: (fn: unknown) => fn,
  useQueryCache: () => ({ ensure, refresh }),
}));

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

describe("FileItem prefetch", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.useFakeTimers();
    refresh.mockClear();
    ensure.mockClear();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  const mountRow = () =>
    mount(FileItem, {
      global: { plugins: [createPinia()] },
      props: { data: makeFile("f1"), isSelected: false, viewMode: "list" },
    });

  it("warms the details drawer queries after the hover delay", async () => {
    const wrapper = mountRow();
    await wrapper.find("button").trigger("mouseenter");
    expect(refresh).not.toHaveBeenCalled();
    vi.advanceTimersByTime(150);
    expect(ensure).toHaveBeenCalledTimes(4);
    expect(refresh).toHaveBeenCalledTimes(4);
  });

  it("cancels a pending warmup when interest moves away", async () => {
    const wrapper = mountRow();
    await wrapper.find("button").trigger("mouseenter");
    vi.advanceTimersByTime(100);
    await wrapper.find("button").trigger("pointerleave");
    vi.advanceTimersByTime(1000);
    expect(refresh).not.toHaveBeenCalled();
  });

  it("starts no delayed requests after the row unmounts", async () => {
    const wrapper = mountRow();
    await wrapper.find("button").trigger("mouseenter");
    wrapper.unmount();
    vi.advanceTimersByTime(1000);
    expect(refresh).not.toHaveBeenCalled();
  });

  it("rows depend only on individual display state", () => {
    const wrapper = mountRow();
    const props = wrapper.vm.$options.props as Record<string, unknown>;
    expect(props).not.toHaveProperty("selectedCount");
    expect(props).not.toHaveProperty("tags");
  });
});
