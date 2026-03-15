import { acceptHMRUpdate, defineStore } from "pinia";
import { ref } from "vue";

import type { ExplorerTab } from "@/types/explorer-tab";

import { logger } from "@/utils/logger";

export const useTabStore = defineStore(
  "tab",
  () => {
    const tabs = ref<ExplorerTab[]>([]);
    const activeTabId = ref<string | null>(null);

    const createTab = (activeDirId: string | null) => {
      logger.log("CREATING TAB");
      const id = crypto.randomUUID();
      tabs.value.push({
        activeDirId,
        id,
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

    const getTab = (tabId: string) => tabs.value.find((t) => t.id === tabId);

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
      closeTab,
      createTab,
      getTab,
      setActiveDir,
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
