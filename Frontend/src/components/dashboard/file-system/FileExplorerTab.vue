<template>
  <div class="flex flex-col h-full w-full flex-1" @click="handleContainerClick">
    <!-- Toolbar -->
    <div class="flex flex-col w-full border-b border-b-primary">
      <div class="flex items-center gap-2 p-3 w-full">
        <div
          class="flex border border-primary rounded-md overflow-hidden shrink-0"
        >
          <UButton
            color="primary"
            size="sm"
            class="rounded-none border-r dark:border-black"
            @click="
              handleFileUpload(
                selectedUploadType.label as 'File' | 'Directory' | 'Archive',
              )
            "
          >
            <Icon :icon="selectedUploadType.icon" class="w-4 h-4 md:mr-2" />
            <span class="hidden md:inline"
              >Upload {{ selectedUploadType.label }}</span
            >
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
        <UButton
          color="primary"
          size="sm"
          class="shrink-0"
          @click="createNewDirectory"
        >
          <Icon icon="mdi:folder-plus" class="w-4 h-4 md:mr-1" />
          <span class="hidden md:inline">New Folder</span>
        </UButton>

        <div class="flex-1" />

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
            :aria-label="
              selectedSortDirection === SortDirection.Asc
                ? 'Ascending'
                : 'Descending'
            "
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

      <!-- ── Row 2: secondary controls (mobile only) ────────────────────────── -->
      <div class="flex md:hidden items-center gap-1 px-3 pb-2">
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
          :aria-label="
            selectedSortDirection === SortDirection.Asc
              ? 'Ascending'
              : 'Descending'
          "
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

        <!-- Thin divider -->
        <div class="w-px h-5 bg-gray-200 dark:bg-gray-700 mx-1 shrink-0" />

        <!-- View toggle as a compact pill on mobile -->
        <div
          class="flex rounded-md overflow-hidden border border-gray-200 dark:border-gray-700"
        >
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
        :class="
          !canGoForward ? 'opacity-30 cursor-not-allowed' : 'cursor-pointer'
        "
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
      <!-- Drop Zone Overlay  -->
      <Transition name="dropzone">
        <div
          v-if="isOverDropZone"
          class="absolute inset-0 z-50 flex items-center justify-center pointer-events-none"
          aria-hidden="true"
        >
          <!-- Soft backdrop with primary tint -->
          <div class="absolute inset-0 bg-background/60 backdrop-blur-[2px]" />
          <div class="absolute inset-0 bg-primary/5 pulse-tint" />

          <!-- Dashed border inset -->
          <div
            class="absolute inset-3 rounded-xl border-2 border-dashed border-primary/25 pulse-border"
          />

          <!-- Center card -->
          <div
            class="relative flex flex-col items-center gap-3 px-8 py-6 rounded-xl border border-primary/20 bg-background/80 shadow-sm"
          >
            <!-- Breathing ring + icon -->
            <div class="relative flex items-center justify-center">
              <span
                class="breathe absolute rounded-full border border-primary/20"
              />
              <div class="relative z-10 p-3 rounded-full bg-primary/8">
                <Icon :icon="dropIcon" class="w-8 h-8 text-primary/70" />
              </div>
            </div>

            <div class="text-center space-y-0.5">
              <p class="font-medium text-base text-primary/80 tracking-tight">
                {{ dropLabel }}
              </p>
              <p class="text-xs text-muted">Release to upload</p>
            </div>
          </div>
        </div>
      </Transition>
      <!-- ─────────────────────────────────────────────────────────────────── -->

      <!-- Grid View -->
      <div v-if="viewMode === 'grid'" class="p-4">
        <!-- Directories Section -->
        <GridPlaceholder v-if="areDirectoriesLoading" />
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

        <!-- Files Section -->
        <GridPlaceholder v-if="areFilesLoading" />
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
        <!-- Directories Section -->
        <ListPlaceholder v-if="areDirectoriesLoading" />
        <div v-else-if="directoriesList.length > 0">
          <h3 class="text-sm font-semibold opacity-70 mb-2 px-4 pt-4">
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
          />
          <UButton
            variant="ghost"
            label="Show more"
            class="max-w-fit self-end"
            v-if="directoriesData?.hasNext"
            @click="loadMoreDirs"
          />
        </div>

        <!-- Files Section -->
        <ListPlaceholder v-if="areFilesLoading" />

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
import { onMounted, ref, computed, onUnmounted } from "vue";
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
import {
  copyDirectory,
  moveDirectories,
  deleteDirectory,
} from "@/mutations/directories";
import type { SearchTagsSchema } from "@/schemas/tag";
import { searchTag } from "@/queries/tags";
import { useQuery } from "@pinia/colada";
import AdvancedSearchModal from "./Modals/AdvancedSearchModal.vue";
import QuickSearchModal from "./Modals/QuickSearchModal.vue";
import ConfirmModal from "@/components/dashboard/ConfirmModal.vue";
import { useDropZone } from "@vueuse/core";

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

