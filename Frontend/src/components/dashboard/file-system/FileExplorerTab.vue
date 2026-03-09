<template>
  <div class="flex flex-col h-full w-full flex-1" @click="handleContainerClick">
    <!-- Toolbar -->
    <div class="flex flex-col w-full border-b border-b-primary">
      <div class="flex items-center gap-2 p-3 w-full">
        <div class="flex border border-primary rounded-md overflow-hidden shrink-0">
          <UButton
            color="primary"
            size="sm"
            class="rounded-none border-r dark:border-black"
            @click="handleFileUpload(selectedUploadType.label as 'File' | 'Directory' | 'Archive')"
          >
            <Icon :icon="selectedUploadType.icon" class="w-4 h-4 md:mr-2" />
            <span class="hidden md:inline">Upload {{ selectedUploadType.label }}</span>
            <span class="md:hidden">Upload</span>
          </UButton>
          <UDropdownMenu :items="uploadOptions" :ui="{ content: 'w-48' }">
            <UButton
              color="primary"
              size="sm"
              class="rounded-none px-2"
              aria-label="Upload options"
            >
              <Icon icon="mdi:chevron-down" class="w-4 h-4" />
            </UButton>
          </UDropdownMenu>
        </div>

        <!-- New folder -->
        <UButton color="primary" size="sm" class="shrink-0" @click="createNewDirectory">
          <Icon icon="mdi:folder-plus" class="w-4 h-4 md:mr-1" />
          <span class="hidden md:inline">New Folder</span>
        </UButton>

        <div class="flex-1" />

        <div
          class="hidden md:flex items-center gap-1.5 text-xs text-muted select-none"
          aria-live="polite"
        >

          <Transition name="throbber">
            <BlocksSpinner
              v-if="isBackgroundLoading"
              :size="14"
              aria-label="Refreshing"
              class="opacity-50"
            />
          </Transition>

          <Transition name="fade-status">
            <span
              v-if="lastRefreshedLabel && !isBackgroundLoading"
              class="opacity-50 tabular-nums"
              :title="lastRefreshedAt?.toLocaleTimeString()"
            >
              {{ lastRefreshedLabel }}
            </span>
          </Transition>
        </div>

        <div class="hidden md:flex items-center gap-1">
          <USelectMenu
            v-model="selectedSortBy"
            :items="sortByOptions"
            size="sm"
            placeholder="Sort by"
            @update:model-value="handleSorting"
          >
            <template #default="{ modelValue }">
              <Icon icon="mdi:sort" class="w-4 h-4 mr-1" />
              <span>{{ modelValue?.label }}</span>
            </template>
          </USelectMenu>

          <UButton
            size="sm"
            variant="ghost"
            @click="toggleSortDirection"
            :aria-label="selectedSortDirection === SortDirection.Asc ? 'Ascending' : 'Descending'"
          >
            <Icon
              :icon="
                selectedSortDirection === SortDirection.Asc
                  ? 'mdi:sort-ascending'
                  : 'mdi:sort-descending'
              "
              class="w-4 h-4"
            />
          </UButton>

          <div class="w-px h-5 bg-gray-200 dark:bg-gray-700 mx-1" />

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

        <!-- Search — always visible, right-most -->
        <UButton
          @click="quickSearch"
          icon="mdi:magnify"
          size="sm"
          variant="ghost"
          class="shrink-0"
          aria-label="Search"
        />
      </div>

      <div class="flex md:hidden items-center gap-1 px-3 pb-2">
        
          <Transition name="throbber">
          <BlocksSpinner
            v-if="isBackgroundLoading"
            :size="14"
            aria-label="Refreshing"
            class="opacity-50 mr-1 shrink-0"
          />
        </Transition>

        <USelectMenu
          v-model="selectedSortBy"
          :items="sortByOptions"
          size="sm"
          placeholder="Sort by"
          class="flex-1 min-w-0"
          @update:model-value="handleSorting"
        >
          <template #default="{ modelValue }">
            <Icon icon="mdi:sort" class="w-4 h-4 mr-1 shrink-0" />
            <span class="truncate">{{ modelValue?.label }}</span>
          </template>
        </USelectMenu>

        <UButton
          size="sm"
          variant="ghost"
          @click="toggleSortDirection"
          :aria-label="selectedSortDirection === SortDirection.Asc ? 'Ascending' : 'Descending'"
        >
          <Icon
            :icon="
              selectedSortDirection === SortDirection.Asc
                ? 'mdi:sort-ascending'
                : 'mdi:sort-descending'
            "
            class="w-4 h-4"
          />
        </UButton>

        <div class="w-px h-5 bg-gray-200 dark:bg-gray-700 mx-1 shrink-0" />

        <div class="flex rounded-md overflow-hidden border border-gray-200 dark:border-gray-700">
          <UButton
            :variant="viewMode === 'grid' ? 'solid' : 'ghost'"
            size="sm"
            class="rounded-none"
            @click="viewMode = 'grid'"
            aria-label="Grid view"
          >
            <Icon icon="mdi:view-grid" class="w-4 h-4" />
          </UButton>
          <UButton
            :variant="viewMode === 'list' ? 'solid' : 'ghost'"
            size="sm"
            class="rounded-none border-l border-gray-200 dark:border-gray-700"
            @click="viewMode = 'list'"
            aria-label="List view"
          >
            <Icon icon="mdi:view-list" class="w-4 h-4" />
          </UButton>
        </div>
      </div>
    </div>

    <div class="flex items-center gap-1 px-4 py-2">
      <UButton
        size="sm"
        variant="ghost"
        :disabled="!canGoBack"
        :class="!canGoBack ? 'opacity-30 cursor-not-allowed' : 'cursor-pointer'"
        :title="canGoBack ? 'Go back' : 'No previous location'"
        @click="navigateBack()"
        aria-label="Back"
      >
        <Icon icon="mdi:arrow-left" class="w-4 h-4" />
      </UButton>
      <UButton
        size="sm"
        variant="ghost"
        :disabled="!canGoForward"
        :class="!canGoForward ? 'opacity-30 cursor-not-allowed' : 'cursor-pointer'"
        :title="canGoForward ? 'Go forward' : 'No next location'"
        @click="navigateForward()"
        aria-label="Forward"
      >
        <Icon icon="mdi:arrow-right" class="w-4 h-4" />
      </UButton>

      <div class="w-px h-4 bg-gray-200 dark:bg-gray-700 mx-1" />

      <UBreadcrumb :items="breadcrumbs">
        <template #item="{ item }">
          <UButton
            :icon="item.icon"
            :label="item.label"
            color="neutral"
            variant="link"
            class="p-0.5"
            @click="handleNavigate(item.key)"
          />
        </template>
        <template #separator>
          <span class="mx-2 text-muted">/</span>
        </template>
      </UBreadcrumb>
    </div>

    <!-- Content Area -->
    <div ref="containerRef" class="flex-1 overflow-auto relative">
      <!-- Drop Zone Overlay -->
      <Transition name="dropzone">
        <div
          v-if="isOverDropZone"
          class="absolute inset-0 z-50 flex items-center justify-center pointer-events-none"
          aria-hidden="true"
        >
          <div class="absolute inset-0 bg-background/60 backdrop-blur-[2px]" />
          <div class="absolute inset-0 bg-primary/5 pulse-tint" />
          <div
            class="absolute inset-3 rounded-xl border-2 border-dashed border-primary/25 pulse-border"
          />
          <div
            class="relative flex flex-col items-center gap-3 px-8 py-6 rounded-xl border border-primary/20 bg-background/80 shadow-sm"
          >
            <div class="relative flex items-center justify-center">
              <span class="breathe absolute rounded-full border border-primary/20" />
              <div class="relative z-10 p-3 rounded-full bg-primary/8">
                <Icon :icon="dropIcon" class="w-8 h-8 text-primary/70" />
              </div>
            </div>
            <div class="text-center space-y-0.5">
              <p class="font-medium text-base text-primary/80 tracking-tight">{{ dropLabel }}</p>
              <p class="text-xs text-muted">Release to upload</p>
            </div>
          </div>
        </div>
      </Transition>

      <!-- Grid View -->
      <div v-if="viewMode === 'grid'" class="p-4">
        <!-- Show skeleton only on true initial load (no data yet) -->
        <GridPlaceholder v-if="showDirSkeleton" />
        <div v-else-if="directoriesList.length > 0" class="mb-6 flex flex-col">
          <div>
            <div class="flex items-center gap-2 ml-2 pb-4">
              <h3 class="font-semibold opacity-70 px-1">Folders</h3>
            </div>
          </div>
          <div class="grid gap-3" :class="gridColumns">
            <DirectoryItem
              v-for="dir in directoriesList"
              :key="dir.id"
              :data="dir"
              :view-mode="viewMode"
              :is-selected="isDirectorySelected(dir.id)"
              @navigate="handleNavigate"
              @open="handleNavigate"
              @rename="handleDirectoryRename"
              @move="handleCut"
              @click="handleItemClick($event, dir.id, 'directory')"
              @copy="handleCopy"
              @delete="handleDelete"
              @contextmenu="handleItemClick($event, dir.id, 'directory')"
            />
          </div>
          <UButton
            variant="ghost"
            label="Show more"
            class="max-w-fit self-end"
            v-if="directoriesData?.hasNext"
            @click="loadMoreDirs"
          />
        </div>

        <GridPlaceholder v-if="showFileSkeleton" />
        <div v-else-if="filesList.length > 0" class="mb-6 flex flex-col flex-1">
          <h3 class="text-sm font-semibold opacity-70 mb-3 px-1">Files</h3>
          <div class="grid gap-3" :class="gridColumns">
            <FileItem
              v-for="file in filesList"
              :key="file.fileId"
              :tags="tagsData?.items"
              :data="file"
              :view-mode="viewMode"
              :is-selected="isFileSelected(file.fileId)"
              @download="downloadFile(file.fileId, file.fileName)"
              @click="handleItemClick($event, file.fileId, 'file')"
              @copy="handleCopy"
              @delete="handleDelete"
              @move="handleCut"
              @contextmenu="handleItemClick($event, file.fileId, 'file')"
              @file-trashed="refreshDir"
            />
          </div>
          <UButton
            variant="ghost"
            label="Show more"
            class="max-w-fit self-end"
            v-if="filesData?.hasNext"
            @click="loadMoreFiles"
          />
        </div>
      </div>

      <!-- List View -->
      <div v-else class="flex flex-col">
        <ListPlaceholder v-if="showDirSkeleton" />
        <div v-else-if="directoriesList.length > 0">
          <h3 class="text-sm font-semibold opacity-70 mb-2 px-4 pt-4">Folders</h3>
          <DirectoryItem
            v-for="dir in directoriesList"
            :key="dir.id"
            :data="dir"
            :view-mode="viewMode"
            :is-selected="isDirectorySelected(dir.id)"
            @navigate="handleNavigate"
            @open="handleNavigate"
            @rename="handleDirectoryRename"
            @move="handleCut"
            @click="handleItemClick($event, dir.id, 'directory')"
            @copy="handleCopy"
            @delete="handleDelete"
          />
          <UButton
            variant="ghost"
            label="Show more"
            class="max-w-fit self-end"
            v-if="directoriesData?.hasNext"
            @click="loadMoreDirs"
          />
        </div>

        <ListPlaceholder v-if="showFileSkeleton" />
        <div
          v-else-if="filesList.length > 0"
          :class="{ 'mt-4': directoriesData?.items.length ?? 0 > 0 }"
        >
          <h3
            class="text-sm font-semibold opacity-70 mb-2 px-4"
            :class="{ 'pt-4': directoriesData?.items.length ?? 0 === 0 }"
          >
            Files
          </h3>
          <FileItem
            v-for="file in filesList"
            :key="file.fileId"
            :data="file"
            :view-mode="viewMode"
            :tags="tagsData?.items"
            :is-selected="isFileSelected(file.fileId)"
            @download="downloadFile(file.fileId, file.fileName)"
            @click="handleItemClick($event, file.fileId, 'file')"
            @copy="handleCopy"
            @delete="handleDelete"
            @move="handleCut"
            @file-trashed="refreshDir"
          />
          <UButton
            variant="ghost"
            label="Show more"
            class="max-w-fit self-end"
            v-if="filesData?.hasNext"
            @click="loadMoreFiles"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import FileItem from "./FileItem.vue";
