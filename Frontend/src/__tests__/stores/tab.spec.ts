import { beforeEach, describe, expect, it } from "vitest";
import { createPinia, setActivePinia } from "pinia";

import { useTabStore } from "@/stores/tab";

//oxlint-disable-next-line max-lines-per-function
describe("useTabStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("starts with no tabs and no active tab", () => {
    const store = useTabStore();
    expect(store.tabs).toHaveLength(0);
    expect(store.activeTabId).toBeUndefined();
  });

  it("createTab adds a tab and sets it as active", () => {
    const store = useTabStore();
    store.createTab(null);
    expect(store.tabs).toHaveLength(1);
    expect(store.activeTabId).toBe(store.tabs[0].id);
    expect(store.tabs[0].activeDirId).toBeNull();
    expect(store.tabs[0].title).toBe("New Tab");
  });

  it("createTab stores the activeDirId", () => {
    const store = useTabStore();
    store.createTab("dir-abc");
    expect(store.tabs[0].activeDirId).toBe("dir-abc");
  });

  it("createTab generates unique ids each time", () => {
    const store = useTabStore();
    store.createTab(null);
    store.createTab(null);
    expect(store.tabs[0].id).not.toBe(store.tabs[1].id);
  });

  it("closeTab removes the tab", () => {
    const store = useTabStore();
    store.createTab(null);
    store.createTab(null);
    const id = store.tabs[0].id;
    store.closeTab(id);
    expect(store.tabs.find((t) => t.id === id)).toBeUndefined();
  });

  it("closeTab activates the previous tab when closing the active one", () => {
    const store = useTabStore();
    store.createTab(null);
    store.createTab(null);
    const firstId = store.tabs[0].id;
    const secondId = store.tabs[1].id;
    store.closeTab(secondId);
    expect(store.activeTabId).toBe(firstId);
  });

  it("closeTab activates the first remaining tab when there is no previous", () => {
    const store = useTabStore();
    store.createTab(null);
    store.createTab(null);
    store.createTab(null);
    const firstId = store.tabs[0].id;
    store.activeTabId = firstId;
    store.closeTab(firstId);
    expect(store.activeTabId).toBe(store.tabs[0].id);
  });

  it("closeTab sets activeTabId to undefined when closing the last tab", () => {
    const store = useTabStore();
    store.createTab(null);
    const id = store.tabs[0].id;
    store.closeTab(id);
    expect(store.activeTabId).toBeNull();
  });

  it("closeAllTabs empties everything", () => {
    const store = useTabStore();
    store.createTab(null);
    store.createTab(null);
    store.closeAllTabs();
    expect(store.tabs).toHaveLength(0);
    expect(store.activeTabId).toBeUndefined();
  });

  it("getTab returns the correct tab by id", () => {
    const store = useTabStore();
    store.createTab(null);
    const id = store.tabs[0].id;
    const tab = store.getTab(id);
    expect(tab).toBe(store.tabs[0]);
  });

  it("getTab returns undefined for a non-existent id", () => {
    const store = useTabStore();
    expect(store.getTab("ghost")).toBeUndefined();
  });

  it("setActiveDir updates the tab's activeDirId", () => {
    const store = useTabStore();
    store.createTab(null);
    const id = store.tabs[0].id;
    store.setActiveDir(id, "dir-new");
    expect(store.tabs[0].activeDirId).toBe("dir-new");
  });

  it("updateTabTitle updates the tab's title", () => {
    const store = useTabStore();
    store.createTab(null);
    const id = store.tabs[0].id;
    store.updateTabTitle(id, "My Folder");
    expect(store.tabs[0].title).toBe("My Folder");
  });

  it("setActiveDir and updateTabTitle are no-ops for non-existent tabs", () => {
    const store = useTabStore();
    store.setActiveDir("ghost", "dir-x");
    store.updateTabTitle("ghost", "Title");
    expect(store.tabs).toHaveLength(0);
  });
});
