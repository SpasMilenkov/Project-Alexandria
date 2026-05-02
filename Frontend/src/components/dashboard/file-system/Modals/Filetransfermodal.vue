<template>
  <UModal
    :ui="{
      content: 'sm:max-w-2xl backdrop-blur-sm bg-white/85 dark:bg-neutral-900/85',
      header: 'border-b border-neutral-200/60 dark:border-neutral-700/60',
      footer: 'border-t border-neutral-200/60 dark:border-neutral-700/60',
    }"
    :close="{ onClick: () => emit('close', false) }"
  >
    <template #header>
      <div class="flex items-center gap-3">
        <div class="flex items-center justify-center w-8 h-8 rounded-lg bg-primary/10 shrink-0">
          <Icon
            :icon="props.mode === 'move' ? 'mdi:folder-move-outline' : 'mdi:content-copy'"
            class="w-4 h-4 text-primary"
          />
        </div>
        <div>
          <h2 class="font-semibold text-sm leading-tight text-neutral-900 dark:text-neutral-100">
            {{ props.mode === "move" ? "Move" : "Copy" }}
            {{ totalCount }} {{ totalCount === 1 ? "item" : "items" }}
          </h2>
          <p class="text-xs text-neutral-500 dark:text-neutral-400 mt-0.5">
            {{
              props.mode === "move"
                ? "Choose where to move these items"
                : "Choose where to copy these items"
            }}
          </p>
        </div>
      </div>
    </template>

    <template #body>
      <div class="flex flex-col gap-5 p-1">
        <!-- Transfer visualization -->
        <div class="flex items-stretch gap-3">
          <!-- Origin panel -->
          <div
            class="flex-1 min-w-0 flex flex-col gap-2 p-3 rounded-xl border border-neutral-200/60 dark:border-neutral-700/60 bg-white/50 dark:bg-white/5"
          >
            <div class="flex items-center gap-1.5 mb-1">
              <Icon
                icon="mdi:folder-outline"
                class="w-4 h-4 text-neutral-400 dark:text-neutral-500 shrink-0"
              />
              <span
                class="text-xs font-medium text-neutral-500 dark:text-neutral-400 truncate uppercase tracking-wide"
              >
                From
              </span>
            </div>
            <p
              class="text-xs font-semibold text-neutral-700 dark:text-neutral-200 truncate -mt-1 mb-1"
            >
              {{ props.originDirName ?? "Home" }}
            </p>

            <div class="flex flex-col gap-1">
              <TransitionGroup name="item-leave" tag="div" class="flex flex-col gap-1">
                <div
                  v-for="chip in visibleOriginChips"
                  :key="chip.id"
                  class="flex items-center gap-2 px-2 py-1.5 rounded-lg border border-neutral-200/50 dark:border-neutral-700/50 transition-all duration-300"
                  :class="
                    props.mode === 'move'
                      ? 'opacity-35 bg-neutral-100/60 dark:bg-neutral-800/40'
                      : 'opacity-100 bg-white/60 dark:bg-white/5'
                  "
                >
                  <Icon :icon="chip.icon" class="w-4 h-4 shrink-0 text-neutral-400" />
                  <span
                    class="text-xs truncate text-neutral-600 dark:text-neutral-300"
                    :class="props.mode === 'move' ? 'line-through' : ''"
                  >
                    {{ chip.name }}
                  </span>
                </div>
              </TransitionGroup>

              <div
                v-if="overflowCount > 0"
                class="flex items-center gap-1.5 px-2 py-1 rounded-lg bg-neutral-100/60 dark:bg-neutral-800/40"
              >
                <Icon icon="mdi:dots-horizontal" class="w-3.5 h-3.5 text-neutral-400" />
                <span class="text-xs text-neutral-400 dark:text-neutral-500"
                  >+{{ overflowCount }} more</span
                >
              </div>
            </div>
          </div>

          <!-- Arrow -->
          <div class="flex flex-col items-center justify-center shrink-0 gap-1.5 px-1">
            <Icon
              icon="mdi:arrow-right"
              class="w-5 h-5 transition-colors duration-300"
              :class="destinationDirId ? 'text-primary' : 'text-neutral-300 dark:text-neutral-600'"
            />
            <span
              class="text-[10px] font-semibold uppercase tracking-widest transition-colors duration-300"
              :class="destinationDirId ? 'text-primary' : 'text-neutral-300 dark:text-neutral-600'"
            >
              {{ props.mode === "move" ? "move" : "copy" }}
            </span>
          </div>

          <!-- Destination panel -->
          <div
            class="flex-1 min-w-0 flex flex-col gap-2 p-3 rounded-xl border transition-colors duration-300"
            :class="
              destinationDirId
                ? 'border-primary/40 bg-primary/5 dark:bg-primary/10'
                : 'border-neutral-200/60 dark:border-neutral-700/60 bg-white/50 dark:bg-white/5'
            "
          >
            <div class="flex items-center gap-1.5 mb-1">
              <Icon
                icon="mdi:folder-outline"
                class="w-4 h-4 shrink-0 transition-colors duration-300"
                :class="
                  destinationDirId ? 'text-primary' : 'text-neutral-400 dark:text-neutral-500'
                "
              />
              <span
                class="text-xs font-medium uppercase tracking-wide transition-colors duration-300"
                :class="
                  destinationDirId ? 'text-primary' : 'text-neutral-500 dark:text-neutral-400'
                "
              >
                To
              </span>
            </div>
            <p
              class="text-xs font-semibold truncate -mt-1 mb-1 transition-colors duration-300"
              :class="
                destinationDirId ? 'text-primary' : 'text-neutral-400 dark:text-neutral-500 italic'
              "
            >
              {{ destinationDirName ?? "No folder selected" }}
            </p>

            <div class="flex flex-col gap-1 min-h-0">
              <TransitionGroup name="item-enter" tag="div" class="flex flex-col gap-1">
                <div
                  v-if="destinationDirId !== undefined"
                  v-for="chip in visibleOriginChips"
                  :key="'dest-' + chip.id"
                  class="flex items-center gap-2 px-2 py-1.5 rounded-lg bg-white/60 dark:bg-white/5 border border-primary/20"
                >
                  <Icon :icon="chip.icon" class="w-4 h-4 shrink-0 text-primary/70" />
                  <span class="text-xs truncate text-neutral-700 dark:text-neutral-200">{{
                    chip.name
                  }}</span>
                </div>
              </TransitionGroup>

              <div
                v-if="destinationDirId === undefined"
                class="flex flex-col items-center justify-center py-4 gap-1 opacity-40"
              >
                <Icon icon="mdi:folder-arrow-down-outline" class="w-7 h-7 text-neutral-400" />
              </div>

              <div
                v-else-if="overflowCount > 0"
                class="flex items-center gap-1.5 px-2 py-1 rounded-lg bg-primary/10"
              >
                <Icon icon="mdi:dots-horizontal" class="w-3.5 h-3.5 text-primary/60" />
                <span class="text-xs text-primary/60">+{{ overflowCount }} more</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Same-directory warning -->
        <Transition name="fade-status">
          <div
            v-if="isSameDirectory"
            class="flex items-center gap-2 px-3 py-2 rounded-lg bg-amber-50/80 dark:bg-amber-900/20 border border-amber-200/70 dark:border-amber-700/40 text-xs text-amber-700 dark:text-amber-400"
          >
            <Icon icon="mdi:alert-circle-outline" class="w-4 h-4 shrink-0" />
            Items are already in this folder.
          </div>
        </Transition>

        <!-- Divider -->
        <div class="w-full h-px bg-neutral-200/60 dark:bg-neutral-700/60" />

        <!-- Destination picker -->
        <div class="flex flex-col gap-3">
          <p
            class="text-xs font-semibold text-neutral-500 dark:text-neutral-400 uppercase tracking-widest"
          >
            Choose destination
          </p>

          <!-- Search input -->
          <UInput
            ref="searchInputRef"
            v-model="searchQuery"
            placeholder="Search directories…"
            icon="i-mdi-magnify"
            size="sm"
            :loading="isSearching"
            autofocus
            @update:model-value="handleSearchInput"
          >
            <template v-if="searchQuery" #trailing>
              <UButton
                icon="i-mdi-close"
                size="xs"
                variant="ghost"
                color="neutral"
                aria-label="Clear search"
                @click="clearSearch"
              />
            </template>
          </UInput>

          <!-- Browse mode: breadcrumb strip -->
          <Transition name="fade-status">
            <div v-if="pickerMode === 'browse'" class="flex items-center gap-1 flex-wrap">
              <button
                class="flex items-center gap-1 px-2 py-1 rounded-md text-xs transition-colors hover:bg-neutral-100/60 dark:hover:bg-neutral-800/60"
                :class="
                  browsePath.length === 0
                    ? 'text-primary font-semibold bg-primary/8'
                    : 'text-neutral-500 dark:text-neutral-400'
                "
                @click="browseNavigateTo(null, 'Home')"
              >
                <Icon icon="mdi:home-outline" class="w-3.5 h-3.5" />
                <span>Home</span>
              </button>

              <template v-for="(seg, idx) in browsePath" :key="seg.id">
                <Icon
                  icon="mdi:chevron-right"
                  class="w-3.5 h-3.5 text-neutral-300 dark:text-neutral-600"
                />
                <button
                  class="flex items-center gap-1 px-2 py-1 rounded-md text-xs transition-colors hover:bg-neutral-100/60 dark:hover:bg-neutral-800/60"
                  :class="
                    idx === browsePath.length - 1
                      ? 'text-primary font-semibold bg-primary/8'
                      : 'text-neutral-500 dark:text-neutral-400'
                  "
                  @click="browseNavigateTo(seg.id, seg.name)"
                >
                  {{ seg.name }}
                </button>
              </template>
            </div>
          </Transition>

          <!-- Results / subdirectory list -->
          <div
            class="flex flex-col rounded-xl border border-neutral-200/60 dark:border-neutral-700/60 overflow-hidden bg-white/40 dark:bg-white/3"
            style="min-height: 180px; max-height: 240px; overflow-y: auto"
          >
            <!-- Loading skeleton -->
            <div v-if="isLoadingDirs" class="flex flex-col gap-2 p-3">
              <div v-for="n in 3" :key="n" class="flex items-center gap-3 px-2 py-2">
                <USkeleton class="w-6 h-6 rounded-md shrink-0" />
                <USkeleton class="h-3 rounded flex-1" />
              </div>
            </div>

            <!-- Search results -->
            <template v-else-if="pickerMode === 'search'">
              <div
                v-if="searchResults.length === 0"
                class="flex flex-col items-center justify-center gap-2 py-10 text-center"
              >
                <Icon
                  icon="mdi:folder-search-outline"
                  class="w-10 h-10 text-neutral-300 dark:text-neutral-600"
                />
                <p class="text-sm text-neutral-400 dark:text-neutral-500">No directories found</p>
                <p class="text-xs text-neutral-300 dark:text-neutral-600">
                  Try a different search term
                </p>
              </div>

              <button
                v-for="dir in searchResults"
                :key="dir.id"
                class="flex items-center gap-3 px-4 py-2.5 text-left transition-colors border-b last:border-b-0 border-neutral-100/60 dark:border-neutral-800/60"
                :class="
                  destinationDirId === dir.id
                    ? 'bg-primary/10 text-primary'
                    : 'hover:bg-neutral-100/60 dark:hover:bg-neutral-800/60 text-neutral-700 dark:text-neutral-200'
                "
                @click="selectDestination(dir.id, dir.name)"
              >
                <Icon
                  :icon="destinationDirId === dir.id ? 'mdi:folder-check' : 'mdi:folder-outline'"
                  class="w-5 h-5 shrink-0"
                  :class="
                    destinationDirId === dir.id
                      ? 'text-primary'
                      : 'text-neutral-400 dark:text-neutral-500'
                  "
                />
                <div class="flex-1 min-w-0">
                  <span class="text-sm font-medium truncate block">{{ dir.name }}</span>
                  <span
                    class="text-xs text-neutral-400 dark:text-neutral-500 truncate block"
                    v-if="dir.parentId"
                  >
                    ID: {{ dir.id.slice(0, 8) }}…
                  </span>
                </div>
                <Icon
                  v-if="destinationDirId === dir.id"
                  icon="mdi:check-circle"
                  class="w-4 h-4 text-primary shrink-0"
                />
                <UButton
                  v-else
                  icon="i-mdi-arrow-right"
                  size="xs"
                  variant="ghost"
                  color="neutral"
                  title="Browse into this folder"
                  @click.stop="browseIntoFromSearch(dir.id, dir.name)"
                />
              </button>
            </template>

            <!-- Browse mode -->
            <template v-else>
              <!-- "Select this folder" current dir row -->
              <button
                class="flex items-center gap-3 px-4 py-2.5 text-left transition-colors border-b border-neutral-100/60 dark:border-neutral-800/60"
                :class="
                  destinationDirId === currentBrowseDirId
                    ? 'bg-primary/10'
                    : 'hover:bg-neutral-100/60 dark:hover:bg-neutral-800/60'
                "
                @click="selectCurrentBrowseDir"
              >
                <Icon
                  :icon="
                    destinationDirId === currentBrowseDirId
                      ? 'mdi:folder-check'
                      : 'mdi:folder-open-outline'
                  "
                  class="w-5 h-5 shrink-0"
                  :class="
                    destinationDirId === currentBrowseDirId
                      ? 'text-primary'
                      : 'text-neutral-400 dark:text-neutral-500'
                  "
                />
                <span
                  class="text-sm font-medium flex-1 truncate"
                  :class="
                    destinationDirId === currentBrowseDirId
                      ? 'text-primary'
                      : 'text-neutral-700 dark:text-neutral-200'
                  "
                >
                  {{ currentBrowseDirName }}
                </span>
                <Icon
                  v-if="destinationDirId === currentBrowseDirId"
                  icon="mdi:check-circle"
                  class="w-4 h-4 text-primary shrink-0"
                />
              </button>

              <!-- Subdirectories -->
              <div
                v-if="browseSubdirs.length === 0 && !isLoadingDirs"
                class="flex flex-col items-center justify-center gap-1.5 py-8 text-center"
              >
                <Icon
                  icon="mdi:folder-outline"
                  class="w-8 h-8 text-neutral-300 dark:text-neutral-600"
                />
                <p class="text-xs text-neutral-400 dark:text-neutral-500">No subfolders</p>
              </div>

              <button
                v-for="dir in browseSubdirs"
                :key="dir.id"
                class="flex items-center gap-3 px-4 py-2.5 text-left w-full transition-colors border-b last:border-b-0 border-neutral-100/60 dark:border-neutral-800/60"
                :class="
                  destinationDirId === dir.id
                    ? 'bg-primary/10'
                    : 'hover:bg-neutral-100/60 dark:hover:bg-neutral-800/60'
                "
                @click="selectDestination(dir.id, dir.name)"
              >
                <Icon
                  :icon="destinationDirId === dir.id ? 'mdi:folder-check' : 'mdi:folder-outline'"
                  class="w-5 h-5 shrink-0"
                  :class="
                    destinationDirId === dir.id
                      ? 'text-primary'
                      : 'text-neutral-400 dark:text-neutral-500'
                  "
                />
                <span
                  class="text-sm flex-1 truncate font-medium"
                  :class="
                    destinationDirId === dir.id
                      ? 'text-primary'
                      : 'text-neutral-700 dark:text-neutral-200'
                  "
                >
                  {{ dir.name }}
                </span>
                <div class="flex items-center gap-1 shrink-0">
                  <Icon
                    v-if="destinationDirId === dir.id"
                    icon="mdi:check-circle"
                    class="w-4 h-4 text-primary"
                  />
                  <UButton
                    icon="i-mdi-chevron-right"
                    size="xs"
                    variant="ghost"
                    color="neutral"
                    title="Browse into this folder"
                    @click.stop="browseNavigateTo(dir.id, dir.name)"
                  />
                </div>
              </button>
            </template>
          </div>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex items-center justify-between gap-3">
        <div class="text-xs text-neutral-400 dark:text-neutral-500 min-w-0">
          <template v-if="destinationDirId && !isSameDirectory">
            <Icon icon="mdi:check-circle-outline" class="w-3.5 h-3.5 inline mr-1 text-primary" />
            <span class="text-neutral-600 dark:text-neutral-300">
              {{ props.mode === "move" ? "Moving to" : "Copying to" }}
              <strong>{{ destinationDirName }}</strong>
            </span>
          </template>
          <template v-else-if="!destinationDirId">
            <span class="opacity-60">Select a destination folder above</span>
          </template>
        </div>

        <div class="flex items-center gap-2 shrink-0">
          <UButton variant="outline" color="neutral" size="sm" @click="emit('close', false)">
            Cancel
          </UButton>
          <UButton
            variant="solid"
            color="primary"
            size="sm"
            :loading="isConfirming"
            :disabled="destinationDirId === undefined || isSameDirectory"
            @click="handleConfirm"
          >
            <Icon
              :icon="props.mode === 'move' ? 'mdi:folder-move-outline' : 'mdi:content-copy'"
              class="w-4 h-4 mr-1.5"
            />
            {{ props.mode === "move" ? "Move here" : "Copy here" }}
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed, onMounted, ref, watch } from "vue";
import { useDebounceFn } from "@vueuse/core";
import { useDirectoryStore } from "@/stores/directory";
import { copyFiles, moveFiles } from "@/mutations/files";
import { copyDirectory, moveDirectories } from "@/mutations/directories";
import { useFileStore } from "@/stores/file";
import { logger } from "@/utils/logger";
import { directorySearchApiSchema } from "@/schemas/search";
import { useAppToast } from "@/composables/useAppToast";

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
  parentId?: string | null;
}