import DirectoryItem from "./DirectoryItem.vue";
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { Icon } from "@iconify/vue";
import { SortBy } from "@/enums/SortBy";
import { useFileExplorer } from "@/composables/useFileExplorer";
import { useTabStore } from "@/stores/tab";
import type { BreadcrumbItem, DropdownMenuItem } from "@nuxt/ui";
import { SortDirection } from "@/enums/SortDirection";
import CreateDirectoryModal from "./Modals/CreateDirectoryModal.vue";
import UpdateDirectoryModal from "./Modals/UpdateDirectoryModal.vue";
import FileUploadModal from "./Modals/FileUploadModal.vue";
import DirectoryUploadModal from "./Modals/DirectoryUploadModal.vue";
import { useFileStore } from "@/stores/file";
import { useDirectoryStore } from "@/stores/directory";
import { useSettingsStore } from "@/stores/settings";
import { copyFiles, deleteFiles, moveFiles } from "@/mutations/files";
import { copyDirectory, deleteDirectory, moveDirectories } from "@/mutations/directories";
import type { SearchTagsSchema } from "@/schemas/tag";
import { searchTag } from "@/queries/tags";
import { useQuery } from "@pinia/colada";
import AdvancedSearchModal from "./Modals/AdvancedSearchModal.vue";
import QuickSearchModal from "./Modals/QuickSearchModal.vue";
import ConfirmModal from "@/components/dashboard/ConfirmModal.vue";
import { useDropZone } from "@vueuse/core";
import BlocksSpinner from "@/components/common/BlockSpinner.vue";

