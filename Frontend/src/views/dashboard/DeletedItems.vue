<template>
  <div class="flex flex-col h-full w-full">
    <!-- Header -->
    <div
      class="flex w-full gap-2 p-4 border-b border-gray-200 dark:border-gray-800 items-center justify-between"
    >
      <div class="flex items-center gap-2">
        <div class="p-2 rounded-lg border border-dashed opacity-80 mt-0.5">
          <UIcon name="i-lucide-trash-2" class="w-6 h-6" />
        </div>
        <h1 class="text-2xl font-bold">Deleted Items</h1>
        <UBadge v-if="totalCount > 0" color="gray" variant="subtle">
          {{ totalCount }}
        </UBadge>
      </div>
      <div class="flex items-center gap-2">
        <USelect v-model="daysFilter" :items="daysFilterOptions" class="w-40" />
        <UButton
          variant="ghost"
          size="sm"
          @click="refreshData"
          :loading="isLoading"
        >
          <UIcon name="i-lucide-refresh-cw" class="w-4 h-4 mr-2" />
          Refresh
        </UButton>
      </div>
    </div>

    <!-- Search Bar -->
    <div class="p-4 border-b border-gray-200 dark:border-gray-800">
      <UInput
        v-model="searchQuery"
        placeholder="Search deleted items by name..."
        class="w-full"
        @keyup.enter="handleSearch"
      >
        <template #leading>
          <UIcon name="i-lucide-search" />
        </template>
        <template #trailing>
          <UButton
            v-if="searchQuery"
            variant="ghost"
            size="xs"
            icon="i-lucide-x"
            @click="clearSearch"
          />
        </template>
      </UInput>
    </div>

    <!-- Content -->
    <div class="flex-1 overflow-auto p-4">
      <!-- Loading State -->
      <div
        v-if="isLoading"
        class="flex flex-col items-center justify-center py-20"
      >
        <UIcon
          name="i-lucide-loader-circle"
          class="size-8 animate-spin text-muted mb-3"
        />
        <p class="text-sm text-muted">Loading deleted items...</p>
      </div>

      <!-- Empty State -->
      <div
        v-else-if="!hasResults"
        class="flex flex-col items-center justify-center py-20 text-center px-4"
      >
        <div class="rounded-full p-5 mb-4 bg-elevated border border-default">
          <UIcon name="i-lucide-trash-2" class="size-10 text-muted" />
        </div>
        <h4 class="text-sm font-medium mb-1">No Deleted Items</h4>
        <p class="text-xs text-muted max-w-xs">
          {{ emptyStateMessage }}
        </p>
      </div>

      <!-- Results -->
      <div v-else class="space-y-6">
        <!-- Deleted Directories -->
        <div v-if="directoryResults.length > 0">
          <div class="flex items-center justify-between mb-3">
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-folder" class="size-4 text-muted" />
              <h4
                class="text-xs font-semibold uppercase tracking-wider text-muted"
              >
                Directories ({{ directoryResults.length }})
              </h4>
            </div>
            <div class="flex items-center gap-2">
              <UCheckbox
                :model-value="isAllDirectoriesSelected"
                @update:model-value="toggleSelectAllDirectories"
                label="Select all"
                size="sm"
                :disabled="isMutating"
              />
              <UButton
                v-if="selectedDirectories.size > 0"
                size="xs"
                color="primary"
                variant="soft"
                icon="i-lucide-rotate-ccw"
                :loading="restoreDirectoriesIsLoading"
                :disabled="isMutating"
                @click="restoreSelectedDirectories"
              >
                Restore ({{ selectedDirectories.size }})
              </UButton>
            </div>
          </div>
          <div class="space-y-1.5">
            <div
              v-for="dir in directoryResults"
              :key="dir.id"
              class="flex items-center gap-2 p-2 rounded-lg"
            >
              <UCheckbox
                :model-value="selectedDirectories.has(dir.id)"
                @update:model-value="
                  (checked: boolean) =>
                    toggleDirectorySelection(dir.id, checked)
                "
                size="sm"
                :disabled="isMutating"
              />
              <DirectoryItem
                :data="dir"
                view-mode="list"
                :is-selected="false"
                @click="handleItemClick"
                @navigate="handleNavigate"
                class="flex-1"
              />
            </div>
          </div>
        </div>

        <!-- Deleted Files -->
        <div v-if="fileResults.length > 0">
          <div class="flex items-center justify-between mb-3">
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-file" class="size-4 text-muted" />
              <h4
                class="text-xs font-semibold uppercase tracking-wider text-muted"
              >
                Files ({{ fileResults.length }})
              </h4>
            </div>
            <div class="flex items-center gap-2">
              <UCheckbox
                :model-value="isAllFilesSelected"
                @update:model-value="toggleSelectAllFiles"
                label="Select all"
                size="sm"
                :disabled="isMutating"
              />
              <UButton
                v-if="selectedFiles.size > 0"
                size="xs"
                color="primary"
                variant="soft"
                icon="i-lucide-rotate-ccw"
                :loading="restoreFilesLoading"
                :disabled="isMutating"
                @click="restoreSelectedFiles"
              >
                Restore ({{ selectedFiles.size }})
              </UButton>
            </div>
          </div>
          <div class="space-y-1.5">
            <div
              v-for="file in fileResults"
              :key="file.fileId"
              class="flex items-center gap-2 p-2 rounded-lg"
            >
              <UCheckbox
                :model-value="selectedFiles.has(file.fileId)"
                @update:model-value="
                  (checked: boolean) =>
                    toggleFileSelection(file.fileId, checked)
                "
                size="sm"
                :disabled="isMutating"
              />
              <FileItem
                :data="file"
                :is-selected="false"
                :tags="file.tags"
                view-mode="list"
                @click="handleItemClick"
                class="flex-1"
              />
            </div>
          </div>
        </div>

        <!-- Pagination -->
        <div
          v-if="totalPages > 1"
          class="flex items-center justify-between pt-4 border-t border-gray-200 dark:border-gray-800"
        >
          <p class="text-sm text-muted">
            Showing {{ (currentPage - 1) * pageSize + 1 }} -
            {{ Math.min(currentPage * pageSize, totalCount) }} of
            {{ totalCount }} items
          </p>
          <div class="flex items-center gap-2">
            <UButton
              size="sm"
              color="neutral"
              variant="ghost"
              icon="i-lucide-chevron-left"
              :disabled="currentPage <= 1 || isMutating"
              @click="goToPage(currentPage - 1)"
            />
            <span class="text-sm text-muted">
              Page {{ currentPage }} of {{ totalPages }}
            </span>
            <UButton
              size="sm"
              color="neutral"
              variant="ghost"
              icon="i-lucide-chevron-right"
              :disabled="currentPage >= totalPages || isMutating"
              @click="goToPage(currentPage + 1)"
            />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from "vue";
