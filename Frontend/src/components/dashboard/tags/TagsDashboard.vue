<template>
  <div class="flex h-full w-full flex-1 flex-col gap-3 p-3">
    <header
      class="flex shrink-0 flex-wrap items-center gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:flex-nowrap md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon name="i-lucide-tags" class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400" />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">Tags</h1>
        <UBadge v-if="tagsData" color="neutral" variant="subtle" size="sm">
          {{ tagsData.totalCount }}
        </UBadge>
      </div>

      <UInput
        v-model="searchQuery"
        placeholder="Search tags..."
        aria-label="Search tags"
        icon="i-lucide-search"
        class="order-last w-full md:order-none md:ml-4 md:max-w-md md:flex-1"
      >
        <template v-if="searchQuery" #trailing>
          <UButton
            variant="ghost"
            color="neutral"
            size="xs"
            icon="i-lucide-x"
            square
            class="justify-center"
            aria-label="Clear search"
            @click="searchQuery = ''"
          />
        </template>
      </UInput>

      <div class="ml-auto flex shrink-0 items-center gap-2">
        <Transition
          mode="out-in"
          enter-active-class="transition-all duration-200 ease-out"
          enter-from-class="opacity-0"
          enter-to-class="opacity-100"
          leave-active-class="transition-all duration-150 ease-in"
          leave-from-class="opacity-100"
          leave-to-class="opacity-0"
        >
          <div v-if="selectedTags.size > 0" key="selection" class="flex items-center gap-2">
            <span class="text-sm font-medium text-gray-900 dark:text-gray-100">
              {{ selectedTags.size }} selected
            </span>

            <UButton size="sm" variant="ghost" color="neutral" @click="clearSelection">
              Clear
            </UButton>

            <UButton
              size="sm"
              color="error"
              variant="outline"
              icon="i-lucide-trash-2"
              @click="handleBulkDelete"
            >
              Delete
            </UButton>
          </div>

          <div v-else key="actions" class="flex items-center gap-2">
            <div class="hidden items-center gap-1 md:flex">
              <UTooltip text="Grid view">
                <UButton
                  size="sm"
                  square
                  class="justify-center"
                  icon="i-lucide-layout-grid"
                  aria-label="Grid view"
                  :aria-pressed="viewMode === 'grid'"
                  :variant="viewMode === 'grid' ? 'soft' : 'ghost'"
                  :color="viewMode === 'grid' ? 'primary' : 'neutral'"
                  @click="viewMode = 'grid'"
                />
              </UTooltip>

              <UTooltip text="List view">
                <UButton
                  size="sm"
                  square
                  class="justify-center"
                  icon="i-lucide-list"
                  aria-label="List view"
                  :aria-pressed="viewMode === 'list'"
                  :variant="viewMode === 'list' ? 'soft' : 'ghost'"
                  :color="viewMode === 'list' ? 'primary' : 'neutral'"
                  @click="viewMode = 'list'"
                />
              </UTooltip>

              <UTooltip :text="systemToggleLabel">
                <UButton
                  size="sm"
                  square
                  class="justify-center"
                  icon="i-lucide-library-big"
                  :aria-label="systemToggleLabel"
                  :aria-pressed="settingsStore.isSystemTagsVisible"
                  :variant="settingsStore.isSystemTagsVisible ? 'soft' : 'ghost'"
                  :color="settingsStore.isSystemTagsVisible ? 'primary' : 'neutral'"
                  @click="toggleSystemTags"
                />
              </UTooltip>
            </div>

            <div class="hidden h-5 w-px bg-gray-200/70 md:block dark:bg-gray-700/70" />

            <UButton color="primary" size="sm" icon="i-lucide-tag" @click="handleCreateTag">
              <span class="hidden sm:inline">New tag</span>
            </UButton>

            <UDropdownMenu :items="overflowItems" class="md:hidden">
              <UButton
                size="sm"
                variant="ghost"
                color="neutral"
                icon="i-lucide-ellipsis"
                square
                class="justify-center md:hidden"
                aria-label="More options"
              />
            </UDropdownMenu>
          </div>
        </Transition>
      </div>
    </header>

    <div ref="containerRef" class="min-h-0 flex-1 overflow-y-auto" @click="handleContainerClick">
      <div class="flex min-h-full flex-col gap-3">
        <section
          aria-label="Your tags"
          class="overflow-hidden rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
          :class="{ 'flex flex-1 flex-col': isEmpty }"
        >
          <div v-if="isPending" class="flex justify-center py-16">
            <BlockSpinner :size="24" aria-label="Loading tags" class="text-gray-500" />
          </div>

          <EmptyStatePanel v-else-if="isEmpty" :panel="false">
            <template #ghost>
              <TagsGhost :view-mode="viewMode" />
            </template>

            <GlassEmptyState
              :icon="emptyState.icon"
              :title="emptyState.title"
              :description="emptyState.description"
              class="m-auto"
            >
              <UButton
                v-if="!searchQuery"
                color="primary"
                icon="i-lucide-tag"
                class="rounded-full px-5"
                @click="handleCreateTag"
              >
                New tag
              </UButton>
            </GlassEmptyState>
          </EmptyStatePanel>

          <template v-else>
            <div
              v-if="viewMode === 'grid'"
              class="grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-2 p-3"
            >
              <TagCard
                v-for="tag in tagsList"
                :key="tag.id"
                :tag="tag"
                :is-selected="selectedTags.has(tag.id)"
                @click="handleTagClick($event, tag.id)"
                @edit="handleEditTag"
                @delete="handleDeleteTag"
              />
            </div>

            <div v-else class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
              <TagListItem
                v-for="tag in tagsList"
                :key="tag.id"
                :tag="tag"
                :is-selected="selectedTags.has(tag.id)"
                @click="handleTagClick($event, tag.id)"
                @edit="handleEditTag"
                @delete="handleDeleteTag"
              />
            </div>

            <div
              v-if="tagsData && tagsData.totalPages > 1"
              class="flex justify-center border-t border-gray-200/70 px-4 py-2 dark:border-gray-700/70"
            >
              <UPagination
                v-model:page="currentPage"
                :total="tagsData.totalCount"
                :items-per-page="pageSize"
              />
            </div>
          </template>
        </section>

        <!-- Search results already include system tags, so the vocabulary hides while searching. -->
        <div
          v-if="settingsStore.isSystemTagsVisible && !searchQuery"
          class="overflow-hidden rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
        >
          <SystemTagSection />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { DropdownMenuItem } from "@nuxt/ui";

