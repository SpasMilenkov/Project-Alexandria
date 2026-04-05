<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useMediaQuery } from "@vueuse/core";
import { useTabStore } from "@/stores/tab";
import FileExplorer from "@/components/dashboard/file-system/FileExplorerTab.vue";
import { Icon } from "@iconify/vue";
import { logger } from "@/utils/logger";
import type { ExplorerTab } from "@/types/explorer-tab";

defineShortcuts({
  meta_shift_n: () => tabStore.createTab(null),
  meta_shift_q: () => {
    if (tabStore.activeTabId) {
      tabStore.closeTab(tabStore.activeTabId);
    }
  },
});

const tabStore = useTabStore();
const isMobile = useMediaQuery("(max-width: 767px)");

// Desktop UTabs items
const items = computed(() =>
  tabStore.tabs.map((tab: ExplorerTab) => ({
    icon: "i-heroicons-folder",
    label: tab.title,
    value: tab.id,
  })),
);

const hasNoTabs = computed(() => tabStore.tabs.length === 0);

// Mobile bottom bar: show max 3 tabs, rest go in the overflow sheet
const MOBILE_MAX_VISIBLE_TABS = 3;
const visibleTabs = computed(() => tabStore.tabs.slice(0, MOBILE_MAX_VISIBLE_TABS));
const overflowTabs = computed(() => tabStore.tabs.slice(MOBILE_MAX_VISIBLE_TABS));
const overflowCount = computed(() => overflowTabs.value.length);

// Manage tabs sheet state
const isManageSheetOpen = ref(false);

// Close all tabs confirmation modal
const isCloseAllModalOpen = ref(false);

const openManageSheet = () => {
  isManageSheetOpen.value = true;
};

const closeManageSheet = () => {
  isManageSheetOpen.value = false;
};

const openCloseAllModal = () => {
  isCloseAllModalOpen.value = true;
};

const confirmCloseAll = () => {
  tabStore.closeAllTabs();
  isCloseAllModalOpen.value = false;
  closeManageSheet();
};

const activateTab = (tabId: string) => {
  tabStore.activeTabId = tabId;
  closeManageSheet();
};

const closeTabFromSheet = (tabId: string) => {
  tabStore.closeTab(tabId);
  // If we closed the last overflow tab, close the sheet
  if (tabStore.tabs.length <= MOBILE_MAX_VISIBLE_TABS) {
    closeManageSheet();
  }
};

// Long-press support for mobile tab pills
const LONG_PRESS_DURATION = 500;
let longPressTimer: ReturnType<typeof setTimeout> | null = null;

const onTabPointerDown = (tabId: string) => {
  longPressTimer = setTimeout(() => {
    openManageSheet();
  }, LONG_PRESS_DURATION);
};

const onTabPointerUp = () => {
  if (longPressTimer) {
    clearTimeout(longPressTimer);
    longPressTimer = null;
  }
};

onMounted(() => {
  logger.log(tabStore.tabs.values);
  if (tabStore.tabs.length === 0) {
    tabStore.createTab(null);
  }
});
</script>

