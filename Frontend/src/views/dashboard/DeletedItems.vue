<template>
  <div class="flex h-full w-full flex-1 flex-col gap-3 p-3">
    <header
      class="flex shrink-0 flex-wrap items-center gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:flex-nowrap md:px-5 dark:border-gray-700/70"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <UIcon name="i-lucide-trash-2" class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400" />
        <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">Trash</h1>
        <UBadge v-if="!isPending" color="neutral" variant="subtle" size="sm">
          {{ totalCount }}
        </UBadge>
      </div>

      <UInput
        v-model="searchQuery"
        placeholder="Search deleted items..."
        aria-label="Search deleted items"
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
        <USelect
          v-model="daysFilter"
          :items="DAYS_FILTER_OPTIONS"
          size="sm"
          class="w-36"
          aria-label="Deleted within"
        />

        <UTooltip text="Refresh">
          <UButton
            variant="ghost"
            color="neutral"
            size="sm"
            icon="i-lucide-refresh-cw"
            square
            class="justify-center"
            aria-label="Refresh"
            :loading="isFetching"
            @click="refresh"
          />
        </UTooltip>
      </div>
    </header>

    <section
      aria-label="Deleted items"
      class="flex min-h-0 flex-1 flex-col overflow-hidden rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70"
    >
      <div
        v-if="hasResults"
        class="flex min-h-12 shrink-0 items-center justify-between gap-3 border-b border-gray-200/70 px-4 py-2 dark:border-gray-700/70"
      >
        <UCheckbox
          :model-value="masterState"
          :label="selectionLabel"
          size="sm"
          :disabled="isRestoring"
          :ui="{ label: 'text-gray-900 dark:text-gray-100' }"
          @update:model-value="toggleSelectAll"
        />

        <Transition
          enter-active-class="transition-all duration-200 ease-out"
          enter-from-class="opacity-0"
          leave-active-class="transition-all duration-150 ease-in"
          leave-to-class="opacity-0"
        >
          <div v-if="selectedCount > 0" class="flex items-center gap-2">
            <UButton
              size="sm"
              color="neutral"
              variant="ghost"
              :disabled="isRestoring"
              @click="clearSelection"
            >
              Clear
            </UButton>

            <UButton
              size="sm"
              color="primary"
              icon="i-lucide-rotate-ccw"
              :loading="isRestoring"
              @click="restoreSelected"
            >
              Restore
            </UButton>
          </div>
        </Transition>
      </div>

      <div class="min-h-0 flex-1 overflow-y-auto" :class="{ 'flex flex-col': isEmpty }">
        <div v-if="isPending" class="flex justify-center py-16">
          <BlockSpinner :size="24" aria-label="Loading deleted items" class="text-gray-500" />
        </div>

        <div
          v-else-if="visibleError"
          class="flex flex-col items-center gap-3 px-4 py-16 text-center"
        >
          <UIcon
            name="i-lucide-triangle-alert"
            class="h-12 w-12 text-gray-400 dark:text-gray-600"
          />

          <div class="space-y-1">
            <h2 class="text-sm font-medium text-gray-900 dark:text-gray-100">
              Couldn't load deleted items
            </h2>
            <p class="text-xs text-gray-600 dark:text-gray-400">
              Something went wrong while loading. Try again in a moment.
            </p>
          </div>

          <UButton color="neutral" variant="outline" size="sm" @click="refresh">
            Try again
          </UButton>
        </div>

        <EmptyStatePanel v-else-if="!hasResults" :panel="false">
          <template #ghost>
            <TrashGhost />
          </template>

          <GlassEmptyState
            :icon="emptyState.icon"
            :title="emptyState.title"
            :description="emptyState.message"
            class="m-auto"
          >
            <UButton
              v-if="emptyState.action"
              color="neutral"
              variant="outline"
              class="rounded-full px-5"
              @click="emptyState.action.run"
            >
              {{ emptyState.action.label }}
            </UButton>
          </GlassEmptyState>
        </EmptyStatePanel>

        <template v-else>
          <div v-if="directories.length > 0">
            <h2 :class="GROUP_HEADING_CLASS">Folders · {{ directoryTotal }}</h2>
            <ul class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
              <TrashRow
                v-for="dir in directories"
                :key="dir.id"
                :name="dir.name"
                icon="mdi:folder"
                detail="Folder"
                :is-selected="selectedDirectories.has(dir.id)"
                :disabled="isRestoring"
                @toggle="toggleIn(selectedDirectories, dir.id)"
              />
            </ul>
          </div>

          <div v-if="files.length > 0">
            <h2 :class="GROUP_HEADING_CLASS">Files · {{ fileTotal }}</h2>
            <ul class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
              <TrashRow
                v-for="file in files"
                :key="file.fileId"
                :name="file.fileName"
                :icon="getFileIcon(file.fileName)"
                :detail="formatBytes(Number(file.currentVersion.size))"
                :deleted-label="deletedLabel(file)"
                :is-selected="selectedFiles.has(file.fileId)"
                :disabled="isRestoring"
                @toggle="toggleIn(selectedFiles, file.fileId)"
              />
            </ul>
          </div>
        </template>
      </div>

      <div
        v-if="pageCount > 1"
        class="flex shrink-0 justify-center border-t border-gray-200/70 px-4 py-2 dark:border-gray-700/70"
      >
        <UPagination
          v-model:page="currentPage"
          :total="pagedTotal"
          :items-per-page="PAGE_SIZE"
          :sibling-count="1"
          :disabled="isRestoring"
          size="sm"
        />
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { getLocalTimeZone, today } from "@internationalized/date";
import { useQuery } from "@pinia/colada";
import { watchDebounced } from "@vueuse/core";
import { computed, ref, watch } from "vue";