const props = defineProps<{
  mode: "move" | "copy";
  files: string[];
  directories: string[];
  originDirId: string | null;
  originDirName: string | undefined;
  fileChips?: FileChip[];
  dirChips?: FileChip[];
}>();

const emit = defineEmits<{
  close: [result: string | false];
}>();

// Stores & mutations
const appToast = useAppToast();
const directoryStore = useDirectoryStore();
const fileStore = useFileStore();

const { mutateAsync: moveFilesMutate } = moveFiles();
const { mutateAsync: copyFilesMutate } = copyFiles();
const { mutateAsync: moveDirectoriesMutate } = moveDirectories();
const { mutateAsync: copyDirectoryMutate } = copyDirectory();

// Item chips

const MAX_VISIBLE = 4;

const allChips = computed<FileChip[]>(() => {
  const dirs: FileChip[] =
    props.dirChips ??
    props.directories.map((id) => ({
      icon: "mdi:folder-outline",
      id,
      name: "Folder",
      type: "directory" as const,
    }));
  const files: FileChip[] =
    props.fileChips ??
    props.files.map((id) => ({
      icon: "mdi:file-outline",
      id,
      name: "File",
      type: "file" as const,
    }));
  return [...dirs, ...files];
});

const totalCount = computed(() => props.files.length + props.directories.length);
const visibleOriginChips = computed(() => allChips.value.slice(0, MAX_VISIBLE));
const overflowCount = computed(() => Math.max(0, totalCount.value - MAX_VISIBLE));

