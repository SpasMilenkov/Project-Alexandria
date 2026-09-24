import { createPinia, setActivePinia } from "pinia";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { createApp, defineComponent, h, nextTick } from "vue";

import type { DirectorySummaryDto } from "@/api/directory";

const makeDir = (id: string): DirectorySummaryDto => ({
  createdAt: new Date().toISOString(),
  id,
  name: `Dir ${id}`,
  ownerUserDto: { email: "owner@test.com", id: "owner", name: "Owner" },
  parentId: null,
  updatedAt: new Date().toISOString(),
});

const keyEventOn = (target: Element, key: string): KeyboardEvent => {
  const event = new KeyboardEvent("keydown", { bubbles: true, key });
  target.dispatchEvent(event);
  return event;
};

describe("useExplorerCommands", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  const mountGuard = async (tabId: string, deps?: { hasOpenOverlay?: () => boolean }) => {
    const { useExplorerCommandGuard } = await import("@/composables/useExplorerCommands");
    let guard!: ReturnType<typeof useExplorerCommandGuard>;
    const defaults = { hasOpenOverlay: () => false, isActiveTab: () => true, ...deps };
    const app = createApp(
      defineComponent({
        setup() {
          guard = useExplorerCommandGuard(tabId, defaults);
          return () => null;
        },
      }),
    );
    app.mount(document.createElement("div"));
    return { app, guard };
  };

  it("allows plain background input on the active tab", async () => {
    const { app, guard } = await mountGuard("tab-1");
    const target = document.createElement("div");
    document.body.appendChild(target);
    expect(guard.canHandleCommand(keyEventOn(target, "d"))).toBe(true);
    target.remove();
    app.unmount();
  });

  it("blocks commands when another tab is active", async () => {
    const { useExplorerCommandGuard } = await import("@/composables/useExplorerCommands");
    const { useTabStore } = await import("@/stores/tab");
    let guard!: ReturnType<typeof useExplorerCommandGuard>;
    const app = createApp(
      defineComponent({
        setup() {
          guard = useExplorerCommandGuard("tab-1");
          return () => null;
        },
      }),
    );
    app.mount(document.createElement("div"));
    const tabStore = useTabStore();
    tabStore.activeTabId = "tab-2";
    await nextTick();
    const target = document.createElement("div");
    document.body.appendChild(target);
    expect(guard.canHandleCommand(keyEventOn(target, "d"))).toBe(false);
    target.remove();
    app.unmount();
  });

  it("blocks commands while an overlay owns input", async () => {
    const { app, guard } = await mountGuard("tab-1", { hasOpenOverlay: () => true });
    const target = document.createElement("div");
    document.body.appendChild(target);
    expect(guard.canHandleCommand(keyEventOn(target, "d"))).toBe(false);
    expect(guard.canHandleCommand(keyEventOn(target, "Delete"))).toBe(false);
    target.remove();
    app.unmount();
  });

  it("blocks commands from editable targets", async () => {
    const { app, guard } = await mountGuard("tab-1");
    for (const tag of ["input", "textarea", "select"]) {
      const target = document.createElement(tag);
      document.body.appendChild(target);
      expect(guard.canHandleCommand(keyEventOn(target, "d"))).toBe(false);
      target.remove();
    }
    const editable = document.createElement("div");
    editable.setAttribute("contenteditable", "true");
    document.body.appendChild(editable);
    expect(guard.canHandleCommand(keyEventOn(editable, "d"))).toBe(false);
    editable.remove();
    app.unmount();
  });

  it("blocks commands from dialog, menu, and listbox surfaces", async () => {
    const { app, guard } = await mountGuard("tab-1");
    for (const role of ["dialog", "alertdialog", "menu", "listbox"]) {
      const surface = document.createElement("div");
      surface.setAttribute("role", role);
      const target = document.createElement("button");
      surface.appendChild(target);
      document.body.appendChild(surface);
      expect(guard.canHandleCommand(keyEventOn(target, "d"))).toBe(false);
      surface.remove();
    }
    app.unmount();
  });

  it("treats a missing target as explorer input", async () => {
    const { app, guard } = await mountGuard("tab-1");
    expect(guard.canHandleCommand(new KeyboardEvent("keydown", { key: "d" }))).toBe(true);
    app.unmount();
  });
});

