<template>
  <div class="flex flex-col h-full w-full">
    <div class="flex-1 overflow-auto">
      <div class="px-4 sm:px-6 py-5 space-y-4">
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div class="min-w-0">
            <h1 class="text-lg font-semibold text-gray-900 dark:text-gray-100">Deleted items</h1>
            <p class="text-sm text-gray-600 dark:text-gray-400">{{ subtitle }}</p>
          </div>

          <div class="flex items-center gap-2 shrink-0">
            <USelect v-model="daysFilter" :items="daysFilterOptions" size="sm" class="w-40" />
            <UTooltip text="Refresh">
              <UButton
                variant="ghost"
                color="neutral"
                size="sm"
                icon="i-lucide-refresh-cw"
                aria-label="Refresh"
                :loading="isLoading"
                @click="refreshData"
              />
            </UTooltip>
          </div>
        </div>

        <UInput
          v-model="searchQuery"
          placeholder="Search deleted items by name"
          icon="i-lucide-search"
          size="lg"
          class="w-full"
        >
          <template #trailing>
            <UButton
              v-if="searchQuery"
              variant="ghost"
              color="neutral"
              size="xs"
              icon="i-lucide-x"
              aria-label="Clear search"
              @click="clearSearch"
            />
          </template>
        </UInput>

        <div
          v-if="hasResults && !isLoading"
          class="sticky top-0 z-10 flex items-center justify-between gap-4 px-4 py-2 rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface"
        >
          <UCheckbox
            :model-value="masterState"
            :label="selectionLabel"
            size="sm"
            :disabled="isMutating"
            @update:model-value="toggleSelectAll"
          />

          <Transition
            enter-active-class="transition-all duration-200 ease-out"
            leave-active-class="transition-all duration-150 ease-in"
            enter-from-class="opacity-0 scale-95"
            leave-to-class="opacity-0 scale-95"
          >
            <div v-if="selectedCount > 0" class="flex items-center gap-2">
              <UButton
                size="xs"
                color="neutral"
                variant="ghost"
                :disabled="isMutating"
                @click="clearSelection"
              >
                Clear
              </UButton>
              <UButton
                size="xs"
                color="primary"
                variant="solid"
                icon="i-lucide-rotate-ccw"
                :loading="isMutating"
                @click="restoreSelected"
              >
                Restore
              </UButton>
            </div>
          </Transition>
        </div>

        <div
          v-if="isLoading"
          class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface overflow-hidden divide-y divide-gray-100/50 dark:divide-gray-800/50"
        >
          <div v-for="n in 6" :key="n" class="flex items-center gap-4 px-4 py-3">
            <USkeleton class="w-4 h-4 rounded shrink-0" />
            <USkeleton class="w-8 h-8 rounded-lg shrink-0" />
            <USkeleton class="h-4 flex-1" />
            <USkeleton class="h-4 w-20" />
          </div>
        </div>

        <div v-else-if="hasError" class="flex flex-col items-center gap-2 py-16 text-center">
          <UIcon
            name="i-lucide-triangle-alert"
            class="w-12 h-12 text-gray-400 dark:text-gray-600"
          />
          <h2 class="text-base font-semibold text-gray-900 dark:text-gray-100">
            Couldn't load deleted items
          </h2>
          <p class="text-sm text-gray-600 dark:text-gray-400">
            Something went wrong while loading. Try again in a moment.
          </p>
          <UButton class="mt-2" color="neutral" variant="outline" size="sm" @click="refreshData">
            Try again
          </UButton>
        </div>

        <div v-else-if="!hasResults" class="flex flex-col items-center gap-2 py-16 text-center">
          <UIcon name="i-lucide-trash-2" class="w-12 h-12 text-gray-400 dark:text-gray-600" />
          <h2 class="text-base font-semibold text-gray-900 dark:text-gray-100">
            {{ emptyState.title }}
          </h2>
          <p class="max-w-xs text-sm text-gray-600 dark:text-gray-400">
            {{ emptyState.message }}
          </p>
          <UButton
            v-if="emptyState.action"
            class="mt-2"
            color="neutral"
            variant="outline"
            size="sm"
            @click="emptyState.action"
          >
            {{ emptyState.actionLabel }}
          </UButton>
        </div>

        <template v-else>
          <section v-if="directoryResults.length > 0">
            <h2
              class="flex items-center gap-2 mb-2 text-sm font-semibold text-gray-900 dark:text-gray-100"
            >
              <UIcon name="i-lucide-folder" class="w-4 h-4 text-gray-500 dark:text-gray-500" />
              Folders ({{ directoryResults.length }})
            </h2>
            <div
              class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface overflow-hidden divide-y divide-gray-100/50 dark:divide-gray-800/50"
            >
              <div
                v-for="dir in directoryResults"
                :key="dir.id"
                class="flex items-center gap-3 px-4 py-2 transition-colors"
                :class="rowClass(selectedDirectories.has(dir.id))"
              >
                <UCheckbox
                  :model-value="selectedDirectories.has(dir.id)"
                  size="sm"
                  :disabled="isMutating"
                  :aria-label="`Select ${dir.name}`"
                  @update:model-value="
                    (checked: boolean | 'indeterminate') =>
                      toggleDirectorySelection(dir.id, checked)
                  "
                />
                <DirectoryItem
                  :data="dir"
                  view-mode="list"
                  :is-selected="false"
                  class="flex-1 min-w-0"
                  @click="handleItemClick"
                  @navigate="handleNavigate"
                />
              </div>
            </div>
          </section>

          <section v-if="fileResults.length > 0">
            <h2
              class="flex items-center gap-2 mb-2 text-sm font-semibold text-gray-900 dark:text-gray-100"
            >
              <UIcon name="i-lucide-file" class="w-4 h-4 text-gray-500 dark:text-gray-500" />
              Files ({{ fileResults.length }})
            </h2>
            <div
              class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface overflow-hidden divide-y divide-gray-100/50 dark:divide-gray-800/50"
            >
              <div
                v-for="file in fileResults"
                :key="file.fileId"
                class="flex items-center gap-3 px-4 py-2 transition-colors"
                :class="rowClass(selectedFiles.has(file.fileId))"
              >
                <UCheckbox
                  :model-value="selectedFiles.has(file.fileId)"
                  size="sm"
                  :disabled="isMutating"
                  :aria-label="`Select ${file.name}`"
                  @update:model-value="
                    (checked: boolean | 'indeterminate') =>
                      toggleFileSelection(file.fileId, checked)
                  "
                />
                <FileItem
                  :data="file"
                  :is-selected="false"
                  view-mode="list"
                  class="flex-1 min-w-0"
                  @click="handleItemClick"
                  @file-restored="refreshData"
                />
              </div>
            </div>
          </section>

          <div
            v-if="totalPages > 1"
            class="flex flex-col sm:flex-row items-center justify-between gap-4 pt-2"
          >
            <p class="text-xs text-gray-500 dark:text-gray-500 tabular-nums">{{ rangeText }}</p>
            <UPagination
              :page="currentPage"
              :total="totalCount"
              :items-per-page="pageSize"
              :sibling-count="1"
              :disabled="isMutating"
              size="sm"
              color="neutral"
              variant="ghost"
              active-color="primary"
              active-variant="subtle"
              @update:page="goToPage"
            />
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { getLocalTimeZone, today } from "@internationalized/date";
import { watchDebounced } from "@vueuse/core";
import { computed, onMounted, ref, watch } from "vue";

