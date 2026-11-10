<template>
  <UModal
    :title="modalTitle"
    :description="modalDescription"
    :close="{ onClick: () => emit('close', false) }"
    :ui="{ content: glassModalContentWide, body: glassModalBody }"
  >
    <template #body>
      <!-- Selected items -->
      <ul class="flex flex-wrap gap-2" aria-label="Selected items">
        <li
          v-for="chip in visibleChips"
          :key="chip.id"
          class="flex items-center gap-1.5 max-w-48 rounded-md border border-gray-200/70 dark:border-gray-700/70 px-2 py-1"
        >
          <Icon :icon="chip.icon" class="w-3.5 h-3.5 shrink-0 text-gray-500 dark:text-gray-500" />
          <span class="text-xs truncate text-gray-700 dark:text-gray-300">{{ chip.name }}</span>
        </li>

        <li
          v-if="overflowCount > 0"
          class="flex items-center px-2 py-1 text-xs text-gray-600 dark:text-gray-400"
        >
          +{{ overflowCount }} more
        </li>
      </ul>

      <!-- Destination picker -->
      <div class="space-y-2">
        <UFormField label="Destination">
          <UInput
            v-model="searchQuery"
            placeholder="Search folders…"
            icon="i-mdi-magnify"
            class="w-full"
            @update:model-value="handleSearchInput"
          >
            <template v-if="searchQuery" #trailing>
              <UButton
                icon="i-mdi-close"
                size="xs"
                variant="ghost"
                color="neutral"
                square
                class="justify-center"
                aria-label="Clear search"
                @click="clearSearch"
              />
            </template>
          </UInput>
        </UFormField>

        <nav
          v-if="pickerMode === 'browse'"
          aria-label="Folder path"
          class="flex items-center gap-1 min-w-0 overflow-x-auto"
        >
          <button
            type="button"
            class="flex items-center gap-1 shrink-0 rounded-md px-2 py-1 text-xs transition-colors duration-150 hover:bg-black/[0.03] dark:hover:bg-white/[0.04]"
            :class="crumbClass(browsePath.length === 0)"
            :aria-current="browsePath.length === 0 ? 'location' : undefined"
            @click="browseNavigateTo(null, 'Home')"
          >
            <Icon icon="mdi:home-outline" class="w-3.5 h-3.5" />
            Home
          </button>

          <template v-for="(segment, idx) in browsePath" :key="segment.id">
            <Icon
              icon="mdi:chevron-right"
              class="w-3.5 h-3.5 shrink-0 text-gray-400 dark:text-gray-600"
            />

            <button
              type="button"
              class="shrink-0 max-w-40 truncate rounded-md px-2 py-1 text-xs transition-colors duration-150 hover:bg-black/[0.03] dark:hover:bg-white/[0.04]"
              :class="crumbClass(idx === browsePath.length - 1)"
              :aria-current="idx === browsePath.length - 1 ? 'location' : undefined"
              @click="browseNavigateTo(segment.id, segment.name)"
            >
              {{ segment.name }}
            </button>
          </template>
        </nav>

        <p v-else class="px-2 py-1 text-xs text-gray-600 dark:text-gray-400">
          Folders matching "{{ searchQuery.trim() }}"
        </p>

        <div
          class="h-64 overflow-y-auto rounded-lg border border-gray-200/70 dark:border-gray-700/70"
        >
          <div v-if="isListLoading" class="flex h-full items-center justify-center">
            <BlockSpinner />
          </div>

          <div
            v-else-if="pickerMode === 'search' && searchResults.length === 0"
            class="flex h-full flex-col items-center justify-center gap-2 px-6 text-center"
          >
            <Icon
              icon="mdi:folder-search-outline"
              class="w-12 h-12 text-gray-400 dark:text-gray-600"
            />
            <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No folders found</p>
            <p class="text-xs text-gray-600 dark:text-gray-400">Try a different name</p>
          </div>

          <ul v-else class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
            <li v-for="row in folderRows" :key="row.key">
              <div
                role="button"
                :tabindex="row.state === 'blocked' ? -1 : 0"
                :aria-disabled="row.state === 'blocked'"
                :aria-pressed="row.state === 'selected'"
                :aria-label="rowLabel(row)"
                class="flex items-center gap-3 px-4 py-2 min-h-11 transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/60"
                :class="ROW_CLASSES[row.state]"
                @click="selectFolder(row)"
                @dblclick="openFolder(row)"
                @keydown.enter.self.prevent="selectFolder(row)"
                @keydown.space.self.prevent="selectFolder(row)"
                @keydown.right.self.prevent="openFolder(row)"
              >
                <Icon :icon="row.icon" class="w-5 h-5 shrink-0" :class="ICON_CLASSES[row.state]" />

                <span class="flex-1 min-w-0 truncate text-sm" :class="NAME_CLASSES[row.state]">
                  {{ row.name }}
                </span>

                <span v-if="row.hint" class="shrink-0 text-xs text-gray-500 dark:text-gray-500">
                  {{ row.hint }}
                </span>

                <Icon
                  v-if="row.state === 'selected'"
                  icon="mdi:check-circle"
                  class="w-4 h-4 shrink-0 text-primary"
                />

                <UTooltip v-if="row.canOpen" text="Open folder">
                  <UButton
                    icon="i-mdi-chevron-right"
                    size="xs"
                    variant="ghost"
                    color="neutral"
                    square
                    class="justify-center"
                    :aria-label="`Open ${row.name}`"
                    @click.stop="openFolder(row)"
                  />
                </UTooltip>
              </div>
            </li>

            <li
              v-if="pickerMode === 'browse' && browseSubdirs.length === 0"
              class="px-4 py-6 text-center text-xs text-gray-500 dark:text-gray-500"
            >
              No subfolders here
            </li>
          </ul>
        </div>
      </div>

      <Transition
        enter-active-class="transition-all duration-200 ease-out"
        enter-from-class="opacity-0 -translate-y-1"
        leave-active-class="transition-all duration-150 ease-in"
        leave-to-class="opacity-0 -translate-y-1"
      >
        <UAlert
          v-if="isSameDirectory"
          color="warning"
          variant="subtle"
          icon="i-mdi-alert-circle-outline"
          title="These items are already in this folder"
        />
      </Transition>
    </template>

    <template #footer>
      <div class="flex w-full items-center justify-between gap-3">
        <p class="min-w-0 truncate text-sm text-gray-600 dark:text-gray-400">
          <template v-if="hasDestination">
            {{ mode === "move" ? "Moving to" : "Copying to" }}
            <span class="font-medium text-gray-900 dark:text-gray-100">
              {{ destinationDirName }}
            </span>
          </template>
          <template v-else>Choose a folder</template>
        </p>

        <div class="flex shrink-0 items-center gap-2">
          <UButton label="Cancel" variant="outline" color="neutral" @click="emit('close', false)" />
          <UButton
            :label="mode === 'move' ? 'Move here' : 'Copy here'"
            :icon="mode === 'move' ? 'i-mdi-folder-move-outline' : 'i-mdi-content-copy'"
            variant="solid"
            color="primary"
            :loading="isConfirming"
            :disabled="!hasDestination || isSameDirectory"
            @click="handleConfirm"
          />
        </div>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useDebounceFn } from "@vueuse/core";
