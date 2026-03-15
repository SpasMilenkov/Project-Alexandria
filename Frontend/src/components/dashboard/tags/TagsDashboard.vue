<template>
  <div class="flex flex-col h-full w-full flex-1">
    <!-- Toolbar -->
    <div
      class="flex items-center justify-between gap-3 px-6 py-4 border-b border-gray-200/70 dark:border-gray-700/70 flex-wrap"
    >
      <!-- Left: title + count -->
      <div class="flex items-center gap-2.5 min-w-0">
        <UIcon name="i-lucide-tags" class="w-5 h-5 text-muted shrink-0" />
        <h1 class="text-xl font-semibold truncate">Tags</h1>
        <UBadge v-if="tagsData" color="neutral" variant="subtle" size="sm">
          {{ tagsData.totalCount }}
        </UBadge>
      </div>

      <!-- Right: sort cluster + view toggle + destructive -->
      <div class="flex items-center gap-2 shrink-0">
        <!-- Sort cluster -->
        <div class="flex items-center gap-1">
          <USelectMenu
            v-model="selectedSortBy"
            :items="sortByOptions"
            size="sm"
            class="w-36"
            @update:model-value="handleSorting"
          >
            <template #leading>
              <UIcon name="i-lucide-arrow-up-down" class="w-4 h-4 text-muted" />
            </template>
          </USelectMenu>

          <UTooltip :text="sortDirection === 'asc' ? 'Ascending' : 'Descending'">
            <UButton
              size="sm"
              variant="ghost"
              color="neutral"
              :icon="sortDirection === 'asc' ? 'i-lucide-arrow-up' : 'i-lucide-arrow-down'"
              @click="toggleSortDirection"
            />
          </UTooltip>
        </div>

        <!-- Divider -->
        <div class="w-px h-5 bg-gray-200/70 dark:bg-gray-700/70" />

        <!-- View toggle -->
        <div class="flex items-center gap-1">
          <UTooltip text="Grid view">
            <UButton
              size="sm"
              :variant="viewMode === 'grid' ? 'soft' : 'ghost'"
              :color="viewMode === 'grid' ? 'primary' : 'neutral'"
              icon="i-lucide-layout-grid"
              @click="viewMode = 'grid'"
            />
          </UTooltip>
          <UTooltip text="List view">
            <UButton
              size="sm"
              :variant="viewMode === 'list' ? 'soft' : 'ghost'"
              :color="viewMode === 'list' ? 'primary' : 'neutral'"
              icon="i-lucide-list"
              @click="viewMode = 'list'"
            />
          </UTooltip>
        </div>

        <!-- Divider -->
        <div class="w-px h-5 bg-gray-200/70 dark:bg-gray-700/70" />

        <!-- Primary action -->
        <UButton color="primary" size="sm" icon="i-lucide-tag" @click="handleCreateTag">
          <span class="hidden sm:inline">New Tag</span>
        </UButton>

        <!-- Destructive — only visible when items are selected -->
        <Transition
          enter-active-class="transition-all duration-200 ease-out"
          enter-from-class="opacity-0 scale-95"
          enter-to-class="opacity-100 scale-100"
          leave-active-class="transition-all duration-150 ease-in"
          leave-from-class="opacity-100 scale-100"
          leave-to-class="opacity-0 scale-95"
        >
          <div v-if="selectedTags.size > 0">
            <UButton
              size="sm"
              color="error"
              variant="outline"
              icon="i-lucide-trash-2"
              @click="handleBulkDelete"
            >
              <span class="hidden sm:inline">Delete ({{ selectedTags.size }})</span>
            </UButton>
          </div>
        </Transition>
      </div>
    </div>

    <!-- Content area -->
    <div ref="containerRef" class="flex-1 overflow-auto" @click="handleContainerClick">
      <div class="px-6 py-5 space-y-4">
        <UInput
          v-model="searchQuery"
          placeholder="Search tags..."
          size="lg"
          class="w-full"
          @input="handleSearch"
        >
          <template #leading>
            <UIcon name="i-lucide-search" class="w-4 h-4 text-muted" />
          </template>
          <template #trailing>
            <UButton
              v-if="searchQuery"
              variant="ghost"
              color="neutral"
              size="xs"
              icon="i-lucide-x"
              @click="
                searchQuery = '';
                handleSearch();
              "
            />
          </template>
        </UInput>

        <!-- Loading -->
        <div v-if="isLoading">
          <div v-if="viewMode === 'grid'" class="grid gap-3" :class="gridColumns">
            <USkeleton v-for="i in 12" :key="i" class="h-28 rounded-xl" />
          </div>
          <div v-else class="space-y-2">
            <USkeleton v-for="i in 8" :key="i" class="h-14 rounded-lg" />
          </div>
        </div>

        <!-- Empty state -->
        <div
          v-else-if="!tagsList.length"
          class="flex flex-col items-center justify-center py-24 text-center gap-3"
        >
          <UIcon name="i-lucide-tag-off" class="w-12 h-12 text-muted" />
          <div class="space-y-1">
            <p class="text-sm font-medium">No tags found</p>
            <p class="text-xs text-muted">
              {{
                searchQuery ? "Try a different search term" : "Create your first tag to get started"
              }}
            </p>
          </div>
          <UButton
            v-if="!searchQuery"
            color="primary"
            size="sm"
            icon="i-lucide-tag"
            class="mt-1"
            @click="handleCreateTag"
          >
            New Tag
          </UButton>
        </div>

        <!-- Grid view -->
        <template v-else-if="viewMode === 'grid'">
          <div class="grid gap-3" :class="gridColumns">
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
          <div v-if="tagsData?.hasNext" class="flex justify-end pt-2">
            <UButton variant="ghost" color="neutral" @click="loadMore">Show more</UButton>
          </div>
        </template>

        <!-- List view -->
        <template v-else>
          <div
            class="rounded-lg border border-gray-200/70 dark:border-gray-700/70 overflow-hidden bg-white/60 dark:bg-white/5 backdrop-blur-sm divide-y divide-gray-100/50 dark:divide-gray-800/50"
          >
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
          <div v-if="tagsData?.hasNext" class="flex justify-end pt-2">
            <UButton variant="ghost" color="neutral" @click="loadMore">Show more</UButton>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useQuery } from "@pinia/colada";