import type { FileResult } from "@/api/file";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import EmptyStatePanel from "@/components/common/EmptyStatePanel.vue";
import GlassEmptyState from "@/components/common/GlassEmptyState.vue";
import TrashGhost from "@/components/dashboard/trash/TrashGhost.vue";
import TrashRow from "@/components/dashboard/trash/TrashRow.vue";
import { useAppToast } from "@/composables/useAppToast";
import { SortBy } from "@/enums/SortBy";
import { SortDirection } from "@/enums/SortDirection";
import { describeExplorerItems } from "@/features/toast/format";
import { restoreDirectories } from "@/mutations/directories";
import { restoreFiles } from "@/mutations/files";
import { searchDirectory } from "@/queries/directories";
import { searchFile } from "@/queries/files";
import { formatTimeAgo } from "@/utils/date-formatters";
import { getFileIcon } from "@/utils/files/icons";
import { formatBytes } from "@/utils/size";

const DEFAULT_DAYS = 30;
const PAGE_SIZE = 20;

const DAYS_FILTER_OPTIONS = [
  { label: "Last 7 days", value: 7 },
  { label: "Last 14 days", value: 14 },
  { label: "Last 30 days", value: DEFAULT_DAYS },
];

const GROUP_HEADING_CLASS =
  "px-4 pb-2 pt-4 text-xs font-medium uppercase tracking-wider text-gray-600 dark:text-gray-400";

const toast = useAppToast("trash");

const searchQuery = ref("");
const debouncedSearch = ref("");
const daysFilter = ref(DEFAULT_DAYS);
const currentPage = ref(1);
const selectedFiles = ref(new Set<string>());
const selectedDirectories = ref(new Set<string>());

const deletedAfter = computed(() =>
  today(getLocalTimeZone()).subtract({ days: daysFilter.value }).toString(),
);

const nameContains = computed(() => debouncedSearch.value.trim() || null);

// Both endpoints page independently with the same index, so page N shows the
// Nth slice of folders and of files.
const filesQuery = useQuery(() => ({
  ...searchFile({
    createdAfter: null,
    createdBefore: null,
    currentPage: currentPage.value - 1,
    deletedAfter: deletedAfter.value,
    deletedBefore: null,
    directoryId: null,
    isDeleted: true,
    isShared: false,
    isStarred: false,
    maxSize: null,
    mimeType: null,
    minSize: null,
    nameContains: nameContains.value ?? undefined,
    onlyDeleted: true,
    ownerId: null,
    pageSize: PAGE_SIZE,
    parentDirectoryId: null,
    sortBy: SortBy.UpdatedAt,
    sortDirection: SortDirection.Desc,
    updatedAfter: null,
    updatedBefore: null,
  }),
  placeholderData: (prev) => prev,
}));

const directoriesQuery = useQuery(() => ({
  ...searchDirectory({
    deletedAfter: deletedAfter.value,
    isDeleted: true,
    nameContains: nameContains.value,
    page: currentPage.value - 1,
    pageSize: PAGE_SIZE,
    sortBy: SortBy.UpdatedAt,
    sortDirection: SortDirection.Desc,
  }),
  placeholderData: (prev) => prev,
}));

const { mutateAsync: restoreFilesAsync, isLoading: isRestoringFiles } = restoreFiles();
const { mutateAsync: restoreDirectoriesAsync, isLoading: isRestoringDirectories } =
  restoreDirectories();

const files = computed(() => filesQuery.data.value?.items ?? []);
const directories = computed(() => directoriesQuery.data.value?.items ?? []);
const fileTotal = computed(() => filesQuery.data.value?.totalCount ?? 0);
const directoryTotal = computed(() => directoriesQuery.data.value?.totalCount ?? 0);
const totalCount = computed(() => fileTotal.value + directoryTotal.value);

const pagedTotal = computed(() => Math.max(fileTotal.value, directoryTotal.value));
const pageCount = computed(() => Math.ceil(pagedTotal.value / PAGE_SIZE));