import { computed, onMounted, ref } from "vue";

import { type DirectorySummaryDto, directoryApi } from "@/api/directory";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { useAppToast } from "@/composables/useAppToast";
import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { describeExplorerItems, quoted } from "@/features/toast/format";
import { copyDirectory, moveDirectories } from "@/mutations/directories";
import { copyFiles, moveFiles } from "@/mutations/files";
import { directorySearchApiSchema } from "@/schemas/search";
import { useDirectoryStore } from "@/stores/directory";
import { formatCount } from "@/utils/count";
import { logger } from "@/utils/logger";
import { glassModalBody, glassModalContentWide } from "@/utils/modal-ui";

export interface TransferResult {
  destinationId: string | null;
}

interface FileChip {
  id: string;
  name: string;
  icon: string;
  type: "file" | "directory";
}

interface BrowseSegment {
  id: string;
  name: string;
}

interface DirSummary {
  id: string;
  name: string;
}

type FolderState = "selected" | "blocked" | "idle";

interface FolderRow {
  key: string;
  id: string | null;
  name: string;
  icon: string;
  hint: string | null;
  state: FolderState;
  canOpen: boolean;
}

const {
  mode,
  files,
  directories,
  originDirId,
  originDirName,
  fileChips = [],
  dirChips = [],
} = defineProps<{
  mode: "move" | "copy";
  files: string[];
  directories: string[];
  originDirId: string | null;
  originDirName: string | undefined;
  fileChips?: FileChip[];
  dirChips?: FileChip[];
}>();

