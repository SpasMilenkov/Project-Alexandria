<template>
  <div class="flex flex-col h-full w-full flex-1" @click="handleContainerClick">
    <!-- Toolbar -->
    <div
      class="flex w-full gap-6 md:gap-2 p-3 border-b border-b-primary flex-row items-center justify-between flex-wrap"
    >
      <!-- <div class="flex items-center gap-2 flex-wrap"> -->
      <div class="flex gap-2 justify-between">
        <div class="flex gap-2">
          <UButton color="primary" size="sm" @click="createNewDirectory">
            <Icon icon="mdi:folder-plus" class="w-4 h-4 md:mr-1" />
            <span class="hidden sm:inline">New Folder</span>
          </UButton>
          <UButton color="primary" size="sm" @click="handleFileUpload">
            <Icon icon="mdi:upload" class="w-4 h-4 md:mr-1" />
            <span class="hidden sm:inline">Upload</span>
          </UButton>
        </div>

        <div class="text-sm">
          <USelectMenu
            v-model="selectedSortBy"
            :items="sortByOptions"
            size="sm"
            placeholder="Sort by"
            @update:model-value="handleSorting"
          >
            <template #default="{ modelValue }">
              <Icon icon="mdi:sort" class="w-4 h-4 mr-1" />
              <!-- TODO: Should probably add a  fallback here in case it is null for some reason -->
              <span class="hidden sm:inline">{{ modelValue?.label }}</span>
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
          <!-- <span class="hidden sm:inline">
          {{ directories.length }} folders, {{ files.length }} files
        </span>
        <span class="sm:hidden">
          {{ directories.length + files.length }} items
        </span> -->
        </div>
      </div>

      <SearchComponent @navigate="handleNavigate" />
    </div>

    <UBreadcrumb :items="breadcrumbs" class="p-4">
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

    <!-- Content Area -->
    <div ref="containerRef" class="flex-1 overflow-auto relative">
      <!-- Grid View -->
      <div v-if="viewMode === 'grid'" class="p-4">
        <!-- Directories Section -->
        <div v-if="directories.length > 0" class="mb-6 flex flex-col">
          <div>
            <div class="flex items-center gap-2 ml-2 pb-4">
              <h3 class="font-semibold opacity-70 px-1">Folders</h3>
            </div>
          </div>
          <div class="grid gap-3" :class="gridColumns">
            <DirectoryItem
              v-for="dir in directories"
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
          </div>
          <UButton
            variant="ghost"
            label="Show more"
            class="max-w-fit self-end"
            v-if="dirPagination.hasNext"
            @click="loadMoreDirs"
          />
        </div>

        <!-- Files Section -->
        <div v-if="files.length > 0" class="mb-6 flex flex-col">
          <h3 class="text-sm font-semibold opacity-70 mb-3 px-1">Files</h3>
          <div class="grid gap-3" :class="gridColumns">
            <FileItem
              v-for="file in files"
              :key="file.fileId"
              :data="file"
              :view-mode="viewMode"
              :is-selected="isFileSelected(file.fileId)"
              @download="downloadFile(file.fileId, file.fileName)"
              @click="handleItemClick($event, file.fileId, 'file')"
              @copy="handleCopy"
              @delete="handleDelete"
              @move="handleCut"
            />
          </div>
          <UButton
            variant="ghost"
            label="Show more"
            class="max-w-fit self-end"
            v-if="filePagination.hasNext"
            @click="loadMoreFiles"
          />
        </div>
      </div>

      <!-- List View -->
      <div v-else class="flex flex-col">
        <!-- Directories Section -->
        <div v-if="directories.length > 0">
          <h3 class="text-sm font-semibold opacity-70 mb-2 px-4 pt-4">
            Folders
          </h3>
          <DirectoryItem
            v-for="dir in directories"
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
            v-if="dirPagination.hasNext"
            @click="loadMoreDirs"
          />
        </div>

        <!-- Files Section -->
        <div
          v-if="files.length > 0"
          :class="{ 'mt-4': directories.length > 0 }"
        >
          <h3
            class="text-sm font-semibold opacity-70 mb-2 px-4"
            :class="{ 'pt-4': directories.length === 0 }"
          >
            Files
          </h3>
          <FileItem
            v-for="file in files"
            :key="file.fileId"
            :data="file"
            :view-mode="viewMode"
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
            v-if="filePagination.hasNext"
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
import { onMounted, ref, computed } from "vue";
import { Icon } from "@iconify/vue";
import { OrderBy } from "@/enums/OrderBy";
import { useFileExplorer } from "@/composables/useFileExplorer";
import { useTabStore } from "@/stores/tab";
import type { BreadcrumbItem } from "@nuxt/ui";
import { SortDirection } from "@/enums/SortDirection";
import CreateDirectoryModal from "./Modals/CreateDirectoryModal.vue";
import UpdateDirectoryModal from "./Modals/UpdateDirectoryModal.vue";
import FileUploadModal from "./Modals/FileUploadModal.vue";
import { useFileStore } from "@/stores/file";
import { useDirectoryStore } from "@/stores/directory";
import { useFileExplorerApi } from "@/composables/useFileExplorerApi";