import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";

import DirectoryItem from "@/components/dashboard/file-system/DirectoryItem.vue";
import FileItem from "@/components/dashboard/file-system/FileItem.vue";
import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { restoreDirectories } from "@/mutations/directories";
import { restoreFiles } from "@/mutations/files";
import { useDirectoryStore } from "@/stores/directory";
import { useFileStore } from "@/stores/file";
import { useSettingsStore } from "@/stores/settings";
import { logger } from "@/utils/logger";

// Stores and composables
const fileStore = useFileStore();
const directoryStore = useDirectoryStore();
const settingsStore = useSettingsStore();
const toast = useToast();

const {
  mutate: restoreFilesMutate,
  error: restoreFilesError,
  isLoading: restoreFilesLoading,
} = restoreFiles();

const {
  mutate: restoreDirectoriesMutate,
  error: restoreDirectoriesError,
  isLoading: restoreDirectoriesIsLoading,
} = restoreDirectories();

const DEFAULT_DAYS = 30;

// State
const isLoading = ref(false);
const hasError = ref(false);
const fileResults = ref<FileResult[]>([]);
const directoryResults = ref<DirectorySummaryDto[]>([]);
const totalCount = ref(0);
const currentPage = ref(1);
const pageSize = ref(20);
const searchQuery = ref("");
const daysFilter = ref(DEFAULT_DAYS);
const selectedFiles = ref<Set<string>>(new Set());
const selectedDirectories = ref<Set<string>>(new Set());