// Destination state

const destinationDirId = ref<string | null | undefined>(undefined);
const destinationDirName = ref<string | null>(null);

const isSameDirectory = computed(
  () => destinationDirId.value !== undefined && destinationDirId.value === props.originDirId,
);

const selectDestination = (id: string | null, name: string) => {
  destinationDirId.value = id;
  destinationDirName.value = name;
};

// Browse mode

type PickerMode = "browse" | "search";

const pickerMode = ref<PickerMode>("browse");
const browsePath = ref<BrowseSegment[]>([]);
const browseSubdirs = ref<DirSummary[]>([]);
const isLoadingDirs = ref(false);

const currentBrowseDirId = computed<string | null>(
  () => browsePath.value[browsePath.value.length - 1]?.id ?? null,
);
const currentBrowseDirName = computed(
  () => browsePath.value[browsePath.value.length - 1]?.name ?? "Home",
);

const selectCurrentBrowseDir = () => {
  selectDestination(currentBrowseDirId.value ?? null, currentBrowseDirName.value);
};

// In loadBrowseSubdirs — same fix
const loadBrowseSubdirs = async (parentId: string | null) => {
  isLoadingDirs.value = true;
  browseSubdirs.value = [];
  try {
    const parsed = directorySearchApiSchema.parse({
      parentDirectoryId: parentId ?? undefined,
      currentPage: 0,
      pageSize: 50,
    });
    const result = await directoryStore.searchDirectory(parsed);
    browseSubdirs.value = (result.data?.items ?? []).map((d: any) => ({
      id: d.id,
      name: d.name,
      parentId: d.parentId,
    }));
  } catch (e) {
    logger.error("FileTransferModal: failed to load subdirs", e);
    appToast.error("Couldn't load folders", e);
  } finally {
    isLoadingDirs.value = false;
  }
};