const { data: directoriesData, isLoading: areDirectoriesLoading } =
  directoriesQuery;

const { data: filesData, isLoading: areFilesLoading } = filesQuery;

const tagCurrentPage = ref(1);
const tagPageSize = ref(25);

const searchFilters = computed<SearchTagsSchema>(() => ({
  page: tagCurrentPage.value,
  pageSize: tagPageSize.value,
}));

const { data: tagsData } = useQuery(searchTag(searchFilters.value));

/**
 * Inspects DataTransfer items at drag-time to give a contextual hint.
 * Full directory detection (webkitGetAsEntry) only works reliably on `drop`,
 * so during hover we peek at the item count as a proxy.
 */
const containerRef = ref<HTMLElement | null>(null);

// Tracks whether we *think* a directory is being dragged so the overlay
// can show a contextual icon/label even before the drop resolves.
const dragHasDirectory = ref(false);

const dropIcon = computed(() =>
  dragHasDirectory.value
    ? "mdi:folder-upload-outline"
    : "mdi:cloud-upload-outline",
);
const dropLabel = computed(() =>
  dragHasDirectory.value ? "Drop folder here" : "Drop files here",
);

const { isOverDropZone } = useDropZone(containerRef, {
  /**
   * `onEnter` fires with the raw DragEvent — we can peek at item kinds
   * to speculatively decide if a directory is incoming. Not 100% reliable
   * (browsers may hide the entry type for security), but good enough for UX.
   */
  onEnter(_files, event) {
    const items = Array.from(event.dataTransfer?.items ?? []);
    dragHasDirectory.value = items.some((item) => {
      // webkitGetAsEntry is readable at dragenter (unlike File objects)
      const entry = (item as DataTransferItem).webkitGetAsEntry?.();
      return entry?.isDirectory ?? false;
    });
  },

  onLeave() {
    dragHasDirectory.value = false;
  },

  /**
   * On drop we re-inspect with full fidelity and open the right modal.
   *
   * We deliberately ignore the `_files` argument from vueuse here.
   * dataTransfer.files flattens the tree — directories appear as a single
   * 0-byte File entry and their contents are never exposed. We must walk the
   * tree ourselves via the FileSystem API.
   */
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
      // Recursively unwrap every entry so we get the real files + their paths
      const allFiles = (
        await Promise.all(entries.map((e) => readEntryRecursive(e)))
      ).flat();

      instance = directoryUploadModal.open({
        directoryId: currentDirId.value ?? undefined,
        directoryName: currentDirName.value,
        droppedFiles: allFiles,
      });
    } else {
      // Flat file drop — vueuse's _files is fine here since there's no tree
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
        title: "Upload complete",
        color: "success",
        id: "dropzone-upload-success",
      });
    }
  },
});

// FileSystem API helpers

export interface DroppedFile {
  file: File;
  /** Mirrors webkitRelativePath — e.g. "myFolder/src/index.ts" */
  relativePath: string;
}

/**
 * Recursively reads a FileSystemEntry and returns all leaf files with their
 * relative paths. `path` accumulates the directory prefix as we recurse.
 */
async function readEntryRecursive(
  entry: FileSystemEntry,
  path = "",
): Promise<DroppedFile[]> {
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
      children.map((child) =>
        readEntryRecursive(child, `${path}${entry.name}/`),
      ),
    );
    return nested.flat();
  }

  return [];
}

