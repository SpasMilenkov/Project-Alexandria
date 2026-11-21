<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref, watch } from "vue";

import type { TagDto } from "@/api/tag";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { useFileTagActions } from "@/composables/useFileTagActions";
import { getTagsForFile } from "@/queries/tags";

import TagBadge from "./TagBadge.vue";

// One file's tags as removable badges; removal and its undo toast come from useFileTagActions.
const {
  fileId,
  fallbackTags = [],
  enabled = true,
  scrollOnMobile = false,
} = defineProps<{
  fileId: string;
  // Tags the caller already has (from a file listing), shown until the file's own list loads.
  fallbackTags?: TagDto[];
  enabled?: boolean;
  // One scrolling row below md instead of wrapping, for tight spaces like a bottom sheet.
  scrollOnMobile?: boolean;
}>();

const { removeTag } = useFileTagActions({ fileId: () => fileId });

const { data: fileTags, isPending } = useQuery(() => ({
  ...getTagsForFile(fileId),
  enabled: enabled && fileId.length > 0,
}));

// Removed tags disappear at once and stay hidden until the refreshed list no longer has them,
// so a badge never flashes back while the refetch is in flight.
const hiddenIds = ref(new Set<string>());

const sourceTags = computed(() => fileTags.value?.tags ?? fallbackTags);
const tags = computed(() => sourceTags.value.filter((tag) => !hiddenIds.value.has(tag.id)));

const showSpinner = computed(
  () => isPending.value && !fileTags.value && fallbackTags.length === 0,
);

watch(sourceTags, (current) => {
  if (hiddenIds.value.size === 0) return;

  const stillThere = new Set(current.map((tag) => tag.id));

  hiddenIds.value = new Set([...hiddenIds.value].filter((id) => stillThere.has(id)));
});

watch(
  () => fileId,
  () => {
    hiddenIds.value = new Set();
  },
);

const LIST_LAYOUT = {
  scroll:
    "flex flex-nowrap items-center gap-1.5 overflow-x-auto pb-1 md:flex-wrap md:overflow-visible md:pb-0",
  wrap: "flex flex-wrap items-center gap-1.5",
} as const;

const listLayout = computed(() => {
  if (scrollOnMobile) return LIST_LAYOUT.scroll;

  return LIST_LAYOUT.wrap;
});

const setHidden = (tagId: string, hidden: boolean) => {
  const next = new Set(hiddenIds.value);

  if (hidden) next.add(tagId);
  else next.delete(tagId);

  hiddenIds.value = next;
};

const handleRemove = async (tagId: string) => {
  const name = sourceTags.value.find((tag) => tag.id === tagId)?.name;

  setHidden(tagId, true);

  const removed = await removeTag(tagId, name);

  if (!removed) setHidden(tagId, false);
};
</script>

<template>
  <div v-if="showSpinner" class="flex h-8 items-center text-gray-500">
    <BlockSpinner :size="16" aria-label="Loading tags" />
  </div>

  <div v-else-if="tags.length > 0" :class="listLayout">
    <TagBadge
      v-for="tag in tags"
      :key="tag.id"
      :tag="tag"
      :file-id="fileId"
      class="shrink-0"
      @remove-tag="handleRemove"
    />
  </div>

  <p v-else class="text-sm text-gray-600 dark:text-gray-400">No tags yet.</p>
</template>
