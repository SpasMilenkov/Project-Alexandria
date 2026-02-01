<template>
  <div class="flex flex-col h-full w-full flex-1">
    <!-- Toolbar -->
    <div
      class="flex w-full gap-6 md:gap-2 p-3 border-b border-b-primary flex-row items-center justify-between flex-wrap"
    >
      <div class="flex gap-2 justify-between">
        <div class="flex gap-2">
          <UButton color="primary" size="sm" @click="handleCreateTag">
            <Icon icon="mdi:tag-plus" class="w-4 h-4 md:mr-1" />
            <span class="hidden sm:inline">New Tag</span>
          </UButton>

          <UButton
            color="red"
            size="sm"
            @click="handleBulkDelete"
            v-if="selectedTags.size > 0"
            :disabled="selectedTags.size === 0"
          >
            <Icon icon="mdi:delete" class="w-4 h-4 md:mr-1" />
            <span class="hidden sm:inline"
              >Delete Selected ({{ selectedTags.size }})</span
            >
          </UButton>
        </div>

        <div class="text-sm flex gap-2">
          <USelectMenu
            v-model="selectedSortBy"
            :items="sortByOptions"
            size="sm"
            placeholder="Sort by"
            @update:model-value="handleSorting"
          >
            <template #default="{ modelValue }">
              <Icon icon="mdi:sort" class="w-4 h-4 mr-1" />
              <span class="hidden sm:inline">{{ modelValue?.label }}</span>
            </template>
          </USelectMenu>

          <UButton
            size="sm"
            variant="ghost"
            @click="toggleSortDirection"
            :aria-label="sortDirection === 'asc' ? 'Ascending' : 'Descending'"
          >
            <Icon
              :icon="
                sortDirection === 'asc'
                  ? 'mdi:sort-ascending'
                  : 'mdi:sort-descending'
              "
              class="w-4 h-4"
            />
          </UButton>

          <UButton
            :variant="viewMode === 'grid' ? 'solid' : 'ghost'"
            size="sm"
            @click="viewMode = 'grid'"
            aria-label="Grid view"
          >
            <Icon icon="mdi:view-grid" class="w-4 h-4" />
          </UButton>
          <UButton
            :variant="viewMode === 'list' ? 'solid' : 'ghost'"
            size="sm"
            @click="viewMode = 'list'"
            aria-label="List view"
          >
            <Icon icon="mdi:view-list" class="w-4 h-4" />
          </UButton>
        </div>
      </div>

      <!-- Search Input -->
      <div class="flex-1 max-w-md">
        <UInput
          v-model="searchQuery"
          placeholder="Search tags..."
          icon="i-heroicons-magnifying-glass"
          size="sm"
          @input="handleSearch"
        />
      </div>
    </div>

    <!-- Stats Bar -->
    <div class="px-4 py-2 border-b border-b-primary">
      <div class="flex items-center gap-4 text-sm opacity-70">
        <span>{{ tagsData?.totalCount || 0 }} total tags</span>
        <span v-if="selectedTags.size > 0"
          >{{ selectedTags.size }} selected</span
        >
      </div>
    </div>

    <!-- Content Area -->
    <div
      ref="containerRef"
      class="flex-1 overflow-auto relative"
      @click="handleContainerClick"
    >
      <!-- Loading State -->
      <div v-if="isLoading" class="p-4">
        <div v-if="viewMode === 'grid'" class="grid gap-3" :class="gridColumns">
          <USkeleton v-for="i in 12" :key="i" class="h-24" />
        </div>
        <div v-else class="space-y-2">
          <USkeleton v-for="i in 8" :key="i" class="h-16" />
        </div>
      </div>

      <!-- Empty State -->
      <div
        v-else-if="!tagsList.length"
        class="flex flex-col items-center justify-center h-full text-center p-8"
      >
        <Icon icon="mdi:tag-off" class="w-16 h-16 opacity-40 mb-4" />
        <h3 class="text-lg font-semibold mb-2">No tags found</h3>
        <p class="opacity-70 mb-4">
          {{
            searchQuery
              ? "Try a different search term"
              : "Create your first tag to get started"
          }}
        </p>
        <UButton v-if="!searchQuery" color="primary" @click="handleCreateTag">
          <Icon icon="mdi:tag-plus" class="w-4 h-4 mr-2" />
          Create Tag
        </UButton>
      </div>

      <!-- Grid View -->
      <div v-else-if="viewMode === 'grid'" class="p-4">
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

        <!-- Load More Button -->
        <div v-if="tagsData?.hasNext" class="flex justify-end mt-4">
          <UButton variant="ghost" @click="loadMore"> Show more </UButton>
        </div>
      </div>

      <!-- List View -->
      <div v-else class="flex flex-col">
        <TagListItem
          v-for="tag in tagsList"
          :key="tag.id"
          :tag="tag"
          :is-selected="selectedTags.has(tag.id)"
          @click="handleTagClick($event, tag.id)"
          @edit="handleEditTag"
          @delete="handleDeleteTag"
        />

        <!-- Load More Button -->
        <div v-if="tagsData?.hasNext" class="flex justify-end p-4">
          <UButton variant="ghost" @click="loadMore"> Show more </UButton>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { searchTag } from "@/queries/tags";
