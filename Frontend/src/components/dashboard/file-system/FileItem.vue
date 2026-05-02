<template>
  <UDrawer
    :title="detail.fileName"
    :description="'Created ' + formatDate(detail.createdAt)"
    :direction="isMobile ? 'bottom' : 'right'"
    v-model:open="openDrawer"
    :ui="
      isMobile
        ? { container: 'h-[85vh] rounded-t-2xl' }
        : { container: 'md:max-w-[40rem] lg:min-w-[40rem]' }
    "
    :handle-only="!isMobile"
  >
    <!-- Grid View -->
    <UContextMenu
      v-if="viewMode === 'grid'"
      :items="contextMenuItems"
      :ui="{ content: 'lg:min-w-56' }"
    >
      <div class="relative group" tabindex="0">
        <UTooltip
          :disabled="isMobile"
          :delay-duration="600"
          :content="{ side: 'bottom', align: 'center' }"
          :ui="{
            content:
              'ring-0 h-auto p-0 rounded-md select-none data-[state=delayed-open]:animate-[scale-in_100ms_ease-out] data-[state=closed]:animate-[scale-out_100ms_ease-in] origin-(--reka-tooltip-content-transform-origin) pointer-events-auto',
          }"
        >
          <button
            type="button"
            class="w-full flex flex-col items-center gap-2 p-4 rounded-lg transition-colors cursor-pointer"
            :class="[
              isSelected
                ? 'bg-primary/20 ring-2 ring-primary'
                : 'hover:bg-primary/40 dark:hover:bg-primary/35',
            ]"
            @click="handleClick"
            @dblclick="handleDoubleClick"
            @contextmenu="emit('contextmenu', $event)"
          >
            <Icon
              :icon="getFileIcon(props.data.fileName)"
              :width="iconSize"
              :height="iconSize"
              class="shrink-0"
            />
            <span class="text-sm text-center line-clamp-2 w-full wrap-break-word">
              {{ props.data.fileName }}
            </span>
          </button>

          <template #content>
            <FileTooltipCard :data="props.data" />
          </template>
        </UTooltip>
      </div>
    </UContextMenu>

    <!-- List View -->
    <UContextMenu v-else :items="contextMenuItems">
      <div class="relative group" tabindex="0">
        <UTooltip
          :disabled="isMobile"
          :delay-duration="600"
          :content="{ side: 'top', align: 'center' }"
          :ui="{
            content:
              'z-50 ring-0 h-auto p-0 rounded-md select-none data-[state=delayed-open]:animate-[scale-in_100ms_ease-out] data-[state=closed]:animate-[scale-out_100ms_ease-in] origin-(--reka-tooltip-content-transform-origin) pointer-events-auto',
          }"
        >
          <button
            type="button"
            class="w-full flex items-center gap-3 px-4 py-2 transition-colors cursor-pointer text-left border-b last:border-b-0"
            :class="[
              isSelected
                ? 'bg-primary/20 ring-2 ring-primary'
                : 'hover:bg-primary/40 dark:hover:bg-primary/35',
            ]"
            @click="handleClick"
            @dblclick="handleDoubleClick"
            @contextmenu="emit('contextmenu', $event)"
          >
            <Icon
              :icon="getFileIcon(props.data.fileName)"
              :width="iconSize"
              :height="iconSize"
              class="shrink-0"
            />
            <span class="flex-1 truncate">{{ props.data.fileName }}</span>
            <span class="text-xs opacity-70 shrink-0 min-w-15 text-right">
              {{ formatBytes(Number(props.data.currentVersion.size)) }}
            </span>
          </button>

          <template #content>
            <FileTooltipCard :data="props.data" />
          </template>
        </UTooltip>
      </div>
    </UContextMenu>

    <template #body>
      <div class="flex flex-col gap-6 p-1">
        <!-- File Header Section -->
        <div
          class="flex items-center bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
          :class="isMobile ? 'gap-3 p-3' : 'gap-4 p-6'"
        >
          <div
            class="bg-neutral-100 dark:bg-neutral-800/50 rounded-lg shadow-sm shrink-0"
            :class="isMobile ? 'p-2' : 'p-4'"
          >
            <Icon
              :icon="getFileIcon(detail.fileName)"
              class="text-primary"
              :class="isMobile ? 'w-10 h-10' : 'w-16 h-16'"
            />
          </div>

          <div class="flex-1 min-w-0">
            <!-- File name — tappable on mobile, hover-reveals button on desktop -->
            <div
              class="flex items-center gap-1 group mb-1 min-w-0"
              :class="isMobile ? 'cursor-pointer active:opacity-60' : ''"
              @click="isMobile && copyWithFeedback(detail.fileName, 'File name')"
            >
              <h3 class="font-semibold truncate" :class="isMobile ? 'text-base' : 'text-lg'">
                {{ detail.fileName }}
              </h3>
              <UButton
                icon="i-mdi-content-copy"
                size="xs"
                variant="ghost"
                color="neutral"
                :class="
                  isMobile
                    ? 'shrink-0 opacity-50'
                    : 'shrink-0 opacity-0 group-hover:opacity-100 focus:opacity-100 transition-opacity'
                "
                aria-label="Copy file name"
                @click.stop="copyWithFeedback(detail.fileName, 'File name')"
              />
            </div>

            <div class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 w-full">
              <Icon icon="mdi-file" class="w-4 h-4 shrink-0" />
              <span
                class="text-ellipsis overflow-hidden"
                :class="isMobile ? 'line-clamp-1' : 'max-w-46 max-h-16 wrap-break-word'"
              >
                {{ getFileTypeReadable(detail.currentVersion.mimeType, detail.fileName) }}
              </span>
            </div>
          </div>
        </div>

        <!-- Tags Section -->
        <div class="flex flex-col gap-3">
          <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300">Tags</h4>

          <USkeleton v-if="fileTagsLoading && openDrawer" class="h-8 w-48 rounded-full" />

          <div
            v-else
            class="flex items-center gap-1.5"
            :class="isMobile ? 'flex-nowrap overflow-x-auto pb-1' : 'flex-wrap'"
          >
            <TagBadge
              v-for="tag in displayTags"
              :key="tag.id"
              :tag="tag"
              :file-id="props.data.fileId"
              class="shrink-0"
              @remove-tag="refreshOnRemove"
            />

            <span
              v-if="!displayTags?.length && !showTagSearch"
              class="text-xs text-gray-400 dark:text-gray-500 italic mr-1 shrink-0"
            >
              No tags
            </span>

            <UPopover v-model:open="showTagSearch" :content="{ side: 'bottom', align: 'start' }">
              <UButton
                label="Add tag"
                icon="i-mdi-plus"
                size="xs"
                variant="outline"
                color="neutral"
                class="rounded-full shrink-0"
              />

              <template #content>
                <div class="p-2 w-56">
                  <USelectMenu
                    :loading="tagsLoading"
                    :items="
                      tagsData?.items.map((t) => ({
                        label: t.name,
                        value: t.id,
                        icon: getIconByValue(t.icon),
                      }))
                    "
                    v-model:search-term="searchQuery"
                    @update:search-term="refreshFileTag()"
                    placeholder="Search tags…"
                    autofocus
                    @update:model-value="
                      async (tag) => {
                        await addTagMutate({
                          fileId: props.data.fileId,
                          data: { tagIds: [tag.value] },
                        });
                        showTagSearch = false;
                        searchQuery = '';
                        refreshFileTag();
                      }
                    "
                    class="w-full"
                  />
                </div>
              </template>
            </UPopover>
          </div>
        </div>

        <!-- File Preview Section -->
        <FilePreview
          v-if="!detail.currentVersion.isEncrypted"
          :file-id="props.data.fileId"
          :file-name="detail.fileName"
          :mime-type="detail.currentVersion.mimeType"
        />

        <!-- File Details Grid -->
        <div>
          <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">Details</h4>
          <div class="grid gap-4" :class="isMobile ? 'grid-cols-1' : 'grid-cols-2'">
            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
            >
              <UIcon name="i-heroicons-scale" class="w-8 h-8 text-gray-500 mt-0.5 shrink-0" />
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">Size</div>
                <div class="font-medium text-sm">
                  {{ formatBytes(Number(detail.currentVersion.size)) }}
                </div>
              </div>
            </div>

            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
            >
              <UIcon name="i-heroicons-archive-box" class="w-8 h-8 text-gray-500 mt-0.5 shrink-0" />
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">Version</div>
                <div class="font-medium text-sm">v{{ detail.currentVersion.versionNumber }}</div>
              </div>
            </div>

            <!-- File ID — tappable on mobile, hover-reveals button on desktop -->
            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
              :class="[isMobile ? 'cursor-pointer active:opacity-60' : 'col-span-2']"
              @click="isMobile && copyWithFeedback(detail.fileId, 'File ID')"
            >
              <Icon icon="mdi-identifier" class="w-8 h-8 text-gray-500 mt-0.5 shrink-0" />
              <div class="min-w-0 flex-1">
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">File ID</div>
                <div class="flex items-center gap-1 group min-w-0">
                  <span class="font-mono text-sm truncate">{{ detail.fileId }}</span>
                  <UButton
                    icon="i-mdi-content-copy"
                    size="xs"
                    variant="ghost"
                    color="neutral"
                    :class="
                      isMobile
                        ? 'shrink-0 opacity-50 ml-auto'
                        : 'shrink-0 opacity-0 group-hover:opacity-100 focus:opacity-100 transition-opacity'
                    "
                    aria-label="Copy file ID"
                    @click.stop="copyWithFeedback(detail.fileId, 'File ID')"
                  />
                </div>
              </div>
            </div>

            <!-- col-span-2 on desktop only; on mobile the grid is single-column anyway -->
            <div v-if="!isMobile" class="col-span-2 hidden" />
          </div>
        </div>

        <!-- Owner Section -->
        <UCard :ui="isMobile ? { body: 'p-3' } : {}">
          <template #header>
            <div class="flex items-center gap-2" :class="isMobile ? 'p-3 pb-0' : ''">
              <Icon icon="mdi-account" class="w-5 h-5 text-primary" />
              <span class="font-semibold text-sm">Owner</span>
            </div>
          </template>
          <div class="flex items-center gap-3">
            <UAvatar :alt="detail.owner.name" :size="isMobile ? 'md' : 'lg'" />
            <div class="min-w-0 flex-1">
              <div class="font-medium text-sm">{{ detail.owner.name }}</div>
              <!-- Email — tappable on mobile, hover-reveals button on desktop -->
              <div
                class="text-sm flex items-center gap-1.5 mt-0.5 text-gray-600 dark:text-gray-400 group"
                :class="isMobile ? 'cursor-pointer active:opacity-60' : ''"
                @click="isMobile && copyWithFeedback(detail.owner.email, 'Email')"
              >
                <Icon icon="mdi-email" class="w-4 h-4 text-primary shrink-0" />
                <span class="truncate">{{ detail.owner.email }}</span>
                <UButton
                  icon="i-mdi-content-copy"
                  size="xs"
                  variant="ghost"
                  color="neutral"
                  :class="
                    isMobile
                      ? 'shrink-0 opacity-50 ml-auto'
                      : 'shrink-0 opacity-0 group-hover:opacity-100 focus:opacity-100 transition-opacity ml-auto'
                  "
                  aria-label="Copy owner email"
                  @click.stop="copyWithFeedback(detail.owner.email, 'Email')"
                />
              </div>
            </div>
          </div>
        </UCard>

        <!-- Versions Section -->
        <FileVersionHistory
          :file-id="props.data.fileId"
          :file-name="detail.fileName"
          :current-version-id="detail.currentVersion.id"
          :current-version-number="detail.currentVersion.versionNumber"
          @versions-changed="refreshDetail"
        />
      </div>
    </template>
  </UDrawer>