const fileStore = useFileStore();
const directoryStore = useDirectoryStore();
const settingsStore = useSettingsStore();
const tabStore = useTabStore();

const props = defineProps<{
  tabId: string;
}>();

const toast = useToast();

const {
  currentDirId,
  directoriesQuery,
  filesQuery,
  directoriesList,
  filesList,
  viewMode,
  filePagination,
  dirPagination,
  lastSelected,
  selectedDirectories,
  selectedFiles,
  pathQuery,
  loadMoreDirs,
  loadMoreFiles,
  navigateTo,
  canGoBack,
  canGoForward,
  navigateBack,
  navigateForward,
  refreshDir,
  toggleSelect,
  isDirectorySelected,
  isFileSelected,
  clearSelection,
  selectRange,
  downloadFile,
} = useFileExplorer();

const { mutateAsync: copyFilesMutate } = copyFiles();
const { mutateAsync: copyDirectoryMutate } = copyDirectory();
const { mutateAsync: moveFilesMutate } = moveFiles();
const { mutateAsync: moveDirectoriesMutate } = moveDirectories();
const { mutateAsync: deleteFilesMutate } = deleteFiles();
const { mutateAsync: deleteDirectoryMutate } = deleteDirectory();

const { data: directoriesData, isLoading: areDirectoriesLoading } = directoriesQuery;
const { data: filesData, isLoading: areFilesLoading } = filesQuery;

