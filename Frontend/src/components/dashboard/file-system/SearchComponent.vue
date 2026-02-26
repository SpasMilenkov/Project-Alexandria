<template>
  <div class="relative flex items-center gap-2">
    <USelectMenu
      ref="selectMenu"
      class="min-w-50"
      v-model="selectedItem"
      v-model:open="open"
      v-model:search-term="searchTerm"
      :items="items"
      :loading="directoryStore.isSearching"
      placeholder="Search..."
      icon="i-lucide-search"
      size="md"
      variant="outline"
      value-key="id"
      label-key="name"
      :ignore-filter="true"
      searchable
      @update:search-term="search"
      @update:model-value="handleSelect(selectedItem)"
    >
      <template #default="{ modelValue }">
        <span v-if="modelValue">
          {{ searchResults.find((i) => i.id === modelValue)?.name }}
        </span>
        <span v-else class="text-muted"> Search </span>
      </template>
      <template #trailing>
        <UKbd value="/" />
      </template>
    </USelectMenu>
    <UButton icon="mdi:arrow-expand" class="h-8 w-8" @click="advancedSearch" />
  </div>
</template>

<script setup lang="ts">
import { type Ref, ref } from "vue";
import type { DirectorySummaryDto, SearchDirectoryRequest } from "@/api/directory";
import { useDirectoryStore } from "@/stores/directory";
import { useDebounceFn } from "@vueuse/core";
import AdvancedSearchModal from "./Modals/AdvancedSearchModal.vue";
import { logger } from "@/utils/logger";

const overlay = useOverlay();
const selectMenu = ref(null);
const open = ref(false);
const searchResults: Ref<DirectorySummaryDto[]> = ref([]);
const advancedSearchModal = overlay.create(AdvancedSearchModal);
const advancedSearch = async () => {
  const instance = advancedSearchModal.open();
  await instance.result;
  return;
};

defineShortcuts({
  "/": () => (open.value = !open.value),
});

const emit = defineEmits<{
  navigate: [directoryId: string];
}>();
const directoryStore = useDirectoryStore();
const searchTerm = ref("");
const selectedItem: Ref<string | null> = ref(null);
const items: Ref<DirectorySummaryDto[]> = ref([]);

const search = useDebounceFn(async () => {
  if (!searchTerm.value.trim()) {
    items.value = [];
    return;
  }
  const query: SearchDirectoryRequest = {
    nameContains: searchTerm.value,
  };
  const response = await directoryStore.searchDirectory(query);
  if (response.success && response.data) {
    logger.log(response.data);

    items.value = response.data.items;
    searchResults.value = response.data.items;
  }
}, 350);

const handleSelect = (id: string | null) => {
  if (id) {
    emit("navigate", id);
    selectedItem.value = null;
  }
};
</script>

<style scoped></style>