import { useFileStore } from "@/stores/file";
import { useDirectoryStore } from "@/stores/directory";
import DirectoryItem from "@/components/dashboard/file-system/DirectoryItem.vue";
import FileItem from "@/components/dashboard/file-system/FileItem.vue";
import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";
import { today, getLocalTimeZone } from "@internationalized/date";
import { OrderBy } from "@/enums/OrderBy";
import { SortDirection } from "@/enums/SortDirection";
import { restoreFiles } from "@/mutations/files";
import { restoreDirectories } from "@/mutations/directories";

// Stores and composables
const fileStore = useFileStore();
const directoryStore = useDirectoryStore();
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

// State
const isLoading = ref(false);
const fileResults = ref<FileResult[]>([]);
const directoryResults = ref<DirectorySummaryDto[]>([]);
const totalCount = ref(0);
const currentPage = ref(1);
const pageSize = ref(20);
const searchQuery = ref("");
const daysFilter = ref(30);
const selectedFiles = ref<Set<string>>(new Set());
const selectedDirectories = ref<Set<string>>(new Set());

// Options
const daysFilterOptions = [
  { label: "Last 7 days", value: 7 },
  { label: "Last 14 days", value: 14 },
  { label: "Last 30 days", value: 30 },
];

const sortBy = ref(OrderBy.UpdatedAt);
const sortDirection = ref(SortDirection.Desc);

// Computed
const hasResults = computed(
  () => fileResults.value.length > 0 || directoryResults.value.length > 0,
);

const totalPages = computed(() => Math.ceil(totalCount.value / pageSize.value));

const emptyStateMessage = computed(() => {
  if (searchQuery.value) {
    return `No deleted items matching "${searchQuery.value}" in the last ${daysFilter.value} days.`;
  }
  return `No items have been deleted in the last ${daysFilter.value} days.`;
});

const isAllDirectoriesSelected = computed(() => {
  return (
    directoryResults.value.length > 0 &&
    selectedDirectories.value.size === directoryResults.value.length
  );
});

const isAllFilesSelected = computed(() => {
  return (
    fileResults.value.length > 0 &&
    selectedFiles.value.size === fileResults.value.length
  );
});

// Aggregate mutation loading state — used to disable interactive elements
// while any mutation is in flight to prevent double-submits or conflicting ops.
const isMutating = computed(
  () => restoreFilesLoading.value || restoreDirectoriesIsLoading.value,
);

// Methods
const calculateDeletedAfterDate = (): string => {
  const now = today(getLocalTimeZone());
  const pastDate = now.subtract({ days: daysFilter.value });
  return pastDate.toString();
};

const toggleDirectorySelection = (id: string, checked: boolean) => {
  const newSet = new Set(selectedDirectories.value);
  if (checked) {
    newSet.add(id);
  } else {
    newSet.delete(id);
  }
  selectedDirectories.value = newSet;
};

