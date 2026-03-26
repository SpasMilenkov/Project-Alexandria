<template>
  <div class="flex flex-col h-full w-full">
    <!-- Header -->
    <div
      class="flex items-center justify-between gap-3 px-6 py-4 border-b border-gray-200/70 dark:border-gray-700/70"
    >
      <div class="flex items-center gap-2.5 min-w-0">
        <UIcon name="i-lucide-trash-2" class="w-5 h-5 text-muted shrink-0" />
        <h1 class="text-xl font-semibold truncate">Deleted Items</h1>
        <UBadge v-if="totalCount > 0" color="neutral" variant="subtle" size="sm">
          {{ totalCount }}
        </UBadge>
      </div>

      <!-- Controls: day filter + icon-only refresh -->
      <div class="flex items-center gap-2 shrink-0">
        <USelect v-model="daysFilter" :items="daysFilterOptions" size="sm" class="w-36" />
        <UTooltip text="Refresh">
          <UButton
            variant="ghost"
            color="neutral"
            size="sm"
            icon="i-lucide-refresh-cw"
            :loading="isLoading"
            @click="refreshData"
          />
        </UTooltip>
      </div>
    </div>

    <!-- Content -->
    <div class="flex-1 overflow-auto">
      <div class="px-6 py-5 space-y-4">
        <!-- Search bar — full-width, prominent -->
        <UInput
          v-model="searchQuery"
          placeholder="Search deleted items by name..."
          size="lg"
          class="w-full"
          @keyup.enter="handleSearch"
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
              @click="clearSearch"
            />
          </template>
        </UInput>

        <!-- Loading state -->
        <div v-if="isLoading" class="flex flex-col items-center justify-center py-20 gap-3">
          <UIcon name="i-lucide-loader-circle" class="w-7 h-7 animate-spin text-muted" />
          <p class="text-sm text-muted">Loading deleted items…</p>
        </div>

        <!-- Empty state -->
        <div
          v-else-if="!hasResults"
          class="flex flex-col items-center justify-center py-20 text-center gap-3"
        >
          <UIcon name="i-lucide-trash-2" class="w-12 h-12 text-muted" />
          <div class="space-y-1">
            <p class="text-sm font-medium">No Deleted Items</p>
            <p class="text-xs text-muted max-w-xs">{{ emptyStateMessage }}</p>
          </div>
        </div>

        <!-- Results -->
        <template v-else>
          <!-- Deleted Directories -->
          <section v-if="directoryResults.length > 0">
            <!-- Section header -->
            <div class="flex items-center justify-between gap-2 mb-2">
              <div class="flex items-center gap-1.5">
                <UIcon name="i-lucide-folder" class="w-4 h-4 text-muted" />
                <span class="text-xs font-semibold uppercase tracking-widest text-muted">
                  Directories ({{ directoryResults.length }})
                </span>
              </div>
              <div class="flex items-center gap-2">
                <UCheckbox
                  :model-value="isAllDirectoriesSelected"
                  @update:model-value="toggleSelectAllDirectories"
                  label="Select all"
                  size="sm"
                  :disabled="isMutating"
                />
                <Transition
                  enter-active-class="transition-all duration-200 ease-out"
                  leave-active-class="transition-all duration-150 ease-in"
                  enter-from-class="opacity-0 scale-95"
                  leave-to-class="opacity-0 scale-95"
                >
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
                </Transition>
              </div>
            </div>

            <!-- Rows -->
            <div
              class="rounded-lg border border-gray-200/70 dark:border-gray-700/70 overflow-hidden bg-white/60 dark:bg-white/5 backdrop-blur-sm divide-y divide-gray-100/50 dark:divide-gray-800/50"
            >
              <div
                v-for="dir in directoryResults"
                :key="dir.id"
                class="flex items-center gap-2 px-3 py-2 hover:bg-gray-50/60 dark:hover:bg-white/5 transition-colors"
              >
                <UCheckbox
                  :model-value="selectedDirectories.has(dir.id)"
                  @update:model-value="
                    (checked: boolean) => toggleDirectorySelection(dir.id, checked)
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
                  class="flex-1 min-w-0"
                />
              </div>
            </div>
          </section>

          <!-- Deleted Files -->
          <section v-if="fileResults.length > 0">
            <!-- Section header -->
            <div class="flex items-center justify-between gap-2 mb-2">
              <div class="flex items-center gap-1.5">
                <UIcon name="i-lucide-file" class="w-4 h-4 text-muted" />
                <span class="text-xs font-semibold uppercase tracking-widest text-muted">
                  Files ({{ fileResults.length }})
                </span>
              </div>
              <div class="flex items-center gap-2">
                <UCheckbox
                  :model-value="isAllFilesSelected"
                  @update:model-value="toggleSelectAllFiles"
                  label="Select all"
                  size="sm"
                  :disabled="isMutating"
                />
                <Transition
                  enter-active-class="transition-all duration-200 ease-out"
                  leave-active-class="transition-all duration-150 ease-in"
                  enter-from-class="opacity-0 scale-95"
                  leave-to-class="opacity-0 scale-95"
                >
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
                </Transition>
              </div>
            </div>

            <!-- Rows -->
            <div
              class="rounded-lg border border-gray-200/70 dark:border-gray-700/70 overflow-hidden bg-white/60 dark:bg-white/5 backdrop-blur-sm divide-y divide-gray-100/50 dark:divide-gray-800/50"
            >
              <div
                v-for="file in fileResults"
                :key="file.fileId"
                class="flex items-center gap-2 px-3 py-2 hover:bg-gray-50/60 dark:hover:bg-white/5 transition-colors"
              >
                <UCheckbox
                  :model-value="selectedFiles.has(file.fileId)"
                  @update:model-value="
                    (checked: boolean) => toggleFileSelection(file.fileId, checked)
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
                  @file-restored="refreshData"
                  class="flex-1 min-w-0"
                />
              </div>
            </div>
          </section>

          <!-- Pagination -->
          <div v-if="totalPages > 1" class="flex items-center justify-between pt-2">
            <!-- Count — hidden on mobile -->
            <p class="hidden md:block text-xs text-muted tabular-nums">
              Showing {{ (currentPage - 1) * pageSize + 1 }}–{{
                Math.min(currentPage * pageSize, totalCount)
              }}
              of {{ totalCount }} items
            </p>
            <!-- Prev / page indicator / next — centered on mobile -->
            <div class="flex items-center gap-2 mx-auto md:mx-0">
              <UButton
                size="sm"
                color="neutral"
                variant="ghost"
                icon="i-lucide-chevron-left"
                :disabled="currentPage <= 1 || isMutating"
                @click="goToPage(currentPage - 1)"
              />
              <span class="text-xs text-muted tabular-nums min-w-[5rem] text-center">
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
        </template>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useFileStore } from "@/stores/file";
