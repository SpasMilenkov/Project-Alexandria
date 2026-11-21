<template>
  <UCard class="frosted-glass glass-surface" :ui="{ body: 'p-0 sm:p-0' }">
    <div class="flex flex-col md:flex-row">
      <aside
        class="grid grid-cols-[1fr_auto] items-center gap-4 p-4 md:w-62 md:shrink-0 md:grid-cols-1 md:items-start md:border-r md:p-6 border-gray-200/70 dark:border-gray-700/70"
      >
        <div>
          <div class="text-sm font-semibold text-gray-900 dark:text-gray-100">Tags</div>
          <div class="mt-1 text-xs text-gray-600 dark:text-gray-400">{{ summary }}</div>
        </div>

        <UButton
          v-if="isAudioFile && isAutoTaggingEnabled"
          icon="i-lucide-tag"
          label="Auto-tag"
          size="lg"
          variant="outline"
          color="neutral"
          class="min-h-11 justify-center md:order-3 md:min-h-9 md:w-full"
          :loading="isAutoTagging"
          :disabled="isAutoTagging"
          @click="autoTag"
        />

        <USelectMenu
          v-model="selectedTagId"
          v-model:search-term="searchQuery"
          :loading="isSearching"
          :items="tagOptions"
          value-key="value"
          icon="i-lucide-plus"
          placeholder="Add tag"
          size="lg"
          class="col-span-2 min-h-11 w-full md:order-2 md:col-span-1 md:min-h-9"
          @update:open="handleMenuOpen"
        />
      </aside>

      <div class="min-w-0 flex-1 px-4 pb-4 md:p-6">
        <div v-if="fileTagsLoading" class="flex justify-center py-6">
          <BlockSpinner :size="24" class="text-gray-600 dark:text-gray-400" />
        </div>

        <div v-else-if="!tags.length" class="flex flex-col items-center gap-2 py-6 text-center">
          <UIcon name="i-lucide-tag" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
          <div class="text-sm font-semibold text-gray-900 dark:text-gray-100">No tags yet</div>
          <div class="text-xs text-gray-600 dark:text-gray-400">
            Add a tag to organize this file.
          </div>
        </div>

        <div
          v-else
          class="grid items-start gap-4 md:grid-cols-[repeat(auto-fit,minmax(200px,1fr))]"
        >
          <UCollapsible
            v-for="group in groups"
            :key="group.name"
            :open="isDesktop || isGroupOpen(group.name)"
            :disabled="isDesktop"
            class="rounded-xl border border-gray-200/70 bg-neutral-100/50 px-4 dark:border-neutral-700/70 dark:bg-neutral-900/40"
            @update:open="(open: boolean) => setGroupOpen(group.name, open)"
          >
            <button
              type="button"
              class="flex h-11 w-full items-center gap-2 text-left disabled:cursor-default md:h-auto md:pt-4 md:pb-3"
            >
              <span class="text-xs font-semibold text-gray-600 dark:text-gray-400">
                {{ group.name }}
              </span>
              <span
                v-if="!isGroupOpen(group.name)"
                class="flex items-center gap-1 md:hidden"
                aria-hidden="true"
              >
                <span
                  v-for="tag in group.tags"
                  :key="tag.id"
                  class="h-2 w-2 rounded-full"
                  :style="{ backgroundColor: tag.color }"
                />
              </span>
              <span class="ml-auto text-xs text-gray-600 dark:text-gray-400">
                {{ group.tags.length }}
              </span>
              <UIcon
                name="i-lucide-chevron-down"
                class="w-4 h-4 text-gray-600 transition-transform duration-200 md:hidden dark:text-gray-400"
                :class="{ 'rotate-180': isGroupOpen(group.name) }"
              />
            </button>

            <template #content>
              <div class="flex flex-wrap gap-2 pb-4">
                <TagBadge
                  v-for="tag in group.tags"
                  :key="tag.id"
                  :tag="tag"
                  :file-id="file.fileId"
                  class="shrink-0"
                  @remove-tag="handleRemove"
                />
              </div>
            </template>
          </UCollapsible>
        </div>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { useMediaQuery } from "@vueuse/core";