describe("explorer shortcut ownership", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("registers one window keydown owner that releases on disposal", async () => {
    const added: string[] = [];
    const removed: string[] = [];
    const rawAdd = window.addEventListener;
    const rawRemove = window.removeEventListener;
    vi.spyOn(window, "addEventListener").mockImplementation(((type: string, ...rest: unknown[]) => {
      if (type === "keydown") added.push(type);
      return (rawAdd as (...args: unknown[]) => void).call(window, type, ...(rest as []));
    }) as typeof window.addEventListener);
    vi.spyOn(window, "removeEventListener").mockImplementation(((
      type: string,
      ...rest: unknown[]
    ) => {
      if (type === "keydown") removed.push(type);
      return (rawRemove as (...args: unknown[]) => void).call(window, type, ...(rest as []));
    }) as typeof window.removeEventListener);

    let calls = 0;
    const app = createApp(
      defineComponent({
        setup() {
          defineShortcuts({ d: () => void (calls += 1) });
          return () => h("div");
        },
      }),
    );
    app.mount(document.createElement("div"));
    expect(added.filter((type) => type === "keydown")).toHaveLength(1);

    window.dispatchEvent(new KeyboardEvent("keydown", { bubbles: true, key: "d" }));
    expect(calls).toBe(1);

    app.unmount();
    expect(removed.filter((type) => type === "keydown")).toHaveLength(1);

    window.dispatchEvent(new KeyboardEvent("keydown", { bubbles: true, key: "d" }));
    expect(calls).toBe(1);

    vi.restoreAllMocks();
  });

  it("adds no per-folder keydown listeners and fires no row actions on D", async () => {
    const { default: DirectoryItem } =
      await import("@/components/dashboard/file-system/DirectoryItem.vue");
    const rawAdd = window.addEventListener;

    const countKeydownAdded = async (rowCount: number): Promise<number> => {
      let added = 0;
      const spy = vi.spyOn(window, "addEventListener").mockImplementation(((
        type: string,
        listener: EventListener,
        ...rest: unknown[]
      ) => {
        if (type === "keydown") added += 1;
        return (rawAdd as (...args: unknown[]) => void).call(
          window,
          type,
          listener,
          ...(rest as []),
        );
      }) as typeof window.addEventListener);
      const app = createApp(
        defineComponent({
          setup() {
            return () =>
              h(
                "div",
                Array.from({ length: rowCount }, (_, index) =>
                  h(DirectoryItem as never, {
                    data: makeDir(`d${index}`),
                    isSelected: false,
                    selectedCount: 1,
                    viewMode: "list",
                  }),
                ),
              );
          },
        }),
      );
      app.mount(document.createElement("div"));
      app.unmount();
      spy.mockRestore();
      return added;
    };

    expect(await countKeydownAdded(1)).toBe(await countKeydownAdded(5));

    const downloads: unknown[][] = [];
    const renames: unknown[][] = [];
    const deletes: unknown[][] = [];
    const ids = ["d1", "d2", "d3", "d4", "d5"];
    const app = createApp(
      defineComponent({
        setup() {
          return () =>
            h(
              "div",
              ids.map((id) =>
                h(DirectoryItem as never, {
                  data: makeDir(id),
                  isSelected: false,
                  onDelete: (...args: unknown[]) => void deletes.push(args),
                  onDownload: (...args: unknown[]) => void downloads.push(args),
                  onRename: (...args: unknown[]) => void renames.push(args),
                  selectedCount: 1,
                  viewMode: "list",
                }),
              ),
            );
        },
      }),
    );
    app.mount(document.createElement("div"));

    window.dispatchEvent(new KeyboardEvent("keydown", { bubbles: true, key: "d" }));
    window.dispatchEvent(new KeyboardEvent("keydown", { bubbles: true, key: "r" }));
    window.dispatchEvent(new KeyboardEvent("keydown", { bubbles: true, key: "Delete" }));
    expect(downloads).toHaveLength(0);
    expect(renames).toHaveLength(0);
    expect(deletes).toHaveLength(0);

    app.unmount();
    vi.restoreAllMocks();
  }, 60000);
});