</template>

<script setup lang="ts">
import { type FileResult } from "@/api/file";
import type { TagDto } from "@/api/tag";
import { addTagToFile, removeTagFromFile } from "@/mutations/tags";
import { getFile } from "@/queries/files";
import { getTagsForFile, searchTag } from "@/queries/tags";
import type { SearchTagsSchema } from "@/schemas/tag";
import { useSettingsStore } from "@/stores/settings";
import { formatDate } from "@/utils/date-formatters";
import { getFileIcon, getIconByValue } from "@/utils/icon.utils";
import { getFileTypeReadable } from "@/utils/mimetype.utils";
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { breakpointsTailwind, useBreakpoints, useClipboard } from "@vueuse/core";
import { computed, ref, watch } from "vue";
import FilePreview from "./FilePreview.vue";
import FileTooltipCard from "./FileTooltipCard.vue";
import FileVersionHistory from "./FileVersionHistory.vue";
import { formatBytes } from "@/utils/size.utils";

const settingsStore = useSettingsStore();

const breakpoints = useBreakpoints(breakpointsTailwind);
const isMobile = breakpoints.smaller("md");

const toast = useToast();
const { copy } = useClipboard();

const copyWithFeedback = async (value: string, label: string) => {
  await copy(value);
  toast.add({
    color: "success",
    duration: 2000,
    icon: "i-mdi-check-circle",
    title: `${label} copied`,
    // Bottom-right on desktop; top-center on mobile so it clears the bottom-sheet drawer
  });
};