const toggleFileSelection = (id: string, checked: boolean) => {
  const newSet = new Set(selectedFiles.value);
  if (checked) {
    newSet.add(id);
  } else {
    newSet.delete(id);
  }
  selectedFiles.value = newSet;
};

const toggleSelectAllDirectories = (checked: boolean) => {
  if (checked) {
    selectedDirectories.value = new Set(
      directoryResults.value.map((d) => d.id),
    );
  } else {
    selectedDirectories.value = new Set();
  }
};

const toggleSelectAllFiles = (checked: boolean) => {
  if (checked) {
    selectedFiles.value = new Set(fileResults.value.map((f) => f.fileId));
  } else {
    selectedFiles.value = new Set();
  }
};

const fetchDeletedItems = async () => {
  isLoading.value = true;
  selectedFiles.value = new Set();
  selectedDirectories.value = new Set();

  try {
    const deletedAfter = calculateDeletedAfterDate();

    // Fetch files
    const filesResult = await fileStore.searchFiles({
      nameContains: searchQuery.value || null,
      isDeleted: true,
      deletedAfter,
      currentPage: currentPage.value - 1,
      pageSize: pageSize.value,
      sortBy: sortBy.value,
      sortDirection: sortDirection.value,
      minSize: null,
      maxSize: null,
      mimeType: null,
      onlyDeleted: true,
      directoryId: null,
      parentDirectoryId: null,
      ownerId: null,
      isShared: false,
      isStarred: false,
      createdAfter: null,
      createdBefore: null,
      updatedAfter: null,
      updatedBefore: null,
      deletedBefore: null,
    });

    // Fetch directories
    const directoriesResult = await directoryStore.searchDirectory({
      nameContains: searchQuery.value || null,
      isDeleted: true,
      deletedAfter,
      deletedBefore: null,
      page: currentPage.value - 1,
      pageSize: pageSize.value,
      sortBy: sortBy.value,
      sortDirection: sortDirection.value,
      directoryId: null,
      parentDirectoryId: null,
      ownerId: null,
      isShared: null,
      isStarred: undefined,
      hasFiles: null,
      hasSubdirectories: null,
      createdAfter: null,
      createdBefore: null,
      updatedAfter: null,
      updatedBefore: null,
    });

    if (filesResult.success && filesResult.data) {
      fileResults.value = filesResult.data.items;
    } else {
      fileResults.value = [];
    }

    if (directoriesResult.success && directoriesResult.data) {
      directoryResults.value = directoriesResult.data.items;
    } else {
      directoryResults.value = [];
    }

    totalCount.value =
      (filesResult.success && filesResult.data
        ? filesResult.data.totalCount
        : 0) +
      (directoriesResult.success && directoriesResult.data
        ? directoriesResult.data.totalCount
        : 0);
  } catch (error) {
    console.error("Error fetching deleted items:", error);
    toast.add({
      title: "Error",
      description: "Failed to load deleted items. Please try again.",
      color: "error",
    });
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
  currentPage.value = 1;
  fetchDeletedItems();
};

const goToPage = (page: number) => {
  if (page >= 1 && page <= totalPages.value) {
    currentPage.value = page;
    fetchDeletedItems();
  }
};

const refreshData = () => {
  fetchDeletedItems();
};

const handleItemClick = () => {};

const handleNavigate = (_directoryId: string) => {
  toast.add({
    title: "Info",
    description:
      "This item is deleted. Restore it first to navigate to its location.",
    color: "info",
  });
};

const restoreSelectedFiles = async () => {
  if (selectedFiles.value.size === 0) return;
  const filesToRestore = Array.from(selectedFiles.value);
  restoreFilesMutate(filesToRestore);
};

const restoreSelectedDirectories = async () => {
  if (selectedDirectories.value.size === 0) return;
  const dirsToRestore = Array.from(selectedDirectories.value);
  restoreDirectoriesMutate(dirsToRestore);
};

// Watchers
watch(daysFilter, () => {
  currentPage.value = 1;
  fetchDeletedItems();
});

watch([sortBy, sortDirection], () => {
  currentPage.value = 1;
  fetchDeletedItems();
});

watch(restoreFilesError, (err) => {
  if (err) {
    toast.add({
      title: "Restore Failed",
      description: "Failed to restore the selected files. Please try again.",
      color: "error",
    });
  }
});

watch(restoreDirectoriesError, (err) => {
  if (err) {
    toast.add({
      title: "Restore Failed",
      description:
        "Failed to restore the selected directories. Please try again.",
      color: "error",
    });
  }
});

watch(restoreFilesLoading, (loading) => {
  if (!loading && !restoreFilesError.value) fetchDeletedItems();
});

watch(restoreDirectoriesIsLoading, (loading) => {
  if (!loading && !restoreDirectoriesError.value) fetchDeletedItems();
});

onMounted(() => {
  fetchDeletedItems();
});
</script>

<style scoped></style>