import { searchTag } from "@/queries/tags";
import { deleteTag } from "@/mutations/tags";
import type { SearchTagsSchema } from "@/schemas/tag";
import TagCard from "./TagCard.vue";
import TagListItem from "./TagListItem.vue";
import CreateTagModal from "./modals/CreateTagModal.vue";
import UpdateTagModal from "./modals/UpdateTagModal.vue";
import type { TagDto } from "@/api/tag";
import { useSettingsStore } from "@/stores/settings";

const settingsStore = useSettingsStore();
const toast = useToast();
const overlay = useOverlay();

const createTagModal = overlay.create(CreateTagModal);
const updateTagModal = overlay.create(UpdateTagModal);

// View state
const viewMode = ref<"grid" | "list">("grid");
const searchQuery = ref("");
const selectedTags = ref<Set<string>>(new Set());
const lastSelected = ref<string | null>(null);
const containerRef = ref<HTMLElement | null>(null);

// Sorting
const sortByOptions = [
  { label: "Name", value: "name" },
  { label: "Date Created", value: "createdAt" },
  { label: "Date Modified", value: "updatedAt" },
];

const selectedSortBy = ref({ label: "Name", value: "name" });
const sortDirection = ref<"asc" | "desc">("asc");

// Pagination
const currentPage = ref(1);
const pageSize = ref(25);

// Search filters
const searchFilters = computed<SearchTagsSchema>(() => ({
  SortBy: selectedSortBy.value.value,
  name: searchQuery.value || undefined,
  page: currentPage.value,
  pageSize: pageSize.value,
  sortDirection: sortDirection.value,
}));

// Query
const { data: tagsData, isLoading, refetch } = useQuery(searchTag(searchFilters.value));

const tagsList = computed(() => tagsData.value?.items || []);

// Mutations
const { mutateAsync: deleteTagMutate } = deleteTag();