const props = defineProps<{
  data: FileResult;
  viewMode: "grid" | "list";
  isSelected: boolean;
  selectedCount?: number;
  tags: TagDto[] | undefined;
}>();

defineExpose({
  openDetails: () => {
    openDrawer.value = true;
  },
});

const iconSize = computed(() =>
  props.viewMode === "grid" ? settingsStore.gridIconSize : settingsStore.listIconSize,
);

const openDrawer = ref(false);

// File detail query

const { data: fileDetail, refresh: refreshDetail } = useQuery({
  ...getFile(props.data.fileId),
  enabled: openDrawer.value,
});
const detail = computed(() => fileDetail.value ?? props.data);

// Tag queries & mutations

const { mutateAsync: addTagMutate } = addTagToFile();
const { mutateAsync: removeTagMutateAsync } = removeTagFromFile();

const currentPage = ref(1);
const pageSize = ref(25);
const searchQuery = ref("");

const searchFilters = computed<SearchTagsSchema>(() => ({
  excludeOnFile: props.data.fileId,
  name: searchQuery.value || undefined,
  page: currentPage.value,
  pageSize: pageSize.value,
}));

const {
  data: tagsData,
  isLoading: tagsLoading,
  refresh: refreshFileTag,
} = useQuery({ ...searchTag(searchFilters.value), enabled: openDrawer.value });

