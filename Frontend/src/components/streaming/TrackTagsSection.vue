<template>
  <UCard class="frosted-glass glass-surface" :ui="{ body: 'p-4' }">
    <div class="flex items-center justify-between gap-2 mb-3">
      <span class="font-semibold text-sm text-gray-700 dark:text-gray-300">Tags</span>
      <UButton
        v-if="isAudioFile"
        icon="i-mdi-tag-outline"
        label="Auto-tag"
        size="xs"
        variant="outline"
        color="neutral"
        :loading="isAutoTagging"
        :disabled="isAutoTagging"
        @click="handleAutoTag"
      />
    </div>

    <div v-if="fileTagsLoading" class="flex justify-center py-6">
      <UIcon name="mdi:loading" class="w-6 h-6 animate-spin text-muted" />
    </div>

    <div v-else class="flex items-center gap-1.5 flex-wrap">
      <TagBadge
        v-for="tag in tags"
        :key="tag.id"
        :tag="tag"
        :file-id="file.fileId"
        class="shrink-0"
        @remove-tag="handleRemove"
      />

      <span
        v-if="!tags.length && !showTagSearch"
        class="text-xs text-gray-400 dark:text-gray-500 italic mr-1 shrink-0"
      >
        No tags
      </span>

      <UPopover v-model:open="showTagSearch" :content="{ side: 'bottom', align: 'start' }">
        <UButton
          label="Add tag"
          icon="i-mdi-plus"
          size="xs"
          variant="outline"
          color="neutral"
          class="rounded-full shrink-0"
        />

        <template #content>
          <div class="p-2 w-56">
            <USelectMenu
              v-model="selectedTagId"
              :loading="tagsLoading"
              :items="tagOptions"
              value-key="value"
              value-attribute="value"
              v-model:search-term="searchQuery"
              placeholder="Search tags…"
              autofocus
              class="w-full"
            />
          </div>
        </template>
      </UPopover>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed, ref, watch } from "vue";

import type { MediaFileDto } from "@/api/streaming";
import type { TagDto } from "@/api/tag";
import type { SearchTagsSchema } from "@/schemas/tag";

import TagBadge from "@/components/dashboard/tags/TagBadge.vue";
import { useAppToast } from "@/composables/useAppToast";
import { autoTagFile } from "@/mutations/files";
import { addTagToFile, removeTagFromFile } from "@/mutations/tags";
import { getTagsForFile, searchTag } from "@/queries/tags";
import { getIconByValue } from "@/utils/icon.utils";
import { isAutoTagSupportedFileType } from "@/utils/mimetype.utils";

const { file } = defineProps<{ file: MediaFileDto }>();

const appToast = useAppToast();
const { mutateAsync: addTagMutate } = addTagToFile();
const { mutateAsync: removeTagMutateAsync } = removeTagFromFile();
const { mutateAsync: autoTagMutate, isLoading: isAutoTagging } = autoTagFile();

const isAudioFile = computed(() => isAutoTagSupportedFileType(file.mimeType));

const extractAutoTagError = (err: any): string => {
  const data = err?.response?.data;
  if (data?.errors && typeof data.errors === "object") {
    const parts = Object.values(data.errors as Record<string, string[]>)
      .flat()
      .filter(Boolean);
    if (parts.length > 0) return parts.join(" · ");
  }
  return data?.message ?? data?.error ?? err?.message ?? "Unknown error";
};

const handleAutoTag = async () => {
  if (isAutoTagging.value) return;
  try {
    const result = await autoTagMutate(file.fileId);
    if (result.queued) {
      appToast.success("Auto-tag queued", "Tags will be derived in the background.");
    } else {
      appToast.info("Already tagged", result.message);
    }
  } catch (error) {
    const message = extractAutoTagError(error);
    if (message.toLowerCase().includes("disabled")) {
      appToast.info("Auto-tagging disabled", message);
    } else {
      appToast.error("Auto-tag failed", message);
    }
  }
};

const searchQuery = ref("");
const selectedTagId = ref<string | undefined>(undefined);
const showTagSearch = ref(false);

const searchFilters = computed<SearchTagsSchema>(() => ({
  excludeOnFile: file.fileId,
  nameContains: searchQuery.value || undefined,
  ownerScope: "all",
  page: 1,
  pageSize: 25,
}));

const { data: tagsData, isLoading: tagsLoading } = useQuery(() => ({
  ...searchTag(searchFilters.value),
}));

const tagOptions = computed(() =>
  (tagsData.value?.items ?? []).map((t) => ({
    label: t.name,
    value: t.id,
    icon: getIconByValue(t.icon),
  })),
);

const {
  data: fileTags,
  isLoading: fileTagsLoading,
  refresh: refreshFileTags,
} = useQuery(() => ({
  ...getTagsForFile(file.fileId),
}));

const tags = computed((): TagDto[] => fileTags.value?.tags ?? []);

const handleTagAdd = async (tagId: string) => {
  if (!tagId) return;
  try {
    await addTagMutate({ fileId: file.fileId, data: { tagIds: [tagId] } });
    selectedTagId.value = undefined;
    searchQuery.value = "";
    showTagSearch.value = false;
  } catch {
    selectedTagId.value = undefined;
  }
};

watch(selectedTagId, (tagId) => {
  if (tagId) void handleTagAdd(tagId);
});

const handleRemove = async (tagId: string) => {
  await removeTagMutateAsync({ fileId: file.fileId, tagId });
  refreshFileTags();
};

watch(showTagSearch, (open) => {
  if (!open) {
    searchQuery.value = "";
    selectedTagId.value = undefined;
  }
});
</script>
