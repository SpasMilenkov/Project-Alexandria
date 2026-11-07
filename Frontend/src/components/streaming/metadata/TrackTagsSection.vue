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
          v-if="isAudioFile"
          icon="i-mdi-tag-outline"
          label="Auto-tag"
          size="lg"
          variant="outline"
          color="neutral"
          class="min-h-11 justify-center md:order-3 md:min-h-9 md:w-full"
          :loading="isAutoTagging"
          :disabled="isAutoTagging"
          @click="handleAutoTag"
        />

        <USelectMenu
          v-model="selectedTagId"
          v-model:search-term="searchQuery"
          :loading="tagsLoading"
          :items="tagOptions"
          value-key="value"
          icon="i-mdi-plus"
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
          <UIcon name="i-mdi-tag-outline" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
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
                name="i-mdi-chevron-down"
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
import type { SearchTagsSchema } from "@/schemas/tag";

import { FACET_LABELS, type TagDto } from "@/api/tag";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import TagBadge from "@/components/dashboard/tags/TagBadge.vue";
import { useAppToast } from "@/composables/useAppToast";
import { quoted } from "@/features/toast/format";
import { autoTagFile } from "@/mutations/files";
import { addTagToFile, removeTagFromFile } from "@/mutations/tags";
import { getTagsForFile, searchTag } from "@/queries/tags";
import { getIconByValue } from "@/utils/files/icons";
import { isAutoTagSupportedFileType } from "@/utils/files/mimetype";

const { file } = defineProps<{ file: MediaFileDto }>();

const appToast = useAppToast("tags");

const trackLabel = computed(() => quoted(file.title || file.fileName));
const { mutateAsync: addTagMutate } = addTagToFile();
const { mutateAsync: removeTagMutateAsync } = removeTagFromFile();
const { mutateAsync: autoTagMutate, isLoading: isAutoTagging } = autoTagFile();

const isAudioFile = computed(() => isAutoTagSupportedFileType(file.mimeType));
const isDesktop = useMediaQuery("(min-width: 768px)");

const extractAutoTagError = (err: any): string => {
  const data = err?.data;
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
      appToast.success(
        `Auto-tagging ${trackLabel.value}`,
        "Tags appear here once the background analysis finishes.",
      );
    } else {
      appToast.info(`${trackLabel.value} is already auto-tagged`, result.message);
    }
  } catch (error) {
    const message = extractAutoTagError(error);
    if (message.toLowerCase().includes("disabled")) {
      appToast.info("Auto-tagging is turned off", message);
    } else {
      appToast.error(`Couldn't auto-tag ${trackLabel.value}`, message, { retry: handleAutoTag });
    }
  }
};

const searchQuery = ref("");
const selectedTagId = ref<string | undefined>(undefined);

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
  isPending: fileTagsLoading,
  refresh: refreshFileTags,
} = useQuery(() => ({
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
  const tagLabel = count === 1 ? "tag" : "tags";
  if (groups.value.length < 2) return `${count} ${tagLabel}`;
  return `${count} ${tagLabel} in ${groups.value.length} groups`;
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

const handleTagAdd = async (tagId: string) => {
  if (!tagId) return;
  try {
    await addTagMutate({ fileId: file.fileId, data: { tagIds: [tagId] } });
    selectedTagId.value = undefined;
    searchQuery.value = "";
  } catch (error) {
    selectedTagId.value = undefined;
    appToast.error(`Couldn't add the tag to ${trackLabel.value}`, error);
  }
};

watch(selectedTagId, (tagId) => {
  if (tagId) void handleTagAdd(tagId);
});

const handleMenuOpen = (open: boolean) => {
  if (!open) {
    searchQuery.value = "";
    selectedTagId.value = undefined;
  }
};

const handleRemove = async (tagId: string) => {
  const removedName = tags.value.find((tag) => tag.id === tagId)?.name;
  const tagLabel = removedName ? quoted(removedName) : "the tag";

  try {
    await removeTagMutateAsync({ fileId: file.fileId, tagId });
  } catch (error) {
    appToast.error(`Couldn't remove ${tagLabel}`, error);
    return;
  }

  refreshFileTags();
  appToast.success(`Removed ${tagLabel}`, {
    actions: [
      {
        icon: "i-lucide-undo-2",
        label: "Undo",
        onClick: () => {
          void handleTagAdd(tagId);
        },
      },
    ],
  });
};
</script>
