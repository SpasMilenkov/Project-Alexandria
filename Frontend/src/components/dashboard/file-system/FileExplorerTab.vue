<template>
  <div class="flex flex-col h-full w-full flex-1" @click="handleContainerClick">
    <!-- Toolbar -->
    <div class="flex flex-col w-full border-b border-b-primary">
      <!-- Desktop toolbar -->
      <div class="hidden md:flex items-center gap-2 p-3 w-full">
        <!-- Upload split button -->
        <div class="flex border border-primary rounded-md overflow-hidden shrink-0">
          <UButton
            color="primary"
            size="sm"
            class="rounded-none border-r border-primary/40"
            @click="handleFileUpload(selectedUploadType.label as 'File' | 'Directory' | 'Archive')"
          >
            <Icon :icon="selectedUploadType.icon" class="w-4 h-4 mr-2" />
            Upload {{ selectedUploadType.label }}
          </UButton>
          <UDropdownMenu :items="uploadOptions" :ui="{ content: 'w-48' }">
            <UButton
              color="primary"
              variant="ghost"
              size="sm"
              class="rounded-none px-2"
              aria-label="Upload options"
            >
              <Icon icon="mdi:chevron-down" class="w-4 h-4" />
            </UButton>
          </UDropdownMenu>
        </div>

        <!-- New Folder — secondary -->
        <UButton
          variant="outline"
          color="neutral"
          size="sm"
          class="shrink-0"
          @click="createNewDirectory"
        >
          <Icon icon="mdi:folder-plus" class="w-4 h-4 mr-1" />
          New Folder
        </UButton>

        <div class="w-px h-5 bg-gray-200 dark:bg-gray-700 mx-1 shrink-0" />

        <!-- Sort + direction + view toggles cluster -->
        <div class="flex items-center gap-1 shrink-0">
          <USelectMenu
            v-model="selectedSortBy"
            :items="sortByOptions"
            size="sm"
            placeholder="Sort by"
            class="min-w-37"
            @update:model-value="handleSorting"
          >
            <template #default="{ modelValue }">
              <span>{{ modelValue?.label }}</span>
            </template>
          </USelectMenu>

          <UButton
            size="sm"
            variant="ghost"
            color="neutral"
            :title="
              selectedSortDirection === SortDirection.Asc
                ? 'Switch to descending'
                : 'Switch to ascending'
            "
            :aria-label="selectedSortDirection === SortDirection.Asc ? 'Ascending' : 'Descending'"
            @click="toggleSortDirection"
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

          <div class="w-px h-5 bg-gray-200 dark:bg-gray-700 mx-0.5" />

          <UButton
            :variant="viewMode === 'grid' ? 'solid' : 'ghost'"
            color="neutral"
            size="sm"
            aria-label="Grid view"
            @click="viewMode = 'grid'"
          >
            <Icon icon="mdi:view-grid" class="w-4 h-4" />
          </UButton>
          <UButton
            :variant="viewMode === 'list' ? 'solid' : 'ghost'"
            color="neutral"
            size="sm"
            aria-label="List view"
            @click="viewMode = 'list'"
          >
            <Icon icon="mdi:view-list" class="w-4 h-4" />
          </UButton>
        </div>

        <div class="flex-1" />

        <!-- Throbber + last-refreshed label -->
        <div class="flex items-center gap-1.5 text-xs select-none" aria-live="polite">
          <Transition name="throbber">
            <BlocksSpinner
              v-if="isBackgroundLoading"
              :size="14"
              aria-label="Refreshing"
              class="opacity-40"
            />
          </Transition>
          <Transition name="fade-status">
            <span
              v-if="lastRefreshedLabel && !isBackgroundLoading"
              class="opacity-40 tabular-nums"
              :title="lastRefreshedAt?.toLocaleTimeString()"
            >
              {{ lastRefreshedLabel }}
            </span>
          </Transition>
        </div>

        <!-- Search -->
        <UButton
          variant="ghost"
          color="neutral"
          size="sm"
          class="shrink-0"
          aria-label="Search"
          title="Quick search (⌘/)"
          @click="quickSearch"
        >
          <Icon icon="mdi:magnify" class="w-4 h-4" />
        </UButton>
      </div>

      <!-- Mobile toolbar — 3 controls only -->
      <div class="flex md:hidden items-center gap-2 px-3 py-2 w-full">
        <!-- Upload — opens action sheet -->
        <UButton
          color="primary"
          size="sm"
          class="shrink-0"
          aria-label="Upload"
          @click="isMobileUploadSheetOpen = true"
        >
          <Icon icon="mdi:upload" class="w-4 h-4 mr-1.5" />
          Upload
        </UButton>

        <UButton
          variant="outline"
          color="neutral"
          size="sm"
          class="shrink-0"
          aria-label="Search files"
          @click="quickSearch"
        >
          <Icon icon="mdi:magnify" class="w-4 h-4 mr-1.5" />
          Search
        </UButton>

        <div class="flex-1" />

        <UDropdownMenu :items="mobileOverflowItems" :ui="{ content: 'w-52' }">
          <UButton variant="ghost" color="neutral" size="sm" aria-label="More options">
            <Icon icon="mdi:dots-horizontal" class="w-5 h-5" />
          </UButton>
        </UDropdownMenu>
      </div>
    </div>

    <!-- mobile upload drawer -->
    <UDrawer
      v-model:open="isMobileUploadSheetOpen"
      direction="bottom"
      class="md:hidden"
      :ui="{
        content: 'rounded-t-2xl border-t border-gray-200/70 dark:border-gray-700/70',
      }"
    >
      <template #content>
        <div class="px-4 pt-2 pb-3">
          <p class="text-xs font-medium uppercase tracking-widest text-gray-400 dark:text-gray-600">
            Upload to {{ currentDirName ?? "Home" }}
          </p>
        </div>

        <ul
          class="px-2 space-y-1"
          :style="{ paddingBottom: 'max(1rem, env(safe-area-inset-bottom))' }"
        >
          <li>
            <button
              class="flex items-center gap-4 w-full px-4 py-3.5 rounded-xl min-h-14 transition-colors text-left hover:bg-gray-100/60 dark:hover:bg-gray-800/60 text-gray-800 dark:text-gray-100"
              @click="mobileUpload('File')"
            >
              <div
                class="flex items-center justify-center w-9 h-9 rounded-xl bg-primary/10 shrink-0"
              >
                <Icon icon="mdi:file-outline" class="w-5 h-5 text-primary" />
              </div>
              <div>
                <p class="text-sm font-medium">File</p>
                <p class="text-xs text-gray-400 dark:text-gray-500 mt-0.5">
                  Upload one or more files
                </p>
              </div>
            </button>
          </li>
          <li>
            <button
              class="flex items-center gap-4 w-full px-4 py-3.5 rounded-xl min-h-14 transition-colors text-left hover:bg-gray-100/60 dark:hover:bg-gray-800/60 text-gray-800 dark:text-gray-100"
              @click="mobileUpload('Directory')"
            >
              <div
                class="flex items-center justify-center w-9 h-9 rounded-xl bg-primary/10 shrink-0"
              >
                <Icon icon="mdi:folder-outline" class="w-5 h-5 text-primary" />
              </div>
              <div>
                <p class="text-sm font-medium">Folder</p>
                <p class="text-xs text-gray-400 dark:text-gray-500 mt-0.5">
                  Upload an entire folder
                </p>
              </div>
            </button>
          </li>
          <li>
            <button
              class="flex items-center gap-4 w-full px-4 py-3.5 rounded-xl min-h-14 transition-colors text-left hover:bg-gray-100/60 dark:hover:bg-gray-800/60 text-gray-800 dark:text-gray-100"
              @click="mobileUpload('Archive')"
            >
              <div
                class="flex items-center justify-center w-9 h-9 rounded-xl bg-primary/10 shrink-0"
              >
                <Icon icon="formkit:zip" class="w-5 h-5 text-primary" />
              </div>
              <div>
                <p class="text-sm font-medium">Archive</p>
                <p class="text-xs text-gray-400 dark:text-gray-500 mt-0.5">
                  Upload and extract a zip
                </p>
              </div>
            </button>
          </li>
        </ul>
      </template>
    </UDrawer>

    <!-- breadcrumb row -->
    <div class="flex items-center gap-1 px-4 py-1.5">
      <UButton
        size="xs"
        variant="ghost"
        color="neutral"
        :disabled="!canGoBack"
        :class="!canGoBack ? 'opacity-30 cursor-not-allowed' : 'cursor-pointer'"
        :title="canGoBack ? 'Go back' : 'No previous location'"
        aria-label="Back"
        @click="navigateBack()"
      >
        <Icon icon="mdi:arrow-left" class="w-4 h-4" />
      </UButton>
      <UButton
        size="xs"
        variant="ghost"
        color="neutral"
        :disabled="!canGoForward"
        :class="!canGoForward ? 'opacity-30 cursor-not-allowed' : 'cursor-pointer'"
        :title="canGoForward ? 'Go forward' : 'No next location'"
        aria-label="Forward"
        @click="navigateForward()"
      >
        <Icon icon="mdi:arrow-right" class="w-4 h-4" />
      </UButton>

      <div class="w-px h-4 bg-gray-200 dark:bg-gray-700 mx-1" />

      <BreadcrumbNavigation :items="breadcrumbs" @navigate="handleNavigate" />
    </div>

    <!-- mobile background refresh indicator -->
    <Transition name="fade-status">
      <div
        v-if="isBackgroundLoading"
        class="flex md:hidden items-center gap-1.5 px-4 py-1 text-xs text-gray-400 dark:text-gray-500 select-none"
        aria-live="polite"
      >
        <BlocksSpinner :size="11" aria-label="Refreshing" class="opacity-50" />
        <span class="opacity-50">Refreshing…</span>
      </div>
    </Transition>

    <!-- content area -->
    <div ref="containerRef" class="flex-1 overflow-auto relative">
      <!-- drop zone overlay -->
      <Transition name="dropzone">
        <div
          v-if="isOverDropZone"
          class="absolute inset-0 z-50 flex items-center justify-center pointer-events-none"
          aria-hidden="true"
        >
          <div class="absolute inset-0 bg-background/60 backdrop-blur-sm" />
          <div class="absolute inset-0 bg-primary/5 pulse-tint" />
          <div
            class="absolute inset-3 rounded-xl border-2 border-dashed border-primary/25 pulse-border"
          />
          <div
            class="relative flex flex-col items-center gap-3 px-8 py-6 rounded-xl border border-primary/20 bg-white/60 dark:bg-white/5 shadow-sm"
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

      <!-- grid view -->
      <div v-if="viewMode === 'grid'" class="p-4">
        <GridPlaceholder v-if="showDirSkeleton" />
        <div v-else-if="directoriesList.length > 0" class="mb-8 flex flex-col">
          <h3 class="text-xs font-medium uppercase tracking-widest text-gray-400 px-1 mb-2">
            Folders
          </h3>
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
          <div
            v-if="directoriesData?.hasNext"
            class="border-t border-gray-100/70 dark:border-gray-800/70 mt-3 pt-1"
          >
            <UButton
              variant="ghost"
              color="neutral"
              size="sm"
              label="Show more folders"
              class="w-full"
              @click="loadMoreDirs"
            />
          </div>
        </div>

        <GridPlaceholder v-if="showFileSkeleton" />
        <div v-else-if="filesList.length > 0" class="mb-6 flex flex-col flex-1">
          <h3 class="text-xs font-medium uppercase tracking-widest text-gray-400 px-1 mb-2 pt-2">
            Files
          </h3>
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
          <div
            v-if="filesData?.hasNext"
            class="border-t border-gray-100/70 dark:border-gray-800/70 mt-3 pt-1"
          >
            <UButton
              variant="ghost"
              color="neutral"
              size="sm"
              label="Show more files"
              class="w-full"
              @click="loadMoreFiles"
            />
          </div>
        </div>
      </div>

      <!-- list view -->
      <div v-else class="flex flex-col">
        <ListPlaceholder v-if="showDirSkeleton" />
        <div
          v-else-if="directoriesList.length > 0"
          class="divide-y divide-gray-100/50 dark:divide-gray-800/50"
        >
          <h3 class="text-xs font-medium uppercase tracking-widest text-gray-400 px-4 pt-4 pb-2">
            Folders
          </h3>
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
          <div
            v-if="directoriesData?.hasNext"
            class="border-t border-gray-100/70 dark:border-gray-800/70 mt-1 pt-1"
          >
            <UButton
              variant="ghost"
              color="neutral"
              size="sm"
              label="Show more folders"
              class="w-full"
              @click="loadMoreDirs"
            />
          </div>
        </div>

        <ListPlaceholder v-if="showFileSkeleton" />
        <div
          v-else-if="filesList.length > 0"
          class="divide-y divide-gray-100/50 dark:divide-gray-800/50"
          :class="{ 'mt-4': (directoriesData?.items?.length ?? 0) > 0 }"
        >
          <h3
            class="text-xs font-medium uppercase tracking-widest text-gray-400 px-4 pb-2"
            :class="(directoriesData?.items?.length ?? 0) === 0 ? 'pt-4' : 'pt-2'"
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
            @contextmenu="handleItemClick($event, file.fileId, 'file')"
          />
          <div
            v-if="filesData?.hasNext"
            class="border-t border-gray-100/70 dark:border-gray-800/70 mt-1 pt-1"
          >
            <UButton
              variant="ghost"
              color="neutral"
              size="sm"
              label="Show more files"
              class="w-full"
              @click="loadMoreFiles"
            />
          </div>
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
import ArchiveUploadModal from "./Modals/ArchiveUploadModal.vue";
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
import { logger } from "@/utils/logger";
import BreadcrumbNavigation from "./BreadcrumbNavigation.vue";