// Initial-load tracking
// True until the *first* successful data fetch for this tab instance.
// Skeletons only render while this is true.
const dirHasLoaded = ref(false);
const fileHasLoaded = ref(false);

watch(
  directoriesData,
  (val) => {
    if (val !== undefined) dirHasLoaded.value = true;
  },
  { immediate: true },
);

watch(
  filesData,
  (val) => {
    if (val !== undefined) fileHasLoaded.value = true;
  },
  { immediate: true },
);

// Show skeleton only when loading AND data has never arrived yet
const showDirSkeleton = computed(() => areDirectoriesLoading.value && !dirHasLoaded.value);
const showFileSkeleton = computed(() => areFilesLoading.value && !fileHasLoaded.value);

// Background loading: loading but already have data → throbber, not skeleton
const isBackgroundLoading = computed(
  () =>
    (areDirectoriesLoading.value && dirHasLoaded.value) ||
    (areFilesLoading.value && fileHasLoaded.value),
);

// Last-refreshed timer
const lastRefreshedAt = ref<Date | null>(null);
const lastRefreshedLabel = ref<string>("");
let labelTimer: ReturnType<typeof setInterval> | null = null;

const formatRelative = (date: Date): string => {
  const secs = Math.floor((Date.now() - date.getTime()) / 1000);
  if (secs < 5) return "just now";
  if (secs < 60) return `${secs}s ago`;
  const mins = Math.floor(secs / 60);
  if (mins < 60) return `${mins}m ago`;
  return `${Math.floor(mins / 60)}h ago`;
};

