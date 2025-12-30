import type { ExplorerTab } from "@/types/explorer-tab";
import { acceptHMRUpdate, defineStore } from "pinia";
import { ref } from "vue";

export const useTabStore = defineStore(
  "tab",
  () => {
    const tabs = ref<ExplorerTab[]>([]);
    const activeTabId = ref<string | null>(null);

    const createTab = (activeDirId: string | null) => {
      console.log("CREATING TAB");
      const id = crypto.randomUUID();

      tabs.value.push({
        id,
        title: "New Tab",
        activeDirId,
      });

      activeTabId.value = id;
      console.log(activeTabId.value);
    };

    const closeTab = (tabId: string) => {
      const idx = tabs.value.findIndex((t) => t.id === tabId);
      tabs.value = tabs.value.filter((t) => t.id !== tabId);

      if (activeTabId.value === tabId) {
        activeTabId.value =
          tabs.value[idx - 1]?.id ?? tabs.value[0]?.id ?? null;
      }
    };

    const getTab = (tabId: string) => {
      return tabs.value.find((t) => t.id === tabId);
    };

    const setActiveDir = (tabId: string, dirId: string | null) => {
      const tab = tabs.value.find((t) => t.id === tabId);
      if (tab) {
        tab.activeDirId = dirId;
      }
    };

    return {
      tabs,
      activeTabId,
      createTab,
      closeTab,
      getTab,
      setActiveDir,
    };
  },
  {
    persist: true,
  }
);

if (import.meta.hot) {
  import.meta.hot.accept(acceptHMRUpdate(useTabStore, import.meta.hot));
}
