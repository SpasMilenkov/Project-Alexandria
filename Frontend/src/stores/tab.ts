import { acceptHMRUpdate, defineStore } from "pinia";
import { ref } from "vue";

import type { ExplorerTab } from "@/types/explorer-tab";

import { logger } from "@/utils/logger";

export const useTabStore = defineStore(
  "tab",
  () => {
    const tabs = ref<ExplorerTab[]>([]);
    const activeTabId = ref<string | undefined>();

    const createTab = (activeDirId: string | null, revealFileId?: string) => {
      logger.log("CREATING TAB");
      const id = crypto.randomUUID();
      tabs.value.push({
        activeDirId,
        id,
        revealFileId,
        title: "New Tab",
      });
      activeTabId.value = id;
      logger.log(activeTabId.value);
    };

    const closeTab = (tabId: string) => {
      const idx = tabs.value.findIndex((t) => t.id === tabId);
      tabs.value = tabs.value.filter((t) => t.id !== tabId);
      if (activeTabId.value === tabId) {
        activeTabId.value = tabs.value[idx - 1]?.id ?? tabs.value[0]?.id ?? null;
      }
    };

    const closeAllTabs = () => {
      tabs.value = [];
      activeTabId.value = undefined;
    };

    const resetSessionState = () => {
      closeAllTabs();
    };

    const getTab = (tabId: string) => tabs.value.find((t) => t.id === tabId);

    // Read once: a tab reopened later, or after a reload, should not jump back to the file.
    const takeRevealFileId = (tabId: string) => {
      const tab = tabs.value.find((t) => t.id === tabId);
      if (!tab?.revealFileId) return undefined;

      const fileId = tab.revealFileId;
      tab.revealFileId = undefined;
      return fileId;
    };

    const setActiveDir = (tabId: string, dirId: string | null) => {
      const tab = tabs.value.find((t) => t.id === tabId);
      if (tab) {
        tab.activeDirId = dirId;
      }
    };

    const updateTabTitle = (tabId: string, title: string) => {
      const tab = tabs.value.find((t) => t.id === tabId);
      if (tab) {
        tab.title = title;
      }
    };

    return {
      activeTabId,
      closeAllTabs,
      closeTab,
      createTab,
      getTab,
      setActiveDir,
      resetSessionState,
      takeRevealFileId,
      tabs,
      updateTabTitle,
    };
  },
  {
    persist: true,
  },
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useTabStore, import.meta.hot));
}