/**
 * readEntries only guarantees up to 100 results per call — loop until the
 * batch comes back empty to ensure we collect every entry in large directories.
 */
function readAllEntries(
  reader: FileSystemDirectoryReader,
): Promise<FileSystemEntry[]> {
  return new Promise((resolve, reject) => {
    const collected: FileSystemEntry[] = [];

    const readBatch = () => {
      reader.readEntries((batch) => {
        if (batch.length === 0) {
          resolve(collected);
        } else {
          collected.push(...batch);
          readBatch(); // keep reading until empty batch
        }
      }, reject);
    };

    readBatch();
  });
}

let copyMode = true;

defineShortcuts({
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
  Delete: () => handleDelete(),
  alt_arrowleft: () => {
    if (canGoBack.value) navigateBack();
  },
  alt_arrowright: () => {
    if (canGoForward.value) navigateForward();
  },
});

// Sort options
const sortByOptions = ref([
  { label: "Name", value: SortBy.Name },
  { label: "Date Created", value: SortBy.CreatedAt },
  { label: "Date Modified", value: SortBy.UpdatedAt },
]);

const selectedUploadType = ref({
  label: "File",
  icon: "mdi:file-outline",
});

const uploadOptions = ref([
  {
    label: "File",
    icon: "mdi:file-outline",
    onSelect: () => handleFileUpload("File"),
  },
  {
    label: "Directory",
    icon: "mdi:folder-outline",
    onSelect: () => handleFileUpload("Directory"),
  },
  {
    label: "Archive",
    icon: "formkit:zip",
    onSelect: () => handleFileUpload("Archive"),
  },
] satisfies DropdownMenuItem[]);

const selectedSortBy = ref({ label: "Name", value: SortBy.Name });
const selectedSortDirection = ref<SortDirection>(SortDirection.Asc);

const toggleSortDirection = () => {
  selectedSortDirection.value =
    selectedSortDirection.value === SortDirection.Asc
      ? SortDirection.Desc
      : SortDirection.Asc;

  filePagination.value.paginationParams.sortDirection =
    selectedSortDirection.value;
  dirPagination.value.paginationParams.sortDirection =
    selectedSortDirection.value;
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
  if (!target.closest("button")) {
    clearSelection();
  }
};

const gridColumns = computed(() => {
  return "grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 2xl:grid-cols-8";
});

const handleDirectoryRename = async (directoryId: string) => {
  const instance = updateDirectoryModal.open({ directoryId });
  const shouldRefresh = await instance.result;

  if (shouldRefresh && settingsStore.toastLevel === "all") {
    toast.add({
      title: "Directory updated successfully",
      color: "success",
      id: "modal-success",
    });
    refreshDir();
    return;
  }
  if (!shouldRefresh && settingsStore.toastLevel !== "silent")
    toast.add({
      title: "Directory update failed",
      color: "error",
      id: "modal-error",
    });
};

const handleCopy = async () => {
  fileStore.filesToCopy = [...selectedFiles.value];
  directoryStore.directoriesToCopy = [...selectedDirectories.value];
  if (settingsStore.toastLevel === "all")
    toast.add({ title: "Items selected", color: "info", id: "copying" });
};

const handleDelete = async () => {
  if (selectedFiles.value.size > 0) {
    await deleteFilesMutate({ ids: [...selectedFiles.value] });
  }

  if (selectedDirectories.value.size > 0) {
    const dirIds = Array.from(selectedDirectories.value);

    const results = await Promise.allSettled(
      dirIds.map((id) =>
        deleteDirectoryMutate({ id, options: { force: false } }),
      ),
    );

    const failedDirs = results
      .map((result, index) => ({ result, id: dirIds[index] }))
      .filter(
        (x) =>
          x.result.status === "rejected" &&
          x.result.reason?.response?.status === 409,
      )
      .map((x) => x.id);

    if (failedDirs.length > 0) {
      if (settingsStore.skipDeleteConfirmation) {
        await Promise.all(
          failedDirs.map((id) =>
            deleteDirectoryMutate({ id, options: { force: true } }),
          ),
        );
      } else {
        const instance = confirmModal.open({
          title: "Delete directories?",
          description: `${failedDirs.length} ${failedDirs.length === 1 ? "directory" : "directories"} still contain files and will be deleted.`,
          dangerMode: true,
          confirmLabel: "Delete anyway",
          confirmIcon: "i-lucide-trash-2",
          alert: {
            title: "All sub items wiill also be deleted",
            color: "warning",
            icon: "i-lucide-triangle-alert",
          },
        });

        const confirmed = await instance.result;
        if (confirmed) {
          await Promise.all(
            failedDirs.map((id) =>
              deleteDirectoryMutate({ id, options: { force: true } }),
            ),
          );
        }
      }
    }
  }
  if (settingsStore.toastLevel === "all")
    toast.add({ title: "Items deleted", color: "info", id: "deleting" });
  refreshDir();
};