const startLabelTimer = (date: Date) => {
  if (labelTimer) clearInterval(labelTimer);
  lastRefreshedAt.value = date;
  lastRefreshedLabel.value = formatRelative(date);
  labelTimer = setInterval(() => {
    lastRefreshedLabel.value = formatRelative(date);
  }, 10_000);
};

// Record time when a background refresh completes (loading → false, data exists)
watch(areDirectoriesLoading, (loading) => {
  if (!loading && dirHasLoaded.value) startLabelTimer(new Date());
});

// Tag query
const tagCurrentPage = ref(1);
const tagPageSize = ref(25);

const searchFilters = computed<SearchTagsSchema>(() => ({
  page: tagCurrentPage.value,
  pageSize: tagPageSize.value,
}));

const { data: tagsData } = useQuery(searchTag(searchFilters.value));

// Drop zone
const containerRef = ref<HTMLElement | null>(null);
const dragHasDirectory = ref(false);

const dropIcon = computed(() =>
  dragHasDirectory.value ? "mdi:folder-upload-outline" : "mdi:cloud-upload-outline",
);
const dropLabel = computed(() => (dragHasDirectory.value ? "Drop folder here" : "Drop files here"));

const { isOverDropZone } = useDropZone(containerRef, {
  onEnter(_files, event) {
    const items = Array.from(event.dataTransfer?.items ?? []);
    dragHasDirectory.value = items.some((item) => {
      const entry = (item as DataTransferItem).webkitGetAsEntry?.();
      return entry?.isDirectory ?? false;
    });
  },

  onLeave() {
    dragHasDirectory.value = false;
  },

  async onDrop(_files, event) {
    dragHasDirectory.value = false;

    const items = Array.from(event.dataTransfer?.items ?? []);
    if (items.length === 0) return;

    const entries = items
      .map((item) => item.webkitGetAsEntry?.())
      .filter((e): e is FileSystemEntry => e !== null && e !== undefined);

    if (entries.length === 0) return;

    const hasDirectory = entries.some((e) => e.isDirectory);

    let instance;
    if (hasDirectory) {
      const allFiles = (await Promise.all(entries.map((e) => readEntryRecursive(e)))).flat();
      instance = directoryUploadModal.open({
        directoryId: currentDirId.value ?? undefined,
        directoryName: currentDirName.value,
        droppedFiles: allFiles,
      });
    } else {
      instance = fileUploadModal.open({
        directoryId: currentDirId.value ?? undefined,
        directoryName: currentDirName.value,
        droppedFile: (_files ?? [])[0] ?? undefined,
      });
    }

    const shouldRefresh = await instance.result;
    if (shouldRefresh && settingsStore.toastLevel === "all") {
      refreshDir();
      toast.add({
        color: "success",
        id: "dropzone-upload-success",
        title: "Upload complete",
      });
    }
  },
});

// FileSystem API helpers
export interface DroppedFile {
  file: File;
  relativePath: string;
}

async function readEntryRecursive(entry: FileSystemEntry, path = ""): Promise<DroppedFile[]> {
  if (entry.isFile) {
    return new Promise((resolve, reject) => {
      (entry as FileSystemFileEntry).file(
        (file) => resolve([{ file, relativePath: path + file.name }]),
        reject,
      );
    });
  }

  if (entry.isDirectory) {
    const dirEntry = entry as FileSystemDirectoryEntry;
    const children = await readAllEntries(dirEntry.createReader());
    const nested = await Promise.all(
      children.map((child) => readEntryRecursive(child, `${path}${entry.name}/`)),
    );
    return nested.flat();
  }

  return [];
}

