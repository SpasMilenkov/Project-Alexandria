<script setup lang="ts">
import { computed, onMounted } from "vue";
import { useTabStore } from "@/stores/tab";
import FileExplorer from "@/components/dashboard/file-system/FileExplorerTab.vue";

const tabStore = useTabStore();

const items = computed(() =>
  tabStore.tabs.map((tab) => ({
    value: tab.id,
    label: tab.title,
    icon: "i-heroicons-folder",
  }))
);

onMounted(() => {
  console.log(tabStore.tabs.values);
  if (tabStore.tabs.length === 0) {
    tabStore.createTab(null);
  }
});
</script>

<template>
  <UTabs
    v-model="tabStore.activeTabId"
    :items="items"
    variant="link"
    class="w-full"
  >
    <template #list-trailing>
      <UButton
        icon="i-heroicons-plus"
        @click="tabStore.createTab(null)"
        variant="ghost"
        color="neutral"
        class="hover:text-primary"
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
