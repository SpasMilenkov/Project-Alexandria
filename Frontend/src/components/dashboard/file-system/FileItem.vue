<template>
  <UDrawer
    :title="detail.fileName"
    :description="'Created ' + formatDate(detail.createdAt)"
    direction="right"
    v-model:open="openDrawer"
    :ui="{ container: 'md:max-w-[40rem] lg:min-w-[40rem]' }"
    :handle-only="true"
  >
    <!-- Grid View -->
    <UContextMenu v-if="viewMode === 'grid'" :items="contextMenuItems">
      <div class="relative group" tabindex="0">
        <UTooltip
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
          >
            <Icon
              :icon="getFileIcon(props.data.fileName)"
              :width="iconSize"
              :height="iconSize"
              class="shrink-0"
            />
            <span class="flex-1 truncate">{{ props.data.fileName }}</span>
            <span class="text-xs opacity-70 shrink-0 min-w-15 text-right">
              {{ formatFileSize(Number(props.data.currentVersion.size)) }}
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
        <div class="flex items-center gap-4 p-6 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg">
          <div class="p-4 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg shadow-sm">
            <Icon :icon="getFileIcon(detail.fileName)" class="w-16 h-16 text-primary" />
          </div>

          <div class="flex-1 min-w-0">
            <h3 class="font-semibold text-lg truncate mb-1">
              {{ detail.fileName }}
            </h3>
            <div class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 w-full">
              <Icon icon="mdi-file" class="w-4 h-4" />
              <span class="max-w-46 text-ellipsis max-h-16 wrap-break-word overflow-hidden">{{
                getFileTypeReadable(detail.currentVersion.mimeType, detail.fileName)
              }}</span>
            </div>
          </div>
        </div>

        <!-- Tags Section -->
        <div class="flex flex-col gap-3">
          <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300">Tags</h4>

          <USkeleton v-if="fileTagsLoading && openDrawer" class="h-8 w-48 rounded-full" />

          <div v-else class="flex flex-wrap items-center gap-1.5">
            <TagBadge
              v-for="tag in displayTags"
              :key="tag.id"
              :tag="tag"
              :file-id="props.data.fileId"
              @remove-tag="refreshOnRemove"
            />

            <span
              v-if="!displayTags?.length && !showTagSearch"
              class="text-xs text-gray-400 dark:text-gray-500 italic mr-1"
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
                class="rounded-full"
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
          :file-id="props.data.fileId"
          :file-name="detail.fileName"
          :mime-type="detail.currentVersion.mimeType"
        />

        <!-- File Details Grid -->
        <div>
          <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">Details</h4>
          <div class="grid grid-cols-2 gap-4">
            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
            >
              <UIcon name="i-heroicons-scale" class="w-10 h-10 text-gray-500 mt-0.5" />
              <div>
                <div class="text-xs mb-0.5">Size</div>
                <div class="font-medium">
                  {{ formatFileSize(Number(detail.currentVersion.size)) }}
                </div>
              </div>
            </div>

            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg"
            >
              <UIcon name="i-heroicons-archive-box" class="w-10 h-10 text-gray-500 mt-0.5" />
              <div>
                <div class="text-xs text-gray-500 dark:text-gray-400 mb-0.5">Version</div>
                <div class="font-medium">v{{ detail.currentVersion.versionNumber }}</div>
              </div>
            </div>

            <div
              class="flex items-start gap-3 p-3 bg-neutral-100 dark:bg-neutral-800/50 rounded-lg col-span-2"
            >
              <Icon icon="mdi-identifier" class="w-10 h-10 text-gray-500 mt-0.5" />
              <div class="min-w-0 flex-1">
                <div class="text-xs mb-0.5">File ID</div>
                <div class="font-mono text-sm truncate">
                  {{ detail.fileId }}
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Owner Section -->
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <Icon icon="mdi-account" class="w-8 h-8 text-primary" />
              <span class="font-semibold">Owner</span>
            </div>
          </template>
          <div class="flex items-center gap-3">
            <UAvatar :alt="detail.owner.name" size="lg" />
            <div>
              <div class="font-medium">{{ detail.owner.name }}</div>
              <div class="text-sm flex items-center gap-1.5 mt-0.5">
                <Icon color="primary" icon="mdi-email" class="w-4 h-4" />
                {{ detail.owner.email }}
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
import type { ContextMenuItem } from "@nuxt/ui";
import { useQuery } from "@pinia/colada";
import { computed, ref, watch } from "vue";
import FilePreview from "./FilePreview.vue";
import FileTooltipCard from "./FileTooltipCard.vue";
import FileVersionHistory from "./FileVersionHistory.vue";
import { logger } from "@/utils/logger";