const browseNavigateTo = (id: string | null, name: string) => {
  if (id === null) {
    browsePath.value = [];
  } else {
    const existingIdx = browsePath.value.findIndex((s) => s.id === id);
    if (existingIdx !== -1) {
      browsePath.value = browsePath.value.slice(0, existingIdx + 1);
    } else {
      browsePath.value = [...browsePath.value, { id, name }];
    }
  }
  loadBrowseSubdirs(id);
};

const browseIntoFromSearch = (id: string, name: string) => {
  clearSearch();
  browseNavigateTo(id, name);
};

// Search mode

const searchQuery = ref("");
const searchResults = ref<DirSummary[]>([]);
const isSearching = ref(false);

const performSearch = useDebounceFn(async (query: string) => {
  if (!query.trim()) return;
  isSearching.value = true;
  try {
    const parsed = directorySearchApiSchema.parse({
      currentPage: 0,
      nameContains: query.trim(),
      pageSize: 20,
    });
    const result = await directoryStore.searchDirectory(parsed);
    searchResults.value = (result.data?.items ?? []).map((d: any) => ({
      id: d.id,
      name: d.name,
      parentId: d.parentId,
    }));
  } catch (e) {
    logger.error("FileTransferModal: search failed", e);
    appToast.error("Search failed", e);
  } finally {
    isSearching.value = false;
  }
}, 200);