const fileStore = useFileStore();
const directoryStore = useDirectoryStore();
const tabStore = useTabStore();

const props = defineProps<{
  tabId: string;
}>();

const toast = useToast();

const {
  currentDirId,
  directories,
  files,
  viewMode,
  filePagination,
  dirPagination,
  pathList,
  lastSelected,
  selectedDirectories,
  selectedFiles,
  loadMoreDirs,
  loadMoreFiles,
  navigateTo,
  refreshDir,
  toggleSelect,
  isDirectorySelected,
  isFileSelected,
  clearSelection,
  selectRange,
  downloadFile,
  // uploadFile,
} = useFileExplorer();

const { copySelected, moveSelected, deleteDirectory, deleteFile } =
  useFileExplorerApi();

let copyMode = true;

defineShortcuts({
  meta_c: () => {
    console.log("Ctrl + C is pressed");
    copyMode = true;
    handleCopy();
  },
  meta_v: () => {
    console.log("Ctrl + V is pressed");

    if (copyMode) handlePaste();
    else handleCut();
  },
  meta_x: () => {
    copyMode = false;
    handleCopy();
    console.log("Ctrl + X is pressed");
  },
  Delete: () => {
    handleDelete();
    console.log("Delete has been pressed");
  },
});

// Sort options
const sortByOptions = ref([
  { label: "Name", value: OrderBy.Name },
  { label: "Date Created", value: OrderBy.CreatedAt },
  { label: "Date Modified", value: OrderBy.UpdatedAt },
]);

const selectedSortBy = ref({ label: "Name", value: OrderBy.Name });
const selectedSortDirection = ref<SortDirection>(SortDirection.Asc);

const toggleSortDirection = async () => {
  selectedSortDirection.value =
    selectedSortDirection.value === SortDirection.Asc
      ? SortDirection.Desc
      : SortDirection.Asc;

  filePagination.value.paginationParams.sortDirection =
    selectedSortDirection.value;
  dirPagination.value.paginationParams.sortDirection =
    selectedSortDirection.value;
  await refreshDir(currentDirId.value);
};

// Modals
const overlay = useOverlay();
const createDirectoryModal = overlay.create(CreateDirectoryModal);
const updateDirectoryModal = overlay.create(UpdateDirectoryModal);
const fileUploadModal = overlay.create(FileUploadModal);

const handleContainerClick = (event: MouseEvent) => {
  // Check if we clicked on empty space (not on any item buttons)
  const target = event.target as HTMLElement;
  if (!target.closest("button")) {
    clearSelection();
  }
};

const gridColumns = computed(() => {
  return "grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 2xl:grid-cols-8";
});