<template>
  <!-- Empty state -->
  <div v-if="hasNoTabs" class="flex items-center justify-center min-h-[calc(100vh-4rem)] w-full">
    <div class="text-center space-y-4 max-w-md px-4">
      <div
        class="inline-flex items-center justify-center w-16 h-16 rounded-full bg-primary/10 mb-2"
      >
        <UIcon name="i-heroicons-folder-open" class="w-8 h-8 text-primary" />
      </div>
      <div class="space-y-2">
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white">No tabs open</h3>
        <p class="text-sm text-gray-500 dark:text-gray-400">
          Create a new tab to start exploring your files and folders.
        </p>
      </div>
      <div class="flex flex-col sm:flex-row gap-3 justify-center pt-2">
        <UButton
          icon="i-heroicons-plus"
          color="primary"
          size="md"
          @click="tabStore.createTab(null)"
        >
          Create New Tab
        </UButton>
        <UButton icon="i-heroicons-command-line" variant="outline" color="neutral" size="md">
          <span class="hidden sm:inline">Press</span>
          <kbd
            class="ml-1 px-1.5 py-0.5 text-xs font-semibold rounded border border-gray-300 dark:border-gray-700"
          >
            ⌘ ⇧ N
          </kbd>
        </UButton>
      </div>
    </div>
  </div>

  <template v-else>
    <!-- Desktop: UTabs -->
    <UTabs
      v-if="!isMobile"
      v-model="tabStore.activeTabId"
      :items="items"
      variant="link"
      class="w-full flex-1 flex flex-col min-h-0 h-0"
      :ui="{
        content: 'flex flex-1 min-h-0',
        list: 'sticky top-0 z-10 bg-background shrink-0',
      }"
    >
      <template #list-leading>
        <UButton
          v-if="tabStore.tabs.length > 1"
          icon="mdi:folder-remove-outline"
          variant="ghost"
          color="error"
          aria-label="Close all tabs"
          title="Close all tabs"
          @click="openCloseAllModal"
        />
      </template>
      <template #list-trailing>
        <UButton
          icon="i-heroicons-plus"
          variant="ghost"
          color="neutral"
          aria-label="New tab"
          title="New tab (⌘⇧N)"
          class="hover:text-primary"
          :ui="{ body: 'sm:p-0 p-0' }"
          @click="tabStore.createTab(null)"
        />
      </template>

      <template #label="{ item }">
        <span class="max-w-[160px] truncate block">{{ item.label }}</span>
      </template>

      <template #trailing="{ item }">
        <UButton
          v-if="tabStore.tabs.length > 1"
          icon="i-heroicons-x-mark"
          variant="ghost"
          color="neutral"
          title="Close tab"
          aria-label="Close tab"
          class="hover:text-primary"
          @click.stop="tabStore.closeTab(item.value)"
        />
      </template>

      <template #content="{ item }">
        <FileExplorer v-if="item.value" :tab-id="item.value" :key="item.value" />
      </template>
    </UTabs>

    <!-- Mobile: active tab only + bottom bar -->
    <template v-else>
      <!-- Active tab content; pb-14 reserves space for the bottom bar -->
      <div class="flex flex-col h-full pb-14">
        <FileExplorer
          v-if="tabStore.activeTabId"
          :tab-id="tabStore.activeTabId"
          :key="tabStore.activeTabId"
        />
      </div>

      <!-- Bottom tab bar -->
      <div
        @contextmenu="openManageSheet"
        class="fixed bottom-0 inset-x-0 z-40 h-14 flex items-center gap-1 px-2 border-t border-gray-200/70 dark:border-gray-700/70 bg-background/80 backdrop-blur-sm"
      >
        <!-- Visible tab pills (max 3) -->
        <button
          v-for="tab in visibleTabs"
          :key="tab.id"
          class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm max-w-[100px] min-w-0 transition-colors select-none"
          :class="
            tab.id === tabStore.activeTabId
              ? 'bg-primary/10 text-primary font-medium'
              : 'text-gray-500 dark:text-gray-400 hover:bg-gray-100/60 dark:hover:bg-gray-800/60'
          "
          @click="tabStore.activeTabId = tab.id"
          @pointerdown="onTabPointerDown(tab.id)"
          @pointerup="onTabPointerUp"
          @pointerleave="onTabPointerUp"
        >
          <Icon icon="mdi:folder" class="w-4 h-4 shrink-0" />
          <span class="truncate">{{ tab.title }}</span>
        </button>

        <!-- Overflow pill -->
        <button
          v-if="overflowCount > 0"
          class="px-3 py-1.5 rounded-lg text-sm text-gray-500 dark:text-gray-400 hover:bg-gray-100/60 dark:hover:bg-gray-800/60 transition-colors shrink-0"
          @click="openManageSheet"
        >
          ···&nbsp;{{ overflowCount }}
        </button>

        <div class="flex-1" />

        <!-- New tab -->
        <UButton
          variant="ghost"
          color="neutral"
          size="sm"
          aria-label="New tab"
          title="New tab"
          @click="tabStore.createTab(null)"
        >
          <Icon icon="mdi:plus" class="w-4 h-4" />
        </UButton>
      </div>

      <!-- Manage tabs bottom sheet -->
      <Transition name="sheet">
        <div
          v-if="isManageSheetOpen"
          class="fixed inset-0 z-50 flex flex-col justify-end"
          @click.self="closeManageSheet"
        >
          <!-- Scrim -->
          <div class="absolute inset-0 bg-black/40 backdrop-blur-[2px]" @click="closeManageSheet" />

          <!-- Sheet panel -->
          <div
            class="relative z-10 rounded-t-2xl border-t border-gray-200/70 dark:border-gray-700/70 bg-background/95 backdrop-blur-sm pb-safe"
          >
            <!-- Handle -->
            <div class="flex justify-center pt-3 pb-1">
              <div class="w-10 h-1 rounded-full bg-gray-300 dark:bg-gray-600" />
            </div>

            <!-- Header -->
            <div class="flex items-center justify-between px-4 py-3">
              <h2 class="text-sm font-semibold text-gray-700 dark:text-gray-200">All tabs</h2>
              <UButton
                v-if="tabStore.tabs.length > 0"
                variant="ghost"
                color="error"
                size="sm"
                aria-label="Close all tabs"
                @click="openCloseAllModal"
              >
                <Icon icon="mdi:folder-remove-outline" class="w-4 h-4" />
              </UButton>
            </div>

            <!-- Tab list -->
            <ul class="px-2 pb-4 space-y-1 max-h-[60vh] overflow-y-auto">
              <li
                v-for="tab in tabStore.tabs"
                :key="tab.id"
                class="flex items-center gap-3 px-3 py-3 rounded-xl min-h-[52px] transition-colors cursor-pointer"
                :class="
                  tab.id === tabStore.activeTabId
                    ? 'bg-primary/8 text-primary'
                    : 'hover:bg-gray-100/60 dark:hover:bg-gray-800/60 text-gray-700 dark:text-gray-200'
                "
                @click="activateTab(tab.id)"
              >
                <Icon
                  :icon="tab.id === tabStore.activeTabId ? 'mdi:folder-open' : 'mdi:folder'"
                  class="w-5 h-5 shrink-0"
                  :class="tab.id === tabStore.activeTabId ? 'text-primary' : 'text-gray-400'"
                />
                <span class="flex-1 text-sm font-medium truncate">{{ tab.title }}</span>

                <!-- Close button — only show when more than 1 tab -->
                <UButton
                  v-if="tabStore.tabs.length > 1"
                  variant="ghost"
                  color="error"
                  size="sm"
                  aria-label="Close tab"
                  class="shrink-0"
                  @click.stop="closeTabFromSheet(tab.id)"
                >
                  <Icon icon="mdi:close" class="w-4 h-4" />
                </UButton>
              </li>
            </ul>

            <!-- New tab CTA -->
            <div class="px-4 pb-6 pt-1 border-t border-gray-100/70 dark:border-gray-800/70">
              <UButton
                variant="outline"
                color="neutral"
                size="sm"
                class="w-full"
                @click="
                  () => {
                    tabStore.createTab(null);
                    closeManageSheet();
                  }
                "
              >
                <Icon icon="mdi:plus" class="w-4 h-4 mr-1.5" />
                New tab
              </UButton>
            </div>
          </div>
        </div>
      </Transition>
    </template>
  </template>

  <!-- Close all tabs confirmation modal (shared between desktop and mobile) -->
  <UModal v-model:open="isCloseAllModalOpen">
    <template #content>
      <div class="p-6 space-y-4">
        <div class="space-y-1">
          <h3 class="text-base font-semibold text-gray-900 dark:text-white">Close all tabs?</h3>
          <p class="text-sm text-gray-500 dark:text-gray-400">
            This will close all {{ tabStore.tabs.length }} open
            {{ tabStore.tabs.length === 1 ? "tab" : "tabs" }}. This action cannot be undone.
          </p>
        </div>
        <div class="flex gap-3 justify-end">
          <UButton variant="outline" color="neutral" size="sm" @click="isCloseAllModalOpen = false">
            Cancel
          </UButton>
          <UButton color="error" size="sm" @click="confirmCloseAll"> Close all </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>

<style scoped>
/* Bottom sheet slide-up transition */
.sheet-enter-active {
  transition: opacity 0.2s ease;
}
.sheet-leave-active {
  transition: opacity 0.15s ease;
}
.sheet-enter-from,
.sheet-leave-to {
  opacity: 0;
}

.sheet-enter-active .relative,
.sheet-leave-active .relative {
  transition: transform 0.25s cubic-bezier(0.32, 0.72, 0, 1);
}
.sheet-enter-from .relative,
.sheet-leave-to .relative {
  transform: translateY(100%);
}

/* iOS safe area support */
.pb-safe {
  padding-bottom: env(safe-area-inset-bottom, 1rem);
}
</style>
