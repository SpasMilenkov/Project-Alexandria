<script setup lang="ts">
import { ref, watch } from "vue";

import { useFileTagActions } from "@/composables/useFileTagActions";
import { useTagSearch } from "@/composables/useTagSearch";

// The "Add tag" button and its search popover for one file. The file's tag list refreshes
// itself through the mutation, so any FileTagList for the same file updates too.
const { fileId, fileLabel = "this file" } = defineProps<{
  fileId: string;
  // How toasts name the file, e.g. its quoted name.
  fileLabel?: string;
}>();

const isOpen = ref(false);
const selectedTagId = ref<string | undefined>(undefined);

const { isSearching, resetSearch, searchQuery, tagOptions } = useTagSearch({
  enabled: isOpen,
  excludeFileId: () => fileId,
});

const { addTag } = useFileTagActions({ fileId: () => fileId, fileLabel: () => fileLabel });

const reset = () => {
  resetSearch();
  selectedTagId.value = undefined;
};

watch(selectedTagId, async (tagId) => {
  if (!tagId) return;

  const added = await addTag(tagId);

  if (added) isOpen.value = false;

  reset();
});

watch(isOpen, (open) => {
  if (!open) reset();
});
</script>

<template>
  <UPopover v-model:open="isOpen" :content="{ align: 'end', side: 'bottom' }">
    <UButton label="Add tag" icon="i-lucide-plus" size="xs" variant="outline" color="neutral" />

    <template #content>
      <div class="w-56 p-2">
        <USelectMenu
          v-model="selectedTagId"
          v-model:search-term="searchQuery"
          :loading="isSearching"
          :items="tagOptions"
          value-key="value"
          placeholder="Search tags…"
          autofocus
          class="w-full"
        />
      </div>
    </template>
  </UPopover>
</template>
