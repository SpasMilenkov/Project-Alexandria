import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { DirectorySummaryDto } from "@/api/directory";

import DirectoryItem from "@/components/dashboard/file-system/DirectoryItem.vue";
import { isTooltipTargetFor, tooltipTargetId } from "@/features/explorer/tooltip-target";

vi.mock("@pinia/colada", () => ({
  defineQueryOptions: (fn: unknown) => fn,
  useQueryCache: () => ({ ensure: vi.fn(), refresh: vi.fn() }),
}));

const makeDirectory = (id: string): DirectorySummaryDto => ({
  createdAt: "2026-01-01T00:00:00.000Z",
  id,
  isStarred: false,
  name: `Folder ${id}`,
  ownerUserDto: { email: "owner@test.com", id: "owner", name: "Owner" },
  parentId: "",
  size: 2048,
  updatedAt: "2026-01-02T00:00:00.000Z",
});

describe("DirectoryItem tooltip intent", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  const mountRow = (props?: { describedBy?: string | null; viewMode?: "grid" | "list" }) =>
    mount(DirectoryItem, {
      global: { plugins: [createPinia()] },
      props: {
        data: makeDirectory("d1"),
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

  it.each(["grid", "list"] as const)("reports pointer intent in %s view", async (viewMode) => {
    const wrapper = mountRow({ viewMode });
    const button = wrapper.find("button");

    await button.trigger("pointerenter");
    const entered = wrapper.emitted("tooltip-enter");
    expect(entered?.length).toBe(1);
    expect(entered?.[0]?.[0]).toEqual(makeDirectory("d1"));
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
    expect(
      mountRow({ describedBy: "explorer-item-tooltip" })
        .find("button")
        .attributes("aria-describedby"),
    ).toBe("explorer-item-tooltip");
  });
});

describe("explorer tooltip targets", () => {
  const directory = makeDirectory("same-id");

  it("reads the id of either row kind", () => {
    expect(tooltipTargetId({ directory, kind: "directory" })).toBe("same-id");
  });

  it("never matches a folder against a file with the same id", () => {
    const target = { directory, kind: "directory" as const };

    expect(isTooltipTargetFor(target, "directory", "same-id")).toBe(true);
    expect(isTooltipTargetFor(target, "file", "same-id")).toBe(false);
    expect(isTooltipTargetFor(null, "directory", "same-id")).toBe(false);
  });
});