function readAllEntries(reader: FileSystemDirectoryReader): Promise<FileSystemEntry[]> {
  return new Promise((resolve, reject) => {
    const collected: FileSystemEntry[] = [];
    const readBatch = () => {
      reader.readEntries((batch) => {
        if (batch.length === 0) resolve(collected);
        else {
          collected.push(...batch);
          readBatch();
        }
      }, reject);
    };
    readBatch();
  });
}

// Shortcuts
let copyMode = true;

defineShortcuts({
  Delete: () => handleDelete(),
  alt_arrowleft: () => {
    if (canGoBack.value) navigateBack();
  },
  alt_arrowright: () => {
    if (canGoForward.value) navigateForward();
  },
  "meta_/": () => quickSearch(),
  meta_c: () => {
    copyMode = true;
    handleCopy();
  },
  meta_v: () => {
    if (copyMode) handlePaste();
    else handleCut();
  },
  meta_x: () => {
    copyMode = false;
    handleCopy();
  },
  shift_k: () => quickSearch(),
  shift_l: () => advancedSearch(),
});

// Sort
const sortByOptions = ref([
  { label: "Name", value: SortBy.Name },
  { label: "Date Created", value: SortBy.CreatedAt },
  { label: "Date Modified", value: SortBy.UpdatedAt },
]);

const selectedUploadType = ref({ icon: "mdi:file-outline", label: "File" });

const uploadOptions = ref([
  { icon: "mdi:file-outline", label: "File", onSelect: () => handleFileUpload("File") },
  { icon: "mdi:folder-outline", label: "Directory", onSelect: () => handleFileUpload("Directory") },
  { icon: "formkit:zip", label: "Archive", onSelect: () => handleFileUpload("Archive") },
] satisfies DropdownMenuItem[]);

const selectedSortBy = ref({ label: "Name", value: SortBy.Name });
const selectedSortDirection = ref<SortDirection>(SortDirection.Asc);

const toggleSortDirection = () => {
  selectedSortDirection.value =
    selectedSortDirection.value === SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc;

  filePagination.value.paginationParams.sortDirection = selectedSortDirection.value;
  dirPagination.value.paginationParams.sortDirection = selectedSortDirection.value;
  refreshDir();
};

// Modals
const overlay = useOverlay();
const createDirectoryModal = overlay.create(CreateDirectoryModal);
const updateDirectoryModal = overlay.create(UpdateDirectoryModal);
const fileUploadModal = overlay.create(FileUploadModal);
const directoryUploadModal = overlay.create(DirectoryUploadModal);
const advancedSearchModal = overlay.create(AdvancedSearchModal);
const quickSearchModal = overlay.create(QuickSearchModal);
const confirmModal = overlay.create(ConfirmModal);

const advancedSearch = async () => {
  const instance = advancedSearchModal.open();
  const result = await instance.result;
  if (result === "close") return;
  else if (result === "root") handleNavigate(null);
  else if (typeof result === "string") handleNavigate(result);
};

const quickSearch = async () => {
  const instance = quickSearchModal.open();
  const result = await instance.result;
  if (result === "close") return;
  else if (result === "root") handleNavigate(null);
  else if (result === "advanced") advancedSearch();
  else if (typeof result === "string") handleNavigate(result);
};

const handleContainerClick = (event: MouseEvent) => {
  const target = event.target as HTMLElement;
  if (!target.closest("button")) clearSelection();
};

const gridColumns = computed(
  () => "grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 2xl:grid-cols-8",
);

const handleDirectoryRename = async (directoryId: string) => {
  const instance = updateDirectoryModal.open({ directoryId });
  const shouldRefresh = await instance.result;
  if (shouldRefresh && settingsStore.toastLevel === "all") {
    toast.add({ color: "success", id: "modal-success", title: "Directory updated successfully" });
    refreshDir();
    return;
  }
  if (!shouldRefresh && settingsStore.toastLevel !== "silent") {
    toast.add({ color: "error", id: "modal-error", title: "Directory update failed" });
  }
};

const handleCopy = async () => {
  fileStore.filesToCopy = [...selectedFiles.value];
  directoryStore.directoriesToCopy = [...selectedDirectories.value];
  if (settingsStore.toastLevel === "all") {
    toast.add({ color: "info", id: "copying", title: "Items selected" });
  }
};