const handleCut = async () => {
  await moveFilesMutate({
    destinationId: currentDirId.value,
    fileIds: fileStore.filesToCopy,
  });
  await moveDirectoriesMutate({
    destinationId: currentDirId.value,
    directoryIds: directoryStore.directoriesToCopy,
  });
  refreshDir();
};

const handlePaste = async () => {
  if (fileStore.filesToCopy.length > 0)
    await copyFilesMutate({
      fileIds: fileStore.filesToCopy,
      destinationId: currentDirId.value,
    });

  if (directoryStore.directoriesToCopy.length > 0)
    await Promise.all(
      directoryStore.directoriesToCopy.map(async (dir) => {
        await copyDirectoryMutate({
          destinationId: currentDirId.value,
          directoryId: dir,
        });
      }),
    );

  refreshDir();
};

const handleSorting = () => {
  filePagination.value.paginationParams.SortBy = selectedSortBy.value.value;
  dirPagination.value.paginationParams.SortBy = selectedSortBy.value.value;
  refreshDir();
};

/**
 * TODO: This is messy, I should add a track for the current dir name in th composable to make this task simpler and less fragile
 */
const currentDirName = computed<string | undefined>(() => {
  if (!currentDirId.value) return undefined;
  const parts = pathQuery.data.value?.pathParts;
  return parts?.[parts.length - 1]?.name;
});

const handleFileUpload = async (type: "File" | "Directory" | "Archive") => {
  const option = uploadOptions.value.find((opt) => opt.label === type);
  if (option) {
    selectedUploadType.value = { label: option.label, icon: option.icon };
  }

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
  if (
    !shouldRefresh &&
    directoryStore.error &&
    settingsStore.toastLevel !== "silent"
  )
    toast.add({
      title: "Upload failed",
      description: directoryStore.error,
      color: "error",
      id: "modal-error",
    });
};

const breadcrumbs = computed(() => {
  const items: BreadcrumbItem[] = [
    {
      label: "Home",
      to: { name: "dashboard" },
      icon: "i-heroicons-home",
      key: null,
    },
  ];

  const path = pathQuery.data.value?.pathParts;

  if (path && path.length > 0) {
    const pathItems = path.map((segment) => ({
      label: segment.name,
      key: segment.id,
    }));
    items.push(...pathItems);
  }
  return items;
});

const createNewDirectory = async () => {
  const instance = createDirectoryModal.open({
    parentId: currentDirId.value,
  });

  const shouldRefresh = await instance.result;
  if (shouldRefresh && settingsStore.toastLevel === "all") {
    toast.add({
      title: "Directory creation successful",
      color: "success",
      id: "modal-success",
    });
    refreshDir();
    return;
  }
  if (
    !shouldRefresh &&
    directoryStore.error &&
    settingsStore.toastLevel !== "silent"
  )
    toast.add({
      title: "Directory creation failed",
      description: directoryStore.error,
      color: "error",
      id: "modal-error",
    });
};

const handleItemClick = (
  event: MouseEvent,
  id: string,
  type: "file" | "directory",
) => {
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
  navigateTo(dirId);
  tabStore.setActiveDir(props.tabId, dirId);
};

const handleMouseNavigate = (event: MouseEvent) => {
  // Button 3 = back, Button 4 = forward (side buttons)
  if (event.button !== 3 && event.button !== 4) return;

  event.preventDefault(); // prevent browser's own back/forward
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
});
</script>

<style scoped>
/* Breathing ring */
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

/* Tint pulse */
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

/* Border pulse */
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

/* Overlay transition  */
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