const fileStore = useFileStore();
const directoryStore = useDirectoryStore();
const settingsStore = useSettingsStore();
const tabStore = useTabStore();

const props = defineProps<{ tabId: string }>();

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

// skeleton tracking

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

const showDirSkeleton = computed(() => areDirectoriesLoading.value && !dirHasLoaded.value);
const showFileSkeleton = computed(() => areFilesLoading.value && !fileHasLoaded.value);

const isBackgroundLoading = computed(
  () =>
    (areDirectoriesLoading.value && dirHasLoaded.value) ||
    (areFilesLoading.value && fileHasLoaded.value),
);

// last-refreshed timer

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

watch(areDirectoriesLoading, (loading) => {
  if (!loading && dirHasLoaded.value) startLabelTimer(new Date());
});

// tab title sync

const currentDirName = computed<string | undefined>(() => {
  if (!currentDirId.value) return undefined;
  const parts = pathQuery.data.value?.pathParts;
  return parts?.[parts.length - 1]?.name;
});

watch(
  currentDirName,
  (name) => {
    tabStore.updateTabTitle(props.tabId, name ?? "Home");
  },
  { immediate: true },
);

// tags

const tagCurrentPage = ref(1);
const tagPageSize = ref(25);
const searchFilters = computed<SearchTagsSchema>(() => ({
  page: tagCurrentPage.value,
  pageSize: tagPageSize.value,
}));
const { data: tagsData } = useQuery(searchTag(searchFilters.value));