// Options
const daysFilterOptions = [
  { label: "Last 7 days", value: 7 },
  { label: "Last 14 days", value: 14 },
  { label: "Last 30 days", value: DEFAULT_DAYS },
];

const sortBy = ref(SortBy.UpdatedAt);
const sortDirection = ref(SortDirection.Desc);

// Computed
const hasResults = computed(
  () => fileResults.value.length > 0 || directoryResults.value.length > 0,
);

const totalPages = computed(() => Math.ceil(totalCount.value / pageSize.value));

const isMutating = computed(() => restoreFilesLoading.value || restoreDirectoriesIsLoading.value);

const subtitle = computed(() => {
  if (isLoading.value) {
    return "Loading deleted items";
  }
  const noun = totalCount.value === 1 ? "item" : "items";
  return `${totalCount.value} ${noun} deleted in the last ${daysFilter.value} days`;
});

const rangeText = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value + 1;
  const end = Math.min(currentPage.value * pageSize.value, totalCount.value);
  return `Showing ${start} to ${end} of ${totalCount.value}`;
});

const selectedCount = computed(() => selectedFiles.value.size + selectedDirectories.value.size);
const itemCount = computed(() => fileResults.value.length + directoryResults.value.length);

const masterState = computed<boolean | "indeterminate">(() => {
  if (selectedCount.value === 0) {
    return false;
  }
  if (selectedCount.value === itemCount.value) {
    return true;
  }
  return "indeterminate";
});

const selectionLabel = computed(() =>
  selectedCount.value > 0 ? `${selectedCount.value} selected` : "Select all",
);

const emptyState = computed(() => {
  const lookedBack = `in the last ${daysFilter.value} days`;
  if (searchQuery.value) {
    return {
      action: () => clearSearch(),
      actionLabel: "Clear search",
      message: `Nothing matching "${searchQuery.value}" was deleted ${lookedBack}.`,
      title: "No matches",
    };
  }
  if (daysFilter.value !== DEFAULT_DAYS) {
    return {
      action: () => {
        daysFilter.value = DEFAULT_DAYS;
      },
      actionLabel: `Look back ${DEFAULT_DAYS} days`,
      message: `Nothing has been deleted ${lookedBack}.`,
      title: "No deleted items",
    };
  }
  return {
    action: null,
    actionLabel: "",
    message: `Nothing has been deleted ${lookedBack}. Deleted items show up here so you can restore them.`,
    title: "Trash is empty",
  };
});

// Methods
const rowClass = (isSelected: boolean) =>
  isSelected ? "bg-primary/10" : "hover:bg-gray-50 dark:hover:bg-white/5";

const calculateDeletedAfterDate = (): string => {
  const now = today(getLocalTimeZone());
  const pastDate = now.subtract({ days: daysFilter.value });
  return pastDate.toString();
};

const clearSelection = () => {
  selectedFiles.value = new Set();
  selectedDirectories.value = new Set();
};

const toggleDirectorySelection = (id: string, checked: boolean | "indeterminate") => {
  if (checked === "indeterminate") return;
  const newSet = new Set(selectedDirectories.value);
  if (checked) newSet.add(id);
  else newSet.delete(id);
  selectedDirectories.value = newSet;
};

const toggleFileSelection = (id: string, checked: boolean | "indeterminate") => {
  if (checked === "indeterminate") return;
  const newSet = new Set(selectedFiles.value);
  if (checked) newSet.add(id);
  else newSet.delete(id);
  selectedFiles.value = newSet;
};

const toggleSelectAll = () => {
  if (selectedCount.value === itemCount.value) {
    clearSelection();
    return;
  }
  selectedDirectories.value = new Set(directoryResults.value.map((d) => d.id));
  selectedFiles.value = new Set(fileResults.value.map((f) => f.fileId));
};