// Grid columns
const gridColumns = computed(() => "grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4");

// Handlers
const handleCreateTag = async () => {
  const instance = createTagModal.open();
  const shouldRefresh = await instance.result;
  if (shouldRefresh) {
    if (settingsStore.toastLevel === "all") {
      toast.add({ color: "success", id: "tag-created", title: "Tag created successfully" });
    }
    refetch();
  }
};

const handleEditTag = async (tag: TagDto) => {
  const instance = updateTagModal.open({ tag });
  const shouldRefresh = await instance.result;
  if (shouldRefresh) {
    if (settingsStore.toastLevel === "all") {
      toast.add({ color: "success", id: "tag-updated", title: "Tag updated successfully" });
    }
    refetch();
  }
};

const handleDeleteTag = async (tagId: string) => {
  try {
    await deleteTagMutate(tagId);
    if (settingsStore.toastLevel === "all") {
      toast.add({ color: "success", id: "tag-deleted", title: "Tag deleted successfully" });
    }
    selectedTags.value.delete(tagId);
    refetch();
  } catch {
    if (settingsStore.toastLevel !== "silent") {
      toast.add({ color: "error", id: "tag-delete-error", title: "Failed to delete tag" });
    }
  }
};

const handleBulkDelete = async () => {
  if (selectedTags.value.size === 0) return;
  try {
    await Promise.all(Array.from(selectedTags.value).map((id) => deleteTagMutate(id)));
    if (settingsStore.toastLevel === "all") {
      toast.add({
        color: "success",
        id: "bulk-delete",
        title: `Deleted ${selectedTags.value.size} tags`,
      });
    }
    selectedTags.value.clear();
    refetch();
  } catch {
    if (settingsStore.toastLevel !== "silent") {
      toast.add({ color: "error", id: "bulk-delete-error", title: "Failed to delete some tags" });
    }
  }
};

const handleTagClick = (event: MouseEvent, tagId: string) => {
  const isCtrlOrCmd = event.ctrlKey || event.metaKey;
  const isShift = event.shiftKey;
  if (isShift && lastSelected.value) {
    selectRange(lastSelected.value, tagId);
  } else if (isCtrlOrCmd) {
    toggleSelect(tagId);
    lastSelected.value = tagId;
  } else {
    clearSelection();
    toggleSelect(tagId);
    lastSelected.value = tagId;
  }
};

const toggleSelect = (tagId: string) => {
  if (selectedTags.value.has(tagId)) selectedTags.value.delete(tagId);
  else selectedTags.value.add(tagId);
};

const clearSelection = () => {
  selectedTags.value.clear();
  lastSelected.value = null;
};

const selectRange = (startId: string, endId: string) => {
  const startIndex = tagsList.value.findIndex((t) => t.id === startId);
  const endIndex = tagsList.value.findIndex((t) => t.id === endId);
  if (startIndex === -1 || endIndex === -1) return;
  const [from, to] = startIndex < endIndex ? [startIndex, endIndex] : [endIndex, startIndex];
  for (let i = from; i <= to; i++) selectedTags.value.add(tagsList.value[i].id);
};

const handleContainerClick = (event: MouseEvent) => {
  const target = event.target as HTMLElement;
  if (!target.closest("button") && !target.closest("[data-tag-item]")) clearSelection();
};

const handleSorting = () => {
  currentPage.value = 1;
  refetch();
};
const toggleSortDirection = () => {
  sortDirection.value = sortDirection.value === "asc" ? "desc" : "asc";
  currentPage.value = 1;
  refetch();
};
const handleSearch = () => {
  currentPage.value = 1;
  refetch();
};
const loadMore = () => {
  if (tagsData.value?.hasNext) currentPage.value++;
};

defineShortcuts({
  Delete: () => {
    if (selectedTags.value.size > 0) handleBulkDelete();
  },
  meta_a: (event) => {
    event.preventDefault();
    tagsList.value.forEach((tag) => selectedTags.value.add(tag.id));
  },
});

watch(searchFilters, () => refetch(), { deep: true });
</script>

<style scoped></style>