import { useDirectoryStore } from "@/stores/directory";
import DirectoryItem from "@/components/dashboard/file-system/DirectoryItem.vue";
import FileItem from "@/components/dashboard/file-system/FileItem.vue";
import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";
import { getLocalTimeZone, today } from "@internationalized/date";
import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { restoreFiles } from "@/mutations/files";
import { restoreDirectories } from "@/mutations/directories";
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

const sortBy = ref(SortBy.UpdatedAt);
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

const isAllDirectoriesSelected = computed(
  () =>
    directoryResults.value.length > 0 &&
    selectedDirectories.value.size === directoryResults.value.length,
);

const isAllFilesSelected = computed(
  () => fileResults.value.length > 0 && selectedFiles.value.size === fileResults.value.length,
);

const isMutating = computed(() => restoreFilesLoading.value || restoreDirectoriesIsLoading.value);

// Methods
const calculateDeletedAfterDate = (): string => {
  const now = today(getLocalTimeZone());
  const pastDate = now.subtract({ days: daysFilter.value });
  return pastDate.toString();
};

const toggleDirectorySelection = (id: string, checked: boolean) => {
  const newSet = new Set(selectedDirectories.value);
  if (checked) newSet.add(id);
  else newSet.delete(id);
  selectedDirectories.value = newSet;
};

const toggleFileSelection = (id: string, checked: boolean) => {
  const newSet = new Set(selectedFiles.value);
  if (checked) newSet.add(id);
  else newSet.delete(id);
  selectedFiles.value = newSet;
};

const toggleSelectAllDirectories = (checked: boolean) => {
  selectedDirectories.value = checked
    ? new Set(directoryResults.value.map((d) => d.id))
    : new Set();
};

const toggleSelectAllFiles = (checked: boolean) => {
  selectedFiles.value = checked ? new Set(fileResults.value.map((f) => f.fileId)) : new Set();
};

const fetchDeletedItems = async () => {
  isLoading.value = true;
  selectedFiles.value = new Set();
  selectedDirectories.value = new Set();

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
        nameContains: searchQuery.value || null,
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
  currentPage.value = 1;
  fetchDeletedItems();
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

const restoreSelectedFiles = () => {
  if (selectedFiles.value.size === 0) return;
  restoreFilesMutate(Array.from(selectedFiles.value));
};

const restoreSelectedDirectories = () => {
  if (selectedDirectories.value.size === 0) return;
  restoreDirectoriesMutate(Array.from(selectedDirectories.value));
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
      description: "Failed to restore the selected directories. Please try again.",
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

<style scoped></style>