const emit = defineEmits<{
  // Success carries the destination, which is null for Home, so it cannot double as "cancelled".
  close: [result: TransferResult | false];
}>();

useModalBackGuard(() => emit("close", false));

const appToast = useAppToast("move");
const directoryStore = useDirectoryStore();

const { mutateAsync: moveFilesMutate } = moveFiles();
const { mutateAsync: copyFilesMutate } = copyFiles();
const { mutateAsync: moveDirectoriesMutate } = moveDirectories();
const { mutateAsync: copyDirectoryMutate } = copyDirectory();

// Header and selected items

const MAX_VISIBLE_CHIPS = 6;

const chipFor = (id: string, chips: FileChip[], fallback: Omit<FileChip, "id">): FileChip =>
  chips.find((chip) => chip.id === id) ?? { ...fallback, id };

const allChips = computed<FileChip[]>(() => [
  ...directories.map((id) =>
    chipFor(id, dirChips, {
      icon: "mdi:folder-outline",
      name: "Folder",
      type: "directory",
    }),
  ),
  ...files.map((id) =>
    chipFor(id, fileChips, {
      icon: "mdi:file-outline",
      name: "File",
      type: "file",
    }),
  ),
]);

const totalCount = computed(() => files.length + directories.length);
const visibleChips = computed(() => allChips.value.slice(0, MAX_VISIBLE_CHIPS));
const overflowCount = computed(() => Math.max(0, totalCount.value - MAX_VISIBLE_CHIPS));

const modalTitle = computed(() => {
  const action = mode === "move" ? "Move" : "Copy";
  return `${action} ${formatCount(totalCount.value, "item")}`;
});

const modalDescription = computed(() => `From ${originDirName ?? "Home"}`);

// Destination state

const destinationDirId = ref<string | null | undefined>(undefined);
const destinationDirName = ref<string | null>(null);

const hasDestination = computed(() => destinationDirId.value !== undefined);

const isSameDirectory = computed(
  () => hasDestination.value && destinationDirId.value === originDirId,
);

// A folder cannot become the destination of its own transfer.
const transferredDirIds = computed(() => new Set(directories));

const isTransferred = (id: string | null) => id !== null && transferredDirIds.value.has(id);

// Browse mode

type PickerMode = "browse" | "search";

const pickerMode = ref<PickerMode>("browse");
const browsePath = ref<BrowseSegment[]>([]);
const browseSubdirs = ref<DirSummary[]>([]);
const isLoadingDirs = ref(false);

let browseRequest = 0;

const currentBrowseDirId = computed<string | null>(() => browsePath.value.at(-1)?.id ?? null);
const currentBrowseDirName = computed(() => browsePath.value.at(-1)?.name ?? "Home");

const toDirSummaries = (items: DirectorySummaryDto[] | undefined): DirSummary[] =>
  (items ?? []).map((d) => ({ id: d.id, name: d.name }));