const fetchDeletedItems = async () => {
  isLoading.value = true;
  hasError.value = false;
  clearSelection();

  try {
    const deletedAfter = calculateDeletedAfterDate();

    const [filesResult, directoriesResult] = await Promise.all([
      fileStore.searchFiles({
        createdAfter: null,
        createdBefore: null,
        currentPage: currentPage.value - 1,
        deletedAfter,
        deletedBefore: null,
        directoryId: null,
        isDeleted: true,
        isShared: false,
        isStarred: false,
        maxSize: null,
        mimeType: null,
        minSize: null,
        nameContains: searchQuery.value || undefined,
        onlyDeleted: true,
        ownerId: null,
        pageSize: pageSize.value,
        parentDirectoryId: null,
        sortBy: sortBy.value,
        sortDirection: sortDirection.value,
        updatedAfter: null,
        updatedBefore: null,
      }),
      directoryStore.searchDirectory({
        createdAfter: null,
        createdBefore: null,
        deletedAfter,
        deletedBefore: null,
        directoryId: null,
        hasFiles: null,
        hasSubdirectories: null,
        isDeleted: true,
        isShared: null,
        isStarred: undefined,
        nameContains: searchQuery.value || null,
        ownerId: null,
        page: currentPage.value - 1,
        pageSize: pageSize.value,
        parentDirectoryId: null,
        sortBy: sortBy.value,
        sortDirection: sortDirection.value,
        updatedAfter: null,
        updatedBefore: null,
      }),
    ]);

    fileResults.value = filesResult.success && filesResult.data ? filesResult.data.items : [];
    directoryResults.value =
      directoriesResult.success && directoriesResult.data ? directoriesResult.data.items : [];

    totalCount.value =
      (filesResult.success && filesResult.data ? filesResult.data.totalCount : 0) +
      (directoriesResult.success && directoriesResult.data ? directoriesResult.data.totalCount : 0);
  } catch (error) {
    logger.error("Error fetching deleted items:", error);
    hasError.value = true;
    if (settingsStore.toastLevel !== "silent") {
      toast.add({
        color: "error",
        description: "Failed to load deleted items. Please try again.",
        title: "Error",
      });
    }
  } finally {
    isLoading.value = false;
  }
};

const handleSearch = () => {
  currentPage.value = 1;
  fetchDeletedItems();
};
const clearSearch = () => {
  searchQuery.value = "";
};
const goToPage = (page: number) => {
  if (page >= 1 && page <= totalPages.value) {
    currentPage.value = page;
    fetchDeletedItems();
  }
};
const refreshData = () => fetchDeletedItems();
const handleItemClick = () => {};
const handleNavigate = (_directoryId: string) => {
  if (settingsStore.toastLevel === "all") {
    toast.add({
      color: "info",
      description: "This item is deleted. Restore it first to navigate to its location.",
      title: "Info",
    });
  }
};

const restoreSelected = () => {
  if (selectedFiles.value.size > 0) {
    restoreFilesMutate(Array.from(selectedFiles.value));
  }
  if (selectedDirectories.value.size > 0) {
    restoreDirectoriesMutate(Array.from(selectedDirectories.value));
  }
};

// Watchers
watchDebounced(searchQuery, handleSearch, { debounce: 300 });
watch(daysFilter, () => {
  currentPage.value = 1;
  fetchDeletedItems();
});
watch([sortBy, sortDirection], () => {
  currentPage.value = 1;
  fetchDeletedItems();
});

watch(restoreFilesError, (err) => {
  if (err)
    toast.add({
      color: "error",
      description: "Failed to restore the selected files. Please try again.",
      title: "Restore Failed",
    });
});
watch(restoreDirectoriesError, (err) => {
  if (err)
    toast.add({
      color: "error",
      description: "Failed to restore the selected folders. Please try again.",
      title: "Restore Failed",
    });
});
watch(restoreFilesLoading, (loading) => {
  if (!loading && !restoreFilesError.value) fetchDeletedItems();
});
watch(restoreDirectoriesIsLoading, (loading) => {
  if (!loading && !restoreDirectoriesError.value) fetchDeletedItems();
});

onMounted(() => fetchDeletedItems());
</script>
