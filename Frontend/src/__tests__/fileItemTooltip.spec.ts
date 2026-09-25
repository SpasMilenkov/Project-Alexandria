import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { FileResult } from "@/api/file";

import FileItem from "@/components/dashboard/file-system/FileItem.vue";

vi.mock("@pinia/colada", () => ({
  defineQueryOptions: (fn: unknown) => fn,
  useQueryCache: () => ({ ensure: vi.fn(), refresh: vi.fn() }),
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

describe("FileItem tooltip intent", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  const mountRow = (props?: { describedBy?: string | null }) =>
    mount(FileItem, {
      global: { plugins: [createPinia()] },
      props: {
        data: makeFile("f1"),
        isSelected: false,
        viewMode: "list",
        ...props,
      },
    });

  it("creates no tooltip controller per row", () => {
    const wrapper = mountRow();
    expect(wrapper.findComponent({ name: "UTooltip" }).exists()).toBe(false);
    expect(wrapper.findComponent({ name: "Tooltip" }).exists()).toBe(false);
  });

  it("creates no menu controller per row and marks the target", () => {
    const wrapper = mountRow();
    expect(wrapper.findComponent({ name: "UContextMenu" }).exists()).toBe(false);
    expect(wrapper.findComponent({ name: "ContextMenu" }).exists()).toBe(false);
    expect(wrapper.find("[data-file-id='f1']").exists()).toBe(true);
  });

  it("reports pointer intent with the trigger anchor", async () => {
    const wrapper = mountRow();
    const button = wrapper.find("button");
    await button.trigger("pointerenter");
    const entered = wrapper.emitted("tooltip-enter");
    expect(entered?.length).toBe(1);
    expect(entered?.[0]?.[0]).toEqual(makeFile("f1"));
    expect(entered?.[0]?.[1]).toBe(button.element);
    expect(entered?.[0]?.[2]).toBe("pointer");
    await button.trigger("pointerleave");
    expect(wrapper.emitted("tooltip-leave")).toEqual([["pointer"]]);
  });

  it("reports focus intent for keyboard users", async () => {
    const wrapper = mountRow();
    const button = wrapper.find("button");
    await button.trigger("focus");
    expect(wrapper.emitted("tooltip-enter")?.[0]?.[2]).toBe("focus");
    await button.trigger("blur");
    expect(wrapper.emitted("tooltip-leave")).toEqual([["focus"]]);
  });

  it("suppresses pointer intent for touch", async () => {
    const wrapper = mountRow();
    await wrapper.find("button").trigger("pointerenter", { pointerType: "touch" });
    expect(wrapper.emitted("tooltip-enter")).toBeUndefined();
  });

  it("associates the trigger with the shared tooltip content", () => {
    expect(mountRow().find("button").attributes("aria-describedby")).toBeUndefined();
    expect(mountRow({ describedBy: "explorer-file-tooltip" }).find("button").attributes(
      "aria-describedby",
    )).toBe("explorer-file-tooltip");
  });
});