const loadBrowseSubdirs = async (parentId: string | null) => {
  const request = ++browseRequest;
  isLoadingDirs.value = true;
  browseSubdirs.value = [];

  try {
    const parsed = directorySearchApiSchema.parse({
      currentPage: 0,
      pageSize: 50,
      parentDirectoryId: parentId ?? undefined,
    });
    const result = await directoryStore.searchDirectory(parsed);
    if (request !== browseRequest) return;

    browseSubdirs.value = toDirSummaries(result.data?.items);
  } catch (e) {
    logger.error("FileTransferModal: failed to load subdirs", e);
    appToast.error("Couldn't load folders", e);
  } finally {
    if (request === browseRequest) isLoadingDirs.value = false;
  }
};

const browseNavigateTo = (id: string | null, name: string) => {
  if (id === null) {
    browsePath.value = [];
  } else {
    const existingIdx = browsePath.value.findIndex((s) => s.id === id);

    if (existingIdx === -1) {
      browsePath.value = [...browsePath.value, { id, name }];
    } else {
      browsePath.value = browsePath.value.slice(0, existingIdx + 1);
    }
  }

  void loadBrowseSubdirs(id);
};

// Search results can live anywhere, so their breadcrumb comes from the server.
const browseIntoFromSearch = async (id: string, name: string) => {
  clearSearch();
  browsePath.value = [{ id, name }];
  void loadBrowseSubdirs(id);

  try {
    const { pathParts } = await directoryApi.getDirectoryPath(id);
    if (currentBrowseDirId.value === id && pathParts.length > 0) browsePath.value = pathParts;
  } catch (e) {
    logger.error("FileTransferModal: failed to resolve folder path", e);
  }
};

// Search mode

const searchQuery = ref("");
const searchResults = ref<DirSummary[]>([]);
const isSearching = ref(false);

let searchRequest = 0;

const performSearch = useDebounceFn(async (query: string) => {
  const trimmed = query.trim();
  if (!trimmed) return;

  const request = ++searchRequest;

  try {
    const parsed = directorySearchApiSchema.parse({
      currentPage: 0,
      nameContains: trimmed,
      pageSize: 20,
    });
    const result = await directoryStore.searchDirectory(parsed);
    if (request !== searchRequest) return;

    searchResults.value = toDirSummaries(result.data?.items);
  } catch (e) {
    logger.error("FileTransferModal: search failed", e);
    appToast.error("Search failed", e);
  } finally {
    if (request === searchRequest) isSearching.value = false;
  }
}, 200);

const handleSearchInput = (val: string) => {
  if (val.trim()) {
    pickerMode.value = "search";
    isSearching.value = true;
    void performSearch(val);
    return;
  }

  clearSearch();
};

const clearSearch = () => {
  searchRequest++;
  searchQuery.value = "";
  pickerMode.value = "browse";
  searchResults.value = [];
  isSearching.value = false;
};

// Folder list

const ROW_CLASSES: Record<FolderState, string> = {
  blocked: "cursor-not-allowed",
  idle: "cursor-pointer hover:bg-black/[0.03] dark:hover:bg-white/[0.04]",
  selected: "cursor-pointer bg-primary/10",
};

const ICON_CLASSES: Record<FolderState, string> = {
  blocked: "text-gray-400 dark:text-gray-600",
  idle: "text-gray-500 dark:text-gray-400",
  selected: "text-primary",
};

const NAME_CLASSES: Record<FolderState, string> = {
  blocked: "text-gray-500 dark:text-gray-500",
  idle: "text-gray-900 dark:text-gray-100",
  selected: "font-medium text-gray-900 dark:text-gray-100",
};

const isListLoading = computed(() => {
  if (pickerMode.value === "search") return isSearching.value;
  return isLoadingDirs.value;
});

// Browsing inside a transferred folder: nothing below it is a valid destination.
const isInsideTransferred = computed(
  () =>
    pickerMode.value === "browse" && browsePath.value.some((segment) => isTransferred(segment.id)),
);

const folderState = (id: string | null): FolderState => {
  if (isTransferred(id) || isInsideTransferred.value) return "blocked";
  if (destinationDirId.value === id) return "selected";
  return "idle";
};

