<template>
  <div class="flex flex-col h-full w-full flex-1" @click="handleContainerClick">
    <!-- Toolbar -->
    <div
      class="flex flex-col gap-2 p-3 border-b border-b-primary md:flex-row md:items-center md:justify-between"
    >
      <!-- Left side - Action buttons -->
      <div class="flex items-center gap-2 flex-wrap">
        <UButton color="primary" size="sm" @click="createNewDirectory">
          <Icon icon="mdi:folder-plus" class="w-4 h-4 md:mr-1" />
          <span class="hidden sm:inline">New Folder</span>
        </UButton>
        <UButton color="primary" size="sm" @click="handleFileUpload">
          <Icon icon="mdi:upload" class="w-4 h-4 md:mr-1" />
          <span class="hidden sm:inline">Upload</span>
        </UButton>

        <!-- View mode toggle -->
        <div class="flex items-center gap-1 ml-auto md:ml-2">
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

      <!-- Right side - Search and info -->
      <div class="flex items-center gap-2 justify-between md:justify-end">
        <div class="text-sm opacity-70 order-2 md:order-1">
          <span class="hidden sm:inline">
            {{ directories.length }} folders, {{ files.length }} files
          </span>
          <span class="sm:hidden">
            {{ directories.length + files.length }} items
          </span>
          <!--
          <span v-if="selectedItems.size > 0" class="ml-2 font-medium">
            ({{ selectedItems.size }} selected)
          </span>
          -->
        </div>
        <div class="order-1 md:order-2">
          <SearchComponent @navigate="handleNavigate" />
        </div>
      </div>
    </div>

    <!-- <FilePathBreadCrumbs /> -->
    <UBreadcrumb :items="breadcrumbs" class="p-4" />

    <!-- Content Area -->
    <div ref="containerRef" class="flex-1 overflow-auto relative">
      <!-- Grid View -->
      <div v-if="viewMode === 'grid'" class="p-4">
        <!-- Directories Section -->
        <div v-if="directories.length > 0" class="mb-6 flex flex-col">
          <div>
            <div class="flex items-center gap-2 ml-2 pb-4">
              <h3 class="font-semibold opacity-70 px-1">Folders</h3>

              <USelectMenu
                v-model="selectedSortBy"
                :items="sortByOptions"
                size="sm"
                placeholder="Sort by"
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
                  selectedSortDirection === 'Asc' ? 'Ascending' : 'Descending'
                "
              >
                <Icon
                  :icon="
                    selectedSortDirection === 'Asc'
                      ? 'mdi:sort-ascending'
                      : 'mdi:sort-descending'
                  "
                  class="w-4 h-4"
                />
              </UButton>
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
              @move="handleDirectoryMove"
              @click="handleItemClick($event, dir.id, 'directory')"
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
            @navigate="handleNavigate(dir.id)"
            @click="handleItemClick($event, dir.id, 'directory')"
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
import CreateDirectoryModal from "./Modals/CreateDirectoryModal.vue";
import UpdateDirectoryModal from "./Modals/UpdateDirectoryModal.vue";
import MoveDirectoryModal from "./Modals/MoveDirectoryModal.vue";
import FileUploadModal from "./Modals/FileUploadModal.vue";
import { useFileExplorer } from "@/composables/useFileExplorer";
import { useTabStore } from "@/stores/tab";

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
  loadMoreDirs,
  loadMoreFiles,
  navigateTo,
  toggleSelect,
  isDirectorySelected,
  isFileSelected,
  clearSelection,
  selectRange,
  downloadFile,
  // uploadFile,
} = useFileExplorer();
// Sort options
const sortByOptions = ref([
  { label: "Name", value: OrderBy.Name },
  { label: "Date Created", value: OrderBy.CreatedAt },
  { label: "Date Modified", value: OrderBy.UpdatedAt },
]);

const selectedSortBy = ref({ label: "Name", value: OrderBy.Name });
const selectedSortDirection = ref<"Asc" | "Desc">("Asc");

const toggleSortDirection = () => {
  selectedSortDirection.value =
    selectedSortDirection.value === "Asc" ? "Desc" : "Asc";
};

// Modals
const overlay = useOverlay();
const createDirectoryModal = overlay.create(CreateDirectoryModal);
const updateDirectoryModal = overlay.create(UpdateDirectoryModal);
const moveDirectoryModal = overlay.create(MoveDirectoryModal);
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

const handleDirectoryMove = async (directoryId: string) => {
  const instance = moveDirectoryModal.open({ directoryId: directoryId });

  const shouldRefresh = await instance.result;

  console.log(shouldRefresh);

  if (shouldRefresh) {
    toast.add({
      title: "Directory moved successfully",
      color: "success",
      id: "modal-success",
    });

    // console.log("refreshing");
    await navigateTo();
    // if (response.success) {
    //   directories.value = response.data?.directories ?? [];
    //   files.value = response.data?.files ?? [];
    // }

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
  const items = [
    {
      label: "Home",
      to: { name: "dashboard" },
      icon: "i-heroicons-home",
    },
  ];

  // Add path segments from store
  const path = pathList;
  if (path && path.value.length > 0) {
    const pathItems = path.value.map((segment) => ({
      label: segment.name,
      to: { name: "dashboard", params: { dirId: segment.id } },
    }));
    items.push(...pathItems);
  }
  console.log("path items", items);
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
