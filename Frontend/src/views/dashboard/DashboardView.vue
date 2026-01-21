<script setup lang="ts">
import { computed, onMounted } from "vue";
import { useTabStore } from "@/stores/tab";
import FileExplorer from "@/components/dashboard/file-system/FileExplorerTab.vue";

defineShortcuts({
  meta_shift_q: () => {
    console.log("triggering tab close");
    if (tabStore.activeTabId) tabStore.closeTab(tabStore.activeTabId);
  },
  meta_shift_n: () => tabStore.createTab(null),
});

const tabStore = useTabStore();

const items = computed(() =>
  tabStore.tabs.map((tab) => ({
    value: tab.id,
    label: tab.title,
    icon: "i-heroicons-folder",
  })),
);

const hasNoTabs = computed(() => tabStore.tabs.length === 0);

onMounted(() => {
  console.log(tabStore.tabs.values);
  if (tabStore.tabs.length === 0) {
    tabStore.createTab(null);
  }
});
</script>

<template>
  <div
    v-if="hasNoTabs"
    class="flex items-center justify-center min-h-[400px] w-full"
  >
    <div class="text-center space-y-4 max-w-md px-4">
      <div
        class="inline-flex items-center justify-center w-16 h-16 rounded-full bg-primary/10 mb-2"
      >
        <UIcon name="i-heroicons-folder-open" class="w-8 h-8 text-primary" />
      </div>

      <div class="space-y-2">
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
          No tabs open
        </h3>
        <p class="text-sm text-gray-500 dark:text-gray-400">
          Create a new tab to start exploring your files and folders.
        </p>
      </div>

      <div class="flex flex-col sm:flex-row gap-3 justify-center pt-2">
        <UButton
          icon="i-heroicons-plus"
          @click="tabStore.createTab(null)"
          color="primary"
          size="md"
        >
          Create New Tab
        </UButton>

        <UButton
          icon="i-heroicons-command-line"
          variant="outline"
          color="neutral"
          size="md"
        >
          <span class="hidden sm:inline">Press</span>
          <kbd
            class="ml-1 px-1.5 py-0.5 text-xs font-semibold bg-gray-100 dark:bg-gray-800 rounded border border-gray-300 dark:border-gray-700"
          >
            ⌘ ⇧ N
          </kbd>
        </UButton>
      </div>
    </div>
  </div>

  <UTabs
    v-else
    v-model="tabStore.activeTabId"
    :items="items"
    variant="link"
    class="w-full p-0"
  >
    <template #list-trailing>
      <UButton
        icon="i-heroicons-plus"
        @click="tabStore.createTab(null)"
        variant="ghost"
        color="neutral"
        class="hover:text-primary"
        :ui="{ body: 'sm:p-0 p-0' }"
      />
    </template>

    <template #content="{ item }">
      <FileExplorer v-if="item.value" :tab-id="item.value" />
    </template>

    <template #trailing="{ item }">
      <UButton
        v-if="tabStore.tabs.length > 1"
        icon="i-heroicons-x-mark"
        @click="tabStore.closeTab(item.value)"
        variant="ghost"
        color="muted"
        class="hover:text-primary"
      />
    </template>
  </UTabs>
</template>