// drop zone

const containerRef = ref<HTMLElement | null>(null);
const dragHasDirectory = ref(false);

const dropIcon = computed(() =>
  dragHasDirectory.value ? "mdi:folder-upload-outline" : "mdi:cloud-upload-outline",
);
const dropLabel = computed(() => (dragHasDirectory.value ? "Drop folder here" : "Drop files here"));

const { isOverDropZone } = useDropZone(containerRef, {
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
      // pass all dropped files to the modal
      instance = fileUploadModal.open({
        directoryId: currentDirId.value ?? undefined,
        directoryName: currentDirName.value,
        droppedFiles: _files ?? [],
      });
    }

    const shouldRefresh = await instance.result;
    if (shouldRefresh && settingsStore.toastLevel === "all") {
      refreshDir();
      toast.add({ color: "success", id: "dropzone-upload-success", title: "Upload complete" });
    }
  },
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
});

export interface DroppedFile {
  file: File;
  relativePath: string;
}

const readEntryRecursive = async (entry: FileSystemEntry, path = ""): Promise<DroppedFile[]> => {
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
};

const readAllEntries = (reader: FileSystemDirectoryReader): Promise<FileSystemEntry[]> =>
  new Promise((resolve, reject) => {
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

// keyboard shortcuts

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

// sort & upload state

const sortByOptions = ref([
  { label: "Name", value: SortBy.Name },
  { label: "Date Created", value: SortBy.CreatedAt },
  { label: "Date Modified", value: SortBy.UpdatedAt },
]);

const selectedSortBy = ref({ label: "Name", value: SortBy.Name });
const selectedSortDirection = ref<SortDirection>(SortDirection.Asc);
const selectedUploadType = ref({ icon: "mdi:file-outline", label: "File" });

const uploadOptions = ref([
  { icon: "mdi:file-outline", label: "File", onSelect: () => handleFileUpload("File") },
  { icon: "mdi:folder-outline", label: "Directory", onSelect: () => handleFileUpload("Directory") },
  { icon: "formkit:zip", label: "Archive", onSelect: () => handleFileUpload("Archive") },
] satisfies DropdownMenuItem[]);

const toggleSortDirection = () => {
  selectedSortDirection.value =
    selectedSortDirection.value === SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc;
  filePagination.value.paginationParams.sortDirection = selectedSortDirection.value;
  dirPagination.value.paginationParams.sortDirection = selectedSortDirection.value;
  refreshDir();
};

const handleSorting = () => {
  filePagination.value.paginationParams.SortBy = selectedSortBy.value.value;
  dirPagination.value.paginationParams.SortBy = selectedSortBy.value.value;
  refreshDir();
};

// mobile upload sheet

const isMobileUploadSheetOpen = ref(false);

const mobileUpload = (type: "File" | "Directory" | "Archive") => {
  isMobileUploadSheetOpen.value = false;
  handleFileUpload(type);
};

const mobileOverflowItems = computed(() => [
  { label: "Sort by", type: "label" as const },
  {
    icon: selectedSortBy.value.value === SortBy.Name ? "mdi:check" : "",
    label: "Name",
    onSelect: () => {
      selectedSortBy.value = sortByOptions.value[0];
      handleSorting();
    },
  },
  {
    icon: selectedSortBy.value.value === SortBy.CreatedAt ? "mdi:check" : "",
    label: "Date Created",
    onSelect: () => {
      selectedSortBy.value = sortByOptions.value[1];
      handleSorting();
    },
  },
  {
    icon: selectedSortBy.value.value === SortBy.UpdatedAt ? "mdi:check" : "",
    label: "Date Modified",
    onSelect: () => {
      selectedSortBy.value = sortByOptions.value[2];
      handleSorting();
    },
  },
  { type: "separator" as const },
  {
    icon:
      selectedSortDirection.value === SortDirection.Asc
        ? "mdi:sort-ascending"
        : "mdi:sort-descending",
    label: selectedSortDirection.value === SortDirection.Asc ? "Ascending" : "Descending",
    onSelect: () => toggleSortDirection(),
  },
  { type: "separator" as const },
  {
    icon: viewMode.value === "grid" ? "mdi:view-list" : "mdi:view-grid",
    label: viewMode.value === "grid" ? "Switch to List view" : "Switch to Grid view",
    onSelect: () => {
      viewMode.value = viewMode.value === "grid" ? "list" : "grid";
    },
  },
  { type: "separator" as const },
  { icon: "mdi:folder-plus", label: "New Folder", onSelect: () => createNewDirectory() },
]);

// modals

const overlay = useOverlay();
const createDirectoryModal = overlay.create(CreateDirectoryModal);
const updateDirectoryModal = overlay.create(UpdateDirectoryModal);
const fileUploadModal = overlay.create(FileUploadModal);
const directoryUploadModal = overlay.create(DirectoryUploadModal);
const archiveUploadModal = overlay.create(ArchiveUploadModal);
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

// interaction handlers

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

const handleCopy = () => {
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
      instance = archiveUploadModal.open(uploadProps);
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
      clearSelection();
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

onMounted(() => {
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

.fade-status-enter-active,
.fade-status-leave-active {
  transition: opacity 0.35s ease;
}
.fade-status-enter-from,
.fade-status-leave-to {
  opacity: 0;
}

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