import { useQuery } from "@pinia/colada";
import { computed, ref, watch } from "vue";

import type { TagDto } from "@/api/tag";
import type { SearchTagsSchema } from "@/schemas/tag";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import EmptyStatePanel from "@/components/common/EmptyStatePanel.vue";
import GlassEmptyState from "@/components/common/GlassEmptyState.vue";
import CreateTagModal from "@/components/dashboard/tags/modals/CreateTagModal.vue";
import UpdateTagModal from "@/components/dashboard/tags/modals/UpdateTagModal.vue";
import { useAppToast } from "@/composables/useAppToast";
import { quoted } from "@/features/toast/format";
import { deleteTag } from "@/mutations/tags";
import { searchTag } from "@/queries/tags";
import { useSettingsStore } from "@/stores/settings";
import { formatCount } from "@/utils/count";

import SystemTagSection from "./SystemTagSection.vue";
import TagCard from "./TagCard.vue";
import TagListItem from "./TagListItem.vue";
import TagsGhost from "./TagsGhost.vue";

const settingsStore = useSettingsStore();
const toast = useAppToast("tags");
const overlay = useOverlay();

const createTagModal = overlay.create(CreateTagModal);
const updateTagModal = overlay.create(UpdateTagModal);

const viewMode = ref<"grid" | "list">("grid");
const searchQuery = ref("");
const selectedTags = ref<Set<string>>(new Set());
const lastSelected = ref<string | null>(null);
const containerRef = ref<HTMLElement | null>(null);

const currentPage = ref(1);
const pageSize = ref(25);

// Browsing lists personal tags only; searching spans personal and system tags.
const searchFilters = computed<SearchTagsSchema>(() => ({
  nameContains: searchQuery.value || undefined,
  ownerScope: searchQuery.value ? "all" : "user",
  page: currentPage.value,
  pageSize: pageSize.value,
}));

// Keeping the previous page on screen avoids a spinner flash per keystroke and page change.
const {
  data: tagsData,
  isPending,
  refetch,
} = useQuery(() => ({
  ...searchTag(searchFilters.value),
  placeholderData: (prev) => prev,
}));

const tagNameById = computed(
  () => new Map((tagsData.value?.items ?? []).map((tag) => [tag.id, tag.name])),
);

const tagsList = computed(() => tagsData.value?.items || []);

const { mutateAsync: deleteTagMutate } = deleteTag();

const systemToggleLabel = computed(() => {
  if (settingsStore.isSystemTagsVisible) return "Hide system vocabulary";

  return "Show system vocabulary";
});

const emptyState = computed(() => {
  if (searchQuery.value) {
    return {
      description: "Try a different search term.",
      icon: "i-lucide-search",
      title: "No matching tags",
    };
  }

  return {
    description:
      "Tags group files across folders, so a project or topic stays together wherever its files live.",
    icon: "i-lucide-tags",
    title: "No tags yet",
  };
});

const isEmpty = computed(() => !isPending.value && tagsList.value.length === 0);