const handleSearchInput = (val: string) => {
  if (val.trim()) {
    pickerMode.value = "search";
    performSearch(val);
  } else {
    pickerMode.value = "browse";
    searchResults.value = [];
  }
};

const clearSearch = () => {
  searchQuery.value = "";
  pickerMode.value = "browse";
  searchResults.value = [];
};

// Confirm

const isConfirming = ref(false);

const handleConfirm = async () => {
  if (destinationDirId.value === undefined || isSameDirectory.value) return;
  isConfirming.value = true;

  try {
    if (props.mode === "move") {
      if (props.files.length > 0) {
        await moveFilesMutate({
          destinationId: destinationDirId.value,
          fileIds: props.files,
          originId: props.originDirId,
        });
      }
      if (props.directories.length > 0) {
        await moveDirectoriesMutate({
          destinationId: destinationDirId.value,
          directoryIds: props.directories,
          originId: props.originDirId,
        });
      }
    } else {
      if (props.files.length > 0) {
        await copyFilesMutate({
          destinationId: destinationDirId.value,
          fileIds: props.files,
          originId: props.originDirId,
        });
      }
      if (props.directories.length > 0) {
        await Promise.all(
          props.directories.map((id) =>
            copyDirectoryMutate({
              destinationId: destinationDirId.value!,
              directoryId: id,
              originId: props.originDirId,
            }),
          ),
        );
      }
    }
    emit("close", destinationDirId?.value ?? false);
  } catch (e) {
    logger.error("FileTransferModal: confirm failed", e);
    const action = props.mode === "move" ? "Move" : "Copy";
    const count = totalCount.value;
    const label = count === 1 ? "item" : "items";
    appToast.error(
      `${action} failed`,
      e ?? `Could not ${props.mode} ${count} ${label} to ${destinationDirName.value ?? "Home"}.`,
    );
  } finally {
    isConfirming.value = false;
  }
};

// Init

onMounted(() => {
  loadBrowseSubdirs(null);
});
</script>

<style scoped>
.item-leave-enter-active {
  transition: all 0.25s ease-out;
}
.item-leave-leave-active {
  transition: all 0.2s ease-in;
}
.item-leave-enter-from {
  opacity: 0;
  transform: translateX(-6px);
}
.item-leave-leave-to {
  opacity: 0;
  transform: translateX(6px);
}

.item-enter-enter-active {
  transition: all 0.3s ease-out;
}
.item-enter-enter-from {
  opacity: 0;
  transform: translateY(6px);
}

.fade-status-enter-active {
  transition: all 0.2s ease-out;
}
.fade-status-leave-active {
  transition: all 0.15s ease-in;
}
.fade-status-enter-from,
.fade-status-leave-to {
  opacity: 0;
  transform: translateY(-3px);
}
</style>