const isPending = computed(() => filesQuery.isPending.value || directoriesQuery.isPending.value);
const isFetching = computed(() => filesQuery.isLoading.value || directoriesQuery.isLoading.value);
const isRestoring = computed(() => isRestoringFiles.value || isRestoringDirectories.value);

const visibleError = computed(() => {
  const err = filesQuery.error.value ?? directoriesQuery.error.value;

  if (!err || isAuthError(err)) return null;

  return err;
});

const hasResults = computed(() => files.value.length > 0 || directories.value.length > 0);

const isEmpty = computed(() => !isPending.value && !visibleError.value && !hasResults.value);

const selectedCount = computed(() => selectedFiles.value.size + selectedDirectories.value.size);
const visibleCount = computed(() => files.value.length + directories.value.length);

const masterState = computed<boolean | "indeterminate">(() => {
  if (selectedCount.value === 0) return false;

  if (selectedCount.value === visibleCount.value) return true;

  return "indeterminate";
});

const selectionLabel = computed(() => {
  if (selectedCount.value > 0) return `${selectedCount.value} selected`;

  return "Select all";
});

const emptyState = computed(() => {
  const lookedBack = `in the last ${daysFilter.value} days`;

  if (nameContains.value) {
    return {
      action: {
        label: "Clear search",
        run: () => {
          searchQuery.value = "";
        },
      },
      icon: "i-lucide-search",
      message: `Nothing matching "${nameContains.value}" was deleted ${lookedBack}.`,
      title: "No matches",
    };
  }

  if (daysFilter.value !== DEFAULT_DAYS) {
    return {
      action: {
        label: `Look back ${DEFAULT_DAYS} days`,
        run: () => {
          daysFilter.value = DEFAULT_DAYS;
        },
      },
      icon: "i-lucide-calendar",
      message: `Nothing has been deleted ${lookedBack}.`,
      title: "No deleted items",
    };
  }

  return {
    action: null,
    icon: "i-lucide-trash-2",
    message: `Nothing has been deleted ${lookedBack}. Deleted items show up here so you can restore them.`,
    title: "Trash is empty",
  };
});

const deletedLabel = (file: FileResult) => {
  if (!file.deletedAt) return null;

  return `Deleted ${formatTimeAgo(file.deletedAt)}`;
};

const toggleIn = (set: Set<string>, id: string) => {
  if (set.has(id)) {
    set.delete(id);
  } else {
    set.add(id);
  }
};

const clearSelection = () => {
  selectedFiles.value.clear();
  selectedDirectories.value.clear();
};

const toggleSelectAll = () => {
  if (selectedCount.value === visibleCount.value) {
    clearSelection();
    return;
  }

  selectedDirectories.value = new Set(directories.value.map((d) => d.id));
  selectedFiles.value = new Set(files.value.map((f) => f.fileId));
};

const refresh = () => Promise.all([filesQuery.refetch(), directoriesQuery.refetch()]);

const restoreSelected = async () => {
  const fileIds = [...selectedFiles.value];
  const directoryIds = [...selectedDirectories.value];

  const results = await Promise.allSettled([
    fileIds.length > 0 ? restoreFilesAsync(fileIds) : Promise.resolve(),
    directoryIds.length > 0 ? restoreDirectoriesAsync(directoryIds) : Promise.resolve(),
  ]);

  const [filesResult, directoriesResult] = results;
  const filesFailed = filesResult?.status === "rejected";
  const directoriesFailed = directoriesResult?.status === "rejected";
  const firstFailure = results.find((result) => result.status === "rejected");

  clearSelection();

  // Restoring a folder restores its files too, and each mutation only
  // invalidates its own domain, so both lists refresh here.
  await refresh();

  const restored = describeExplorerItems(
    filesFailed ? 0 : fileIds.length,
    directoriesFailed ? 0 : directoryIds.length,
  );
  const notRestored = describeExplorerItems(
    filesFailed ? fileIds.length : 0,
    directoriesFailed ? directoryIds.length : 0,
  );
  const restoredAny =
    (!filesFailed && fileIds.length > 0) || (!directoriesFailed && directoryIds.length > 0);

  if (restoredAny) {
    toast.success(`Restored ${restored}`, "They're back where they were before deletion.");
  }

  if (firstFailure) {
    toast.error(`Couldn't restore ${notRestored}`, firstFailure.reason);
  }
};

watchDebounced(
  searchQuery,
  (value) => {
    debouncedSearch.value = value;
  },
  { debounce: 300 },
);

watch([debouncedSearch, daysFilter], () => {
  currentPage.value = 1;
});

// Selection is page-scoped: a restore must never reach rows the user can no longer see.
watch([currentPage, debouncedSearch, daysFilter], clearSelection);
</script>