const toggleSystemTags = () => {
  settingsStore.setSystemTagsVisible(!settingsStore.isSystemTagsVisible);
};

const overflowItems = computed<DropdownMenuItem[][]>(() => [
  [
    {
      checked: viewMode.value === "grid",
      icon: "i-lucide-layout-grid",
      label: "Grid view",
      onUpdateChecked: () => {
        viewMode.value = "grid";
      },
      type: "checkbox",
    },
    {
      checked: viewMode.value === "list",
      icon: "i-lucide-list",
      label: "List view",
      onUpdateChecked: () => {
        viewMode.value = "list";
      },
      type: "checkbox",
    },
  ],
  [
    {
      checked: settingsStore.isSystemTagsVisible,
      icon: "i-lucide-library-big",
      label: "System vocabulary",
      onUpdateChecked: toggleSystemTags,
      type: "checkbox",
    },
  ],
]);

const handleCreateTag = async () => {
  const instance = createTagModal.open();
  const shouldRefresh = await instance.result;

  if (!shouldRefresh) return;

  toast.success("Tag created", "It's ready to apply to files and tracks.");

  refetch();
};

const handleEditTag = async (tag: TagDto) => {
  const instance = updateTagModal.open({ tag });
  const shouldRefresh = await instance.result;

  if (!shouldRefresh) return;

  toast.success(`Saved changes to ${quoted(tag.name)}`);

  refetch();
};

const tagLabel = (tagId: string) => {
  const name = tagNameById.value.get(tagId);

  return name ? quoted(name) : "the tag";
};

const handleDeleteTag = async (tagId: string) => {
  const label = tagLabel(tagId);

  try {
    await deleteTagMutate(tagId);
    toast.success(`Deleted ${label}`, "It was removed from every file that had it.");
    selectedTags.value.delete(tagId);
    refetch();
  } catch (err) {
    toast.error(`Couldn't delete ${label}`, err, { retry: () => handleDeleteTag(tagId) });
  }
};

const handleBulkDelete = async () => {
  if (selectedTags.value.size === 0) return;

  const ids = Array.from(selectedTags.value);
  const results = await Promise.allSettled(ids.map((id) => deleteTagMutate(id)));
  const failedIds = ids.filter((_, index) => results[index]?.status === "rejected");
  const firstFailure = results.find((result) => result.status === "rejected");
  const deletedCount = ids.length - failedIds.length;

  // Only the failures stay selected, so the bulk action can simply be run again.
  if (failedIds.length === 0) {
    clearSelection();
  } else {
    selectedTags.value = new Set(failedIds);
  }
  refetch();

  if (deletedCount > 0) toast.success(`Deleted ${formatCount(deletedCount, "tag")}`);

  if (firstFailure) {
    toast.error(`Couldn't delete ${formatCount(failedIds.length, "tag")}`, firstFailure.reason);
  }
};

const toggleSelect = (tagId: string) => {
  if (selectedTags.value.has(tagId)) {
    selectedTags.value.delete(tagId);
  } else {
    selectedTags.value.add(tagId);
  }
};

const clearSelection = () => {
  selectedTags.value.clear();
  lastSelected.value = null;
};

const selectRange = (startId: string, endId: string) => {
  const startIndex = tagsList.value.findIndex((t) => t.id === startId);
  const endIndex = tagsList.value.findIndex((t) => t.id === endId);

  if (startIndex === -1 || endIndex === -1) return;

  const from = Math.min(startIndex, endIndex);
  const to = Math.max(startIndex, endIndex);

  for (let i = from; i <= to; i++) {
    if (!tagsList.value[i].isSystem) selectedTags.value.add(tagsList.value[i].id);
  }
};

const handleTagClick = (event: MouseEvent | KeyboardEvent, tagId: string) => {
  const tag = tagsList.value.find((t) => t.id === tagId);

  if (tag?.isSystem) return;

  if (event.shiftKey && lastSelected.value) {
    selectRange(lastSelected.value, tagId);
    return;
  }

  if (!event.ctrlKey && !event.metaKey) clearSelection();

  toggleSelect(tagId);
  lastSelected.value = tagId;
};

const handleContainerClick = (event: MouseEvent) => {
  const target = event.target as HTMLElement;

  if (!target.closest("button") && !target.closest("[data-tag-item]")) clearSelection();
};

// Selection is page-scoped: a bulk delete must never reach tags the user can no longer see.
watch(searchQuery, () => {
  currentPage.value = 1;
  clearSelection();
});

watch(currentPage, clearSelection);

defineShortcuts({
  Delete: () => {
    if (selectedTags.value.size > 0) handleBulkDelete();
  },
  meta_a: (event) => {
    event.preventDefault();

    tagsList.value.forEach((tag) => {
      if (!tag.isSystem) selectedTags.value.add(tag.id);
    });
  },
});
</script>