const folderHint = (id: string | null): string | null => {
  if (isTransferred(id)) return mode === "move" ? "Being moved" : "Being copied";
  if (id === originDirId) return "Current location";
  return null;
};

const toFolderRow = (dir: DirSummary): FolderRow => {
  const state = folderState(dir.id);

  return {
    canOpen: state !== "blocked",
    hint: folderHint(dir.id),
    icon: state === "selected" ? "mdi:folder" : "mdi:folder-outline",
    id: dir.id,
    key: dir.id,
    name: dir.name,
    state,
  };
};

const currentFolderRow = computed<FolderRow>(() => {
  const id = currentBrowseDirId.value;
  const isHome = id === null;
  let locationHint = folderHint(id);
  if (!isTransferred(id) && isInsideTransferred.value) {
    locationHint = mode === "move" ? "Inside a moved folder" : "Inside a copied folder";
  }

  let icon = "mdi:folder-open-outline";
  if (isHome) icon = "mdi:home-outline";

  return {
    canOpen: false,
    hint: locationHint ?? "This folder",
    icon,
    id,
    key: `current-${id ?? "home"}`,
    name: currentBrowseDirName.value,
    state: folderState(id),
  };
});

const folderRows = computed<FolderRow[]>(() => {
  if (pickerMode.value === "search") return searchResults.value.map(toFolderRow);
  return [currentFolderRow.value, ...browseSubdirs.value.map(toFolderRow)];
});

const rowLabel = (row: FolderRow) => {
  if (row.hint) return `${row.name}, ${row.hint}`;
  return row.name;
};

const selectFolder = (row: FolderRow) => {
  if (row.state === "blocked") return;

  destinationDirId.value = row.id;
  destinationDirName.value = row.name;
};

const openFolder = (row: FolderRow) => {
  if (!row.canOpen || row.id === null) return;

  if (pickerMode.value === "search") {
    void browseIntoFromSearch(row.id, row.name);
    return;
  }

  browseNavigateTo(row.id, row.name);
};

const crumbClass = (isCurrent: boolean) => {
  if (isCurrent) return "font-medium text-gray-900 dark:text-gray-100";
  return "text-gray-600 dark:text-gray-400";
};

// Confirm

const isConfirming = ref(false);

const transferSubject = computed(() => {
  const [onlyChip] = allChips.value;
  if (totalCount.value === 1 && onlyChip) return quoted(onlyChip.name);

  return describeExplorerItems(files.length, directories.length);
});

const transferDestination = computed(() => destinationDirName.value ?? "Home");

const handleConfirm = async () => {
  if (destinationDirId.value === undefined || isSameDirectory.value) return;

  const destinationId = destinationDirId.value;
  isConfirming.value = true;

  try {
    if (mode === "move") {
      if (files.length > 0) {
        await moveFilesMutate({
          destinationId,
          fileIds: files,
          originId: originDirId,
        });
      }

      if (directories.length > 0) {
        await moveDirectoriesMutate({
          destinationId,
          directoryIds: directories,
          originId: originDirId,
        });
      }
    } else {
      if (files.length > 0) {
        await copyFilesMutate({
          destinationId,
          fileIds: files,
          originId: originDirId,
        });
      }

      if (directories.length > 0) {
        await Promise.all(
          directories.map((id) =>
            copyDirectoryMutate({
              destinationId,
              directoryId: id,
              originId: originDirId,
            }),
          ),
        );
      }
    }

    appToast.success(
      `${mode === "move" ? "Moved" : "Copied"} ${transferSubject.value} to ${transferDestination.value}`,
    );
    emit("close", { destinationId });
  } catch (e) {
    logger.error("FileTransferModal: confirm failed", e);

    appToast.error(`Couldn't ${mode} ${transferSubject.value} to ${transferDestination.value}`, e, {
      retry: handleConfirm,
    });
  } finally {
    isConfirming.value = false;
  }
};

onMounted(() => {
  void loadBrowseSubdirs(null);
});
</script>