import { computed, ref, watch } from "vue";

import type { MediaFileDto } from "@/api/streaming";

import { FACET_LABELS, type TagDto } from "@/api/tag";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import TagBadge from "@/components/dashboard/tags/TagBadge.vue";
import { useAutoTag } from "@/composables/useAutoTag";
import { useFileTagActions } from "@/composables/useFileTagActions";
import { useTagSearch } from "@/composables/useTagSearch";
import { quoted } from "@/features/toast/format";
import { getTagsForFile } from "@/queries/tags";
import { formatCount } from "@/utils/count";
import { isAutoTagSupportedFileType } from "@/utils/files/mimetype";

const { file } = defineProps<{ file: MediaFileDto }>();

const trackLabel = computed(() => quoted(file.title || file.fileName));

const isAudioFile = computed(() => isAutoTagSupportedFileType(file.mimeType));
const isDesktop = useMediaQuery("(min-width: 768px)");

const { autoTag, isAutoTagging, isAutoTaggingEnabled } = useAutoTag({
  fileId: () => file.fileId,
  fileLabel: trackLabel,
});

const isMenuOpen = ref(false);
const selectedTagId = ref<string | undefined>(undefined);

const { isSearching, resetSearch, searchQuery, tagOptions } = useTagSearch({
  enabled: isMenuOpen,
  excludeFileId: () => file.fileId,
});

const { addTag, removeTag } = useFileTagActions({
  fileId: () => file.fileId,
  fileLabel: trackLabel,
});

const { data: fileTags, isPending: fileTagsLoading } = useQuery(() => ({
  ...getTagsForFile(file.fileId),
}));

const tags = computed((): TagDto[] => fileTags.value?.tags ?? []);

const UNGROUPED_LABEL = "Other";

const getGroupName = (tag: TagDto): string => {
  if (tag.facet === null || tag.facet === undefined) return UNGROUPED_LABEL;

  return FACET_LABELS[tag.facet] ?? UNGROUPED_LABEL;
};

const groups = computed(() => {
  const byName = new Map<string, TagDto[]>();
  for (const tag of tags.value) {
    const name = getGroupName(tag);
    const members = byName.get(name) ?? [];
    members.push(tag);
    byName.set(name, members);
  }
  return [...byName.entries()]
    .map(([name, members]) => ({ name, tags: members }))
    .sort((a, b) => Number(a.name === UNGROUPED_LABEL) - Number(b.name === UNGROUPED_LABEL));
});

const summary = computed(() => {
  const count = tags.value.length;
  if (!count) return "No tags yet";
  const tagLabel = formatCount(count, "tag");
  if (groups.value.length < 2) return tagLabel;
  return `${tagLabel} in ${groups.value.length} groups`;
});

// Mobile only: the largest group starts open, the rest start collapsed.
const openGroups = ref<Record<string, boolean>>({});

const largestGroupName = computed(() => {
  const largest = groups.value.reduce<{ name: string; size: number }>(
    (best, group) =>
      group.tags.length > best.size ? { name: group.name, size: group.tags.length } : best,
    { name: "", size: 0 },
  );
  return largest.name;
});

const isGroupOpen = (name: string): boolean =>
  openGroups.value[name] ?? name === largestGroupName.value;

const setGroupOpen = (name: string, open: boolean) => {
  openGroups.value[name] = open;
};

const resetPicker = () => {
  resetSearch();
  selectedTagId.value = undefined;
};

watch(selectedTagId, async (tagId) => {
  if (!tagId) return;

  await addTag(tagId);
  resetPicker();
});

// The search runs only while the menu is open; closing it clears what was typed.
const handleMenuOpen = (open: boolean) => {
  isMenuOpen.value = open;

  if (!open) resetPicker();
};

const handleRemove = async (tagId: string) => {
  const name = tags.value.find((tag) => tag.id === tagId)?.name;

  await removeTag(tagId, name);
};
</script>