const { data: fileTags, isLoading: fileTagsLoading } = useQuery({
  ...getTagsForFile(props.data.fileId),
  enabled: () => openDrawer.value,
});

const displayTags = computed((): TagDto[] => {
  if (fileTags.value?.tags) return fileTags.value.tags;

  if (openDrawer.value && fileDetail.value?.tags) return fileDetail.value.tags;

  return props.data.tags ?? [];
});

const showTagSearch = ref(false);

const refreshOnRemove = async (id: string) => {
  await removeTagMutateAsync({ fileId: props.data.fileId, tagId: id });
  refreshFileTag();
};

// Emits & context menu

const emit = defineEmits<{
  click: [event: MouseEvent];
  open: [fileId: string];
  rename: [fileId: string, originalName: string];
  delete: [fileIds: string[]];
  move: [fileIds: string[]];
  copy: [fileIds: string[]];
  download: [fileIds: string[]];
  preview: [fileId: string];
  share: [fileIds: string[]];
  "file-trashed": [fileId: string];
  "file-restored": [];
  contextmenu: [fileId: PointerEvent];
}>();

watch(
  () => fileDetail.value?.deletedAt,
  (current, prev) => {
    if (prev === undefined) return;
    if (current) {
      openDrawer.value = false;
      setTimeout(() => emit("file-trashed", props.data.fileId), 300);
      return;
    }
    if (prev) {
      openDrawer.value = false;
      setTimeout(() => emit("file-restored"), 300);
    }
  },
);

