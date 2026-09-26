import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it } from "vitest";

import type { DirectorySummaryDto } from "@/api/directory";

import DirectoryItem from "@/components/dashboard/file-system/DirectoryItem.vue";
import FolderDetailsDrawer from "@/components/dashboard/file-system/FolderDetailsDrawer.vue";

const makeDir = (id: string, name?: string): DirectorySummaryDto => ({
  createdAt: "2026-01-01T00:00:00.000Z",
  id,
  name: name ?? `Dir ${id}`,
  ownerUserDto: { email: "owner@test.com", id: "owner", name: "Owner" },
  parentId: null,
  updatedAt: "2026-01-02T00:00:00.000Z",
});

const drawerStub = {
  name: "DrawerStub",
  props: ["open", "title", "description", "direction", "ui", "handleOnly"],
  template: '<div class="drawer-stub"><slot name="body" /></div>',
};

const mountRow = (dir: DirectorySummaryDto) =>
  mount(DirectoryItem, {
    global: { plugins: [createPinia()] },
    props: { data: dir, isSelected: false, viewMode: "list" },
  });

const mountDrawer = (directory: DirectorySummaryDto | null) =>
  mount(FolderDetailsDrawer, {
    global: {
      plugins: [createPinia()],
      stubs: { Drawer: drawerStub, PolicySection: true, UDrawer: drawerStub },
    },
    props: { directory },
  });

const findDrawer = (wrapper: ReturnType<typeof mountDrawer>) =>
  wrapper.findComponent({ name: "DrawerStub" });

const clickByText = async (wrapper: ReturnType<typeof mountDrawer>, text: string) => {
  const buttons = wrapper.findAll("button");
  const target = buttons.find((button) => button.text().includes(text));
  expect(target).toBeDefined();
  await target!.trigger("click");
};

describe("directory details shared ownership", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("creates no drawer controller per row", () => {
    const wrapper = mountRow(makeDir("d1"));
    expect(findDrawer(wrapper).exists()).toBe(false);
    expect(
      (wrapper.vm as unknown as Record<string, unknown>).openDetails,
    ).toBeUndefined();
  });

  it("creates no menu controller per row and marks the target", () => {
    const wrapper = mountRow(makeDir("d1"));
    expect(wrapper.findComponent({ name: "UContextMenu" }).exists()).toBe(false);
    expect(wrapper.findComponent({ name: "ContextMenu" }).exists()).toBe(false);
    expect(wrapper.find("[data-dir-id='d1']").exists()).toBe(true);
  });

  it("rows depend only on individual display state", () => {
    const wrapper = mountRow(makeDir("d1"));
    const props = wrapper.vm.$options.props as Record<string, unknown>;
    expect(props).not.toHaveProperty("selectedCount");
  });

  it("forwards clicks for selection without menu emissions", async () => {
    const wrapper = mountRow(makeDir("d1"));
    await wrapper.find("button").trigger("click");
    expect(wrapper.emitted("click")?.length).toBe(1);
    expect(wrapper.emitted("open-details")).toBeUndefined();
  });

  it("keeps folder navigation on double-click", async () => {
    const wrapper = mountRow(makeDir("d1", "Projects"));
    await wrapper.find("button").trigger("dblclick");
    expect(wrapper.emitted("navigate")?.[0]).toEqual(["d1", "Projects"]);
  });
});

describe("FolderDetailsDrawer", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("stays closed without a target and opens from the shared target", async () => {
    const wrapper = mountDrawer(null);
    expect(findDrawer(wrapper).props("open")).toBe(false);

    await wrapper.setProps({ directory: makeDir("d1", "Projects") });
    const drawer = findDrawer(wrapper);
    expect(drawer.props("open")).toBe(true);
    expect(drawer.props("title")).toBe("Projects");
    expect(drawer.props("description")).toContain("Created");
  });

  it("keeps the last target rendered through the close transition", async () => {
    const wrapper = mountDrawer(makeDir("d1", "Projects"));
    await wrapper.setProps({ directory: null });

    const drawer = findDrawer(wrapper);
    expect(drawer.props("open")).toBe(false);
    expect(drawer.props("title")).toBe("Projects");
  });

  it("retargets to a new folder while open", async () => {
    const wrapper = mountDrawer(makeDir("d1", "Projects"));
    await wrapper.setProps({ directory: makeDir("d2", "Archive") });
    expect(findDrawer(wrapper).props("title")).toBe("Archive");
  });

  it("closes through the drawer and releases the target", async () => {
    const wrapper = mountDrawer(makeDir("d1", "Projects"));
    await findDrawer(wrapper).vm.$emit("update:open", false);
    expect(wrapper.emitted("update:directory")?.[0]).toEqual([null]);
  });

  it("emits navigate with captured ids on Open Directory", async () => {
    const wrapper = mountDrawer(makeDir("d1", "Projects"));
    await clickByText(wrapper, "Open Directory");
    expect(wrapper.emitted("update:directory")?.[0]).toEqual([null]);
    expect(wrapper.emitted("navigate")?.[0]).toEqual(["d1", "Projects"]);
  });

  it("emits rename, move, download and delete with captured ids", async () => {
    const wrapper = mountDrawer(makeDir("d1", "Projects"));
    await clickByText(wrapper, "Rename");
    expect(wrapper.emitted("rename")?.[0]).toEqual(["d1"]);

    await clickByText(wrapper, "Move");
    expect(wrapper.emitted("move")?.[0]).toEqual(["d1"]);

    await clickByText(wrapper, "Download");
    expect(wrapper.emitted("download")?.[0]).toEqual([["d1"]]);

    await clickByText(wrapper, "Delete");
    expect(wrapper.emitted("delete")?.[0]).toEqual([["d1"]]);
  });
});