const settingsStore = useSettingsStore();

const props = defineProps<{
  data: FileResult;
  viewMode: "grid" | "list";
  isSelected: boolean;
  selectedCount?: number;
  tags: TagDto[] | undefined;
}>();

const iconSize = computed(() =>
  props.viewMode === "grid" ? settingsStore.gridIconSize : settingsStore.listIconSize,
);

const openDrawer = ref(false);

// File detail query

const { data: fileDetail, refresh: refreshDetail } = useQuery(getFile(props.data.fileId));
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
} = useQuery(searchTag(searchFilters.value));

const { isLoading: fileTagsLoading } = useQuery({
  ...getTagsForFile(props.data.fileId),
  enabled: () => openDrawer.value,
});

const displayTags = computed(() =>
  openDrawer.value && fileDetail.value ? fileDetail.value.tags : props.data.tags,
);

const showTagSearch = ref(false);

const refreshOnRemove = async (id: string) => {
  await removeTagMutateAsync({ fileId: props.data.fileId, tagId: id });
  refreshFileTag();
};
// Emits & context menu

const emit = defineEmits<{
  click: [event: MouseEvent];
  open: [fileId: string];
  rename: [fileId: string];
  delete: [fileIds: string[]];
  move: [fileIds: string[]];
  copy: [fileIds: string[]];
  download: [fileIds: string[]];
  preview: [fileId: string];
  share: [fileIds: string[]];
  "file-trashed": [fileId: string];
  "file-restored": [];
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

const contextMenuItems = computed(() => {
  const isMultiSelect = (props.selectedCount ?? 0) > 1;
  const items: ContextMenuItem[] = [];

  if (!isMultiSelect) {
    items.push([
      {
        disabled: !canDownload(),
        icon: "i-mdi-download",
        label: "Download",
        onSelect: () => emit("download", [props.data.fileId]),
      },
      {
        disabled: !canRename(),
        icon: "i-mdi-pencil",
        label: "Rename",
        onSelect: () => emit("rename", props.data.fileId),
      },
    ]);
    items.push([
      {
        disabled: !canMove(),
        icon: "i-mdi-folder-move",
        label: "Move",
        onSelect: () => emit("move", [props.data.fileId]),
      },
      {
        disabled: !canCopy(),
        icon: "i-mdi-content-copy",
        label: "Copy",
        onSelect: () => emit("copy", [props.data.fileId]),
      },
      {
        disabled: !canShare(),
        icon: "i-mdi-share-variant",
        label: "Share",
        onSelect: () => emit("share", [props.data.fileId]),
      },
    ]);
    items.push([
      {
        disabled: !canDelete(),
        icon: "i-mdi-delete",
        label: "Delete",
        onSelect: () => emit("delete", [props.data.fileId]),
      },
    ]);
  } else {
    items.push([
      {
        disabled: !canDownload(),
        icon: "i-mdi-download",
        label: `Download ${props.selectedCount} items`,
        onSelect: () => emit("download", []),
      },
    ]);
    items.push([
      {
        disabled: !canMove(),
        icon: "i-mdi-folder-move",
        label: `Move ${props.selectedCount} items`,
        onSelect: () => emit("move", []),
      },
      {
        disabled: !canCopy(),
        icon: "i-mdi-content-copy",
        label: `Copy ${props.selectedCount} items`,
        onSelect: () => emit("copy", []),
      },
      {
        disabled: !canShare(),
        icon: "i-mdi-share-variant",
        label: `Share ${props.selectedCount} items`,
        onSelect: () => emit("share", []),
      },
    ]);
    items.push([
      {
        disabled: !canDelete(),
        icon: "i-mdi-delete",
        label: `Delete ${props.selectedCount} items`,
        onSelect: () => emit("delete", []),
      },
    ]);
  }

  return items;
});

const canRename = (): boolean => true;
const canMove = (): boolean => true;
const canCopy = (): boolean => true;
const canDownload = (): boolean => true;
const canShare = (): boolean => true;
const canDelete = (): boolean => true;

// Formatters

const formatFileSize = (bytes: number | undefined): string => {
  if (!bytes) return "";
  const units = ["B", "KB", "MB", "GB", "TB"];
  let size = bytes;
  let unitIndex = 0;
  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024;
    unitIndex++;
  }
  return `${size.toFixed(1)} ${units[unitIndex]}`;
};

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