const handleDirectoryRename = async (directoryId: string) => {
  const instance = updateDirectoryModal.open({
    directoryId: directoryId,
  });

  const shouldRefresh = await instance.result;

  console.log(shouldRefresh);

  if (shouldRefresh) {
    toast.add({
      title: "Directory updated successfully",
      color: "success",
      id: "modal-success",
    });

    console.log("refreshing");
    await navigateTo(currentDirId.value);
    // if (response.success) {
    //   directories.value = response.data?.directories ?? [];
    //   files.value = response.data?.files ?? [];
    // }

    return;
  }
  // if (!shouldRefresh && directoryStore.error)
  //   toast.add({
  //     title: "Directory update failed",
  //     description: directoryStore.error,
  //     color: "error",
  //     id: "modal-error",
  //   });
};

const handleCopy = async () => {
  fileStore.filesToCopy = [...selectedFiles.value];
  directoryStore.directoriesToCopy = [...selectedDirectories.value];
  console.log("handling copy");
  toast.add({
    title: "Files selected",
    color: "info",
    id: "copying",
  });
};

const handleDelete = async () => {
  await deleteFile([...selectedFiles.value]);

  selectedDirectories.value.forEach(async (d) => await deleteDirectory(d));
  toast.add({
    title: "Items deleted",
    color: "info",
    id: "deleting",
  });
  refreshDir(currentDirId.value);
};

const handleCut = async () => {
  await moveSelected({
    destinationId: currentDirId.value,
    selectedFiles: fileStore.filesToCopy,
    selectedDirectories: directoryStore.directoriesToCopy,
  });
  refreshDir(currentDirId.value);
};

const handlePaste = async () => {
  await copySelected({
    destinationId: currentDirId.value,
    selectedFiles: fileStore.filesToCopy,
    selectedDirectories: directoryStore.directoriesToCopy,
  });
  refreshDir(currentDirId.value);
};

const handleSorting = async () => {
  filePagination.value.paginationParams.orderBy = selectedSortBy.value.value;
  dirPagination.value.paginationParams.orderBy = selectedSortBy.value.value;

  await refreshDir(currentDirId.value);
};

const handleFileUpload = async () => {
  const instance = fileUploadModal.open();

  const shouldRefresh = await instance.result;

  console.log(shouldRefresh);

  if (shouldRefresh) {
    // Fetch the path first if we have a dirId
    navigateTo(currentDirId.value);

    return;
  }
  // if (!shouldRefresh && directoryStore.error)
  //   toast.add({
  //     title: "Directory failed",
  //     description: directoryStore.error,
  //     color: "error",
  //     id: "modal-error",
  //   });
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

  // Add path segments from store
  const path = pathList;
  if (path && path.value.length > 0) {
    const pathItems = path.value.map((segment) => ({
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
  console.log(shouldRefresh);
  if (shouldRefresh) {
    toast.add({
      title: "Directory creation successful",
      color: "success",
      id: "modal-success",
    });

    await navigateTo(currentDirId.value);

    return;
  }
  // if (!shouldRefresh && directoryStore.error)
  //   toast.add({
  //     title: "Directory creation failed",
  //     description: directoryStore.error,
  //     color: "error",
  //     id: "modal-error",
  //   });
};

const handleItemClick = (
  event: MouseEvent,
  id: string,
  type: "file" | "directory"
) => {
  const isCtrlOrCmd = event.ctrlKey || event.metaKey;
  const isShift = event.shiftKey;
  if (isShift && lastSelected.value) {
    // Range selection
    selectRange(lastSelected.value, id);
  } else if (isCtrlOrCmd) {
    // Toggle selection
    toggleSelect(id, type);
    lastSelected.value = id;
  } else {
    // Single selection (clear others)
    clearSelection();
    toggleSelect(id, type);
    lastSelected.value = id;
  }
};

const handleNavigate = (dirId: string | null) => {
  navigateTo(dirId);
  tabStore.setActiveDir(props.tabId, dirId);
};

onMounted(async () => {
  const tab = tabStore.getTab(props.tabId);
  navigateTo(tab?.activeDirId);
});
</script>

<style scoped></style>