import { deleteTag } from "@/mutations/tags";
import type { SearchTagsSchema } from "@/schemas/tag";
import TagCard from "./TagCard.vue";
import TagListItem from "./TagListItem.vue";
import CreateTagModal from "./modals/CreateTagModal.vue";
import UpdateTagModal from "./modals/UpdateTagModal.vue";
import type { TagDto } from "@/api/tag";

const toast = useToast();
const overlay = useOverlay();

// Modals
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
  page: currentPage.value,
  pageSize: pageSize.value,
  orderBy: selectedSortBy.value.value,
  sortDirection: sortDirection.value,
  name: searchQuery.value || undefined,
}));

// Query
const {
  data: tagsData,
  isLoading,
  refetch,
} = useQuery(searchTag(searchFilters.value));

const tagsList = computed(() => tagsData.value?.items || []);

// Mutations
const { mutateAsync: deleteTagMutate } = deleteTag();

// Grid columns
const gridColumns = computed(() => {
  return "grid-cols-1 sm:grid-cols-2 md:grid-cols-2 lg:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4";
});

// Handlers
const handleCreateTag = async () => {
  const instance = createTagModal.open();
  const shouldRefresh = await instance.result;

  if (shouldRefresh) {
    toast.add({
      title: "Tag created successfully",
      color: "success",
      id: "tag-created",
    });
    refetch();
  }
};

const handleEditTag = async (tag: TagDto) => {
  const instance = updateTagModal.open({ tag });
  const shouldRefresh = await instance.result;

  if (shouldRefresh) {
    toast.add({
      title: "Tag updated successfully",
      color: "success",
      id: "tag-updated",
    });
    refetch();
  }
};

const handleDeleteTag = async (tagId: string) => {
  try {
    await deleteTagMutate(tagId);
    toast.add({
      title: "Tag deleted successfully",
      color: "success",
      id: "tag-deleted",
    });
    selectedTags.value.delete(tagId);
    refetch();
  } catch {
    toast.add({
      title: "Failed to delete tag",
      color: "error",
      id: "tag-delete-error",
    });
  }
};

const handleBulkDelete = async () => {
  if (selectedTags.value.size === 0) return;

  try {
    await Promise.all(
      Array.from(selectedTags.value).map((tagId) => deleteTagMutate(tagId)),
    );
    toast.add({
      title: `Deleted ${selectedTags.value.size} tags`,
      color: "success",
      id: "bulk-delete",
    });
    selectedTags.value.clear();
    refetch();
  } catch {
    toast.add({
      title: "Failed to delete some tags",
      color: "error",
      id: "bulk-delete-error",
    });
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

  const [from, to] =
    startIndex < endIndex ? [startIndex, endIndex] : [endIndex, startIndex];

  for (let i = from; i <= to; i++) {
    selectedTags.value.add(tagsList.value[i].id);
  }
};

const handleContainerClick = (event: MouseEvent) => {
  const target = event.target as HTMLElement;
  if (!target.closest("button") && !target.closest("[data-tag-item]")) {
    clearSelection();
  }
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
  if (tagsData.value?.hasNext) {
    currentPage.value++;
  }
};

// Keyboard shortcuts
defineShortcuts({
  meta_a: (event) => {
    event.preventDefault();
    tagsList.value.forEach((tag) => selectedTags.value.add(tag.id));
  },
  escape: () => {
    clearSelection();
  },
  Delete: () => {
    if (selectedTags.value.size > 0) {
      handleBulkDelete();
    }
  },
});

// Watch for search filters change
watch(
  searchFilters,
  () => {
    refetch();
  },
  { deep: true },
);
</script>

<style scoped>
</style>