const canRename = (): boolean => true;
const canMove = (): boolean => true;
const canCopy = (): boolean => true;
const canDownload = (): boolean => true;
const canShare = (): boolean => true;
const canDelete = (): boolean => true;

const singleSelectMenuItems = [
  [
    {
      icon: "i-mdi-information-outline",
      label: "View details",
      kbds: [{ value: "alt" }, { value: "i" }],
      onSelect: () => {
        openDrawer.value = true;
      },
    },
    {
      disabled: !canDownload(),
      icon: "i-mdi-download-outline",
      kbds: [{ value: "⌘" }, { value: "S" }],
      label: "Download",
      onSelect: () => emit("download", [props.data.fileId]),
    },
  ],
  [
    {
      disabled: !canRename(),
      icon: "i-mdi-pencil-outline",
      kbds: ["F2"],
      label: "Rename",
      onSelect: () => emit("rename", props.data.fileId, props.data.fileName),
    },
    {
      disabled: !canMove(),
      icon: "i-mdi-folder-move-outline",
      label: "Move to…",
      onSelect: () => emit("move", [props.data.fileId]),
    },
    {
      disabled: !canCopy(),
      icon: "i-mdi-content-copy",
      kbds: ["⌘", "C"],
      label: "Copy to…",
      onSelect: () => emit("copy", [props.data.fileId]),
    },
  ],
  [
    {
      disabled: !canShare(),
      icon: "i-mdi-share-variant-outline",
      label: "Share",
      onSelect: () => emit("share", [props.data.fileId]),
    },
    {
      icon: "i-mdi-identifier",
      label: "Copy file ID",
      onSelect: () => copyWithFeedback(props.data.fileId, "File ID"),
    },
  ],
  [
    {
      color: "error" as const,
      disabled: !canDelete(),
      icon: "i-mdi-delete-outline",
      kbds: ["Del"],
      label: "Delete",
      onSelect: () => emit("delete", [props.data.fileId]),
    },
  ],
];

const contextMenuItems = computed(() => {
  const isMultiSelect = (props.selectedCount ?? 0) > 1;
  const count = props.selectedCount ?? 1;

  if (!isMultiSelect) {
    return singleSelectMenuItems;
  }

  return [
    [
      {
        label: `${count} items selected`,
        type: "label" as const,
      },
    ],
    [
      {
        disabled: !canDownload(),
        icon: "i-mdi-download-multiple-outline",
        label: "Download all",
        onSelect: () => emit("download", []),
      },
    ],
    [
      {
        disabled: !canMove(),
        icon: "i-mdi-folder-move-outline",
        label: "Move all to…",
        onSelect: () => emit("move", []),
      },
      {
        disabled: !canCopy(),
        icon: "i-mdi-content-copy",
        label: "Copy all to…",
        onSelect: () => emit("copy", []),
      },
      {
        disabled: !canShare(),
        icon: "i-mdi-share-variant-outline",
        label: "Share all",
        onSelect: () => emit("share", []),
      },
    ],
    [
      {
        color: "error" as const,
        disabled: !canDelete(),
        icon: "i-mdi-delete-sweep-outline",
        label: `Delete ${count} items`,
        onSelect: () => emit("delete", []),
      },
    ],
  ];
});

defineShortcuts(extractShortcuts(contextMenuItems.value));

// Interaction handlers

const handleClick = (event: MouseEvent) => emit("click", event);

const handleDoubleClick = () => {
  openDrawer.value = true;
};

// Watchers

watch(showTagSearch, (open) => {
  if (!open) searchQuery.value = "";
});
</script>

<style scoped></style>