const handleDelete = async () => {
  if (selectedFiles.value.size > 0) {
    await deleteFilesMutate({ ids: [...selectedFiles.value] });
  }

  if (selectedDirectories.value.size > 0) {
    const dirIds = Array.from(selectedDirectories.value);

    const results = await Promise.allSettled(
      dirIds.map((id) => deleteDirectoryMutate({ id, options: { force: false } })),
    );

    const failedDirs = results
      .map((result, index) => ({ id: dirIds[index], result }))
      .filter((x) => x.result.status === "rejected" && x.result.reason?.response?.status === 409)
      .map((x) => x.id);

    if (failedDirs.length > 0) {
      if (settingsStore.skipDeleteConfirmation) {
        await Promise.all(
          failedDirs.map((id) => deleteDirectoryMutate({ id, options: { force: true } })),
        );
      } else {
        const instance = confirmModal.open({
          alert: {
            color: "warning",
            icon: "i-lucide-triangle-alert",
            title: "All sub items will also be deleted",
          },
          confirmIcon: "i-lucide-trash-2",
          confirmLabel: "Delete anyway",
          dangerMode: true,
          description: `${failedDirs.length} ${failedDirs.length === 1 ? "directory" : "directories"} still contain files and will be deleted.`,
          title: "Delete directories?",
        });

        const confirmed = await instance.result;
        if (confirmed) {
          await Promise.all(
            failedDirs.map((id) => deleteDirectoryMutate({ id, options: { force: true } })),
          );
        }
      }
    }
  }
  if (settingsStore.toastLevel === "all") {
    toast.add({ color: "info", id: "deleting", title: "Items deleted" });
  }
  refreshDir();
};

const handleCut = async () => {
  if (fileStore.filesToCopy.length > 0) {
    await moveFilesMutate({ destinationId: currentDirId.value, fileIds: fileStore.filesToCopy });
  }
  if (directoryStore.directoriesToCopy.length > 0) {
    await moveDirectoriesMutate({
      destinationId: currentDirId.value,
      directoryIds: directoryStore.directoriesToCopy,
    });
  }
  refreshDir();
};

const handlePaste = async () => {
  if (fileStore.filesToCopy.length > 0) {
    await copyFilesMutate({ destinationId: currentDirId.value, fileIds: fileStore.filesToCopy });
  }

  if (directoryStore.directoriesToCopy.length > 0) {
    await Promise.all(
      directoryStore.directoriesToCopy.map(async (dir) => {
        await copyDirectoryMutate({ destinationId: currentDirId.value, directoryId: dir });
      }),
    );
  }

  refreshDir();
};

const handleSorting = () => {
  filePagination.value.paginationParams.SortBy = selectedSortBy.value.value;
  dirPagination.value.paginationParams.SortBy = selectedSortBy.value.value;
  refreshDir();
};

const currentDirName = computed<string | undefined>(() => {
  if (!currentDirId.value) return undefined;
  const parts = pathQuery.data.value?.pathParts;
  return parts?.[parts.length - 1]?.name;
});

const handleFileUpload = async (type: "File" | "Directory" | "Archive") => {
  const option = uploadOptions.value.find((opt) => opt.label === type);
  if (option) selectedUploadType.value = { icon: option.icon, label: option.label };

  const uploadProps = {
    directoryId: currentDirId.value ?? undefined,
    directoryName: currentDirName.value,
  };

  let instance;
  switch (type) {
    case "File":
      instance = fileUploadModal.open(uploadProps);
      break;
    case "Directory":
      instance = directoryUploadModal.open(uploadProps);
      break;
    case "Archive":
      instance = directoryUploadModal.open(uploadProps);
      break;
    default:
      instance = fileUploadModal.open(uploadProps);
  }

  const shouldRefresh = await instance.result;
  if (shouldRefresh) {
    refreshDir();
    return;
  }
  if (!shouldRefresh && directoryStore.error && settingsStore.toastLevel !== "silent") {
    toast.add({
      color: "error",
      description: directoryStore.error,
      id: "modal-error",
      title: "Upload failed",
    });
  }
};

const breadcrumbs = computed(() => {
  const items: BreadcrumbItem[] = [
    { icon: "i-heroicons-home", key: null, label: "Home", to: { name: "dashboard" } },
  ];

  const path = pathQuery.data.value?.pathParts;
  if (path && path.length > 0) {
    items.push(...path.map((segment) => ({ key: segment.id, label: segment.name })));
  }
  return items;
});

const createNewDirectory = async () => {
  const instance = createDirectoryModal.open({ parentId: currentDirId.value });

  const shouldRefresh = await instance.result;
  if (shouldRefresh && settingsStore.toastLevel === "all") {
    toast.add({ color: "success", id: "modal-success", title: "Directory creation successful" });
    refreshDir();
    return;
  }
  if (!shouldRefresh && directoryStore.error && settingsStore.toastLevel !== "silent") {
    toast.add({
      color: "error",
      description: directoryStore.error,
      id: "modal-error",
      title: "Directory creation failed",
    });
  }
};

const handleItemClick = (event: MouseEvent, id: string, type: "file" | "directory") => {
  const isCtrlOrCmd = event.ctrlKey || event.metaKey;
  const isShift = event.shiftKey;
  const isRightClick = event.button === 2;

  if (isRightClick) {
    if (!isFileSelected(id) && !isDirectorySelected(id)) {
      toggleSelect(id, type);
      lastSelected.value = id;
    }
    return;
  }

  if (isShift && lastSelected.value) {
    selectRange(lastSelected.value, id);
  } else if (isCtrlOrCmd) {
    toggleSelect(id, type);
    lastSelected.value = id;
  } else {
    clearSelection();
    toggleSelect(id, type);
    lastSelected.value = id;
  }
};

const handleNavigate = (dirId: string | null) => {
  // When navigating to a new directory, reset the "has loaded" flags so
  // skeletons appear for the new location's first fetch.
  dirHasLoaded.value = false;
  fileHasLoaded.value = false;

  navigateTo(dirId);
  tabStore.setActiveDir(props.tabId, dirId);
};

const handleMouseNavigate = (event: MouseEvent) => {
  if (event.button !== 3 && event.button !== 4) return;

  event.preventDefault();
  event.stopPropagation();

  if (event.button === 3 && canGoBack.value) navigateBack();
  if (event.button === 4 && canGoForward.value) navigateForward();
};

onMounted(async () => {
  containerRef.value?.addEventListener("mousedown", handleMouseNavigate);
  const tab = tabStore.getTab(props.tabId);
  navigateTo(tab?.activeDirId);
});

onUnmounted(() => {
  containerRef.value?.removeEventListener("mousedown", handleMouseNavigate);
  if (labelTimer) clearInterval(labelTimer);
});
</script>

<style scoped>
/* Throbber transition */
.throbber-enter-active,
.throbber-leave-active {
  transition:
    opacity 0.2s ease,
    transform 0.2s ease;
}
.throbber-enter-from,
.throbber-leave-to {
  opacity: 0;
  transform: scale(0.7);
}

/* Status label transition */
.fade-status-enter-active,
.fade-status-leave-active {
  transition: opacity 0.35s ease;
}
.fade-status-enter-from,
.fade-status-leave-to {
  opacity: 0;
}

/* Drop zone animations */
.breathe {
  width: 4rem;
  height: 4rem;
  animation: breathe 2.8s ease-in-out infinite;
}

@keyframes breathe {
  0%,
  100% {
    transform: scale(1);
    opacity: 0.5;
  }
  50% {
    transform: scale(1.18);
    opacity: 0.15;
  }
}

.pulse-tint {
  animation: pulse-tint 3s ease-in-out infinite;
}

@keyframes pulse-tint {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.35;
  }
}

.pulse-border {
  animation: pulse-border 3s ease-in-out infinite;
}

@keyframes pulse-border {
  0%,
  100% {
    opacity: 0.8;
  }
  50% {
    opacity: 0.2;
  }
}

.dropzone-enter-active {
  transition: opacity 0.2s ease;
}
.dropzone-leave-active {
  transition: opacity 0.15s ease;
}
.dropzone-enter-from,
.dropzone-leave-to {
  opacity: 0;
}
</style>
