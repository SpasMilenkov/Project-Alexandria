<script setup lang="ts">
import { Icon } from "@iconify/vue";

import type {
  DirectoryTreeItem,
  FileUploadStatus,
  FolderStatus,
  UploadStatus,
} from "@/composables/useDirectoryUpload";

import MiddleTruncatedName from "@/components/common/MiddleTruncatedName.vue";
import { getFileIcon } from "@/utils/files/icons";

const { statusMap } = defineProps<{
  items: DirectoryTreeItem[];
  virtualize: boolean;
  hasStatuses: boolean;
  statusMap: Map<string, FileUploadStatus>;
  folderStatusMap: Map<string, FolderStatus>;
  folderChildCountMap: Map<string, { total: number; done: number }>;
  statusIcon: (status: UploadStatus) => string;
  statusIconClass: (status: UploadStatus) => string;
  folderIconClass: (folderPath: string) => string;
  formatBytes: (bytes: number) => string;
}>();

// The label owns the shrinking; trailing size and status keep their width however long the name is.
const TREE_UI = {
  linkLabel: "flex min-w-0 flex-1 items-center gap-2",
  linkTrailing: "ms-auto inline-flex shrink-0 items-center gap-1.5 ps-3",
  listWithChildren: "border-s border-gray-200/50 dark:border-gray-700/50",
};

const labelClass = (item: DirectoryTreeItem) => {
  if (item.isFolder) return "font-medium text-gray-900 dark:text-gray-100";
  if (statusMap.get(item.relativePath ?? "")?.status === "error") return "text-error";

  return "text-gray-900 dark:text-gray-100";
};
</script>

<template>
  <div class="max-h-72 overflow-y-auto px-2 py-1.5">
    <UTree
      :items="items"
      :virtualize="virtualize"
      :get-key="(item: DirectoryTreeItem) => item.fullPath"
      color="neutral"
      size="sm"
      :ui="TREE_UI"
    >
      <template #item-leading="{ item }: { item: DirectoryTreeItem }">
        <Icon
          v-if="item.isFolder"
          icon="mdi:folder"
          class="size-4 shrink-0 transition-colors"
          :class="hasStatuses ? folderIconClass(item.fullPath) : 'text-gray-500 dark:text-gray-400'"
        />
        <Icon
          v-else
          :icon="getFileIcon(item.label ?? '')"
          class="size-4 shrink-0 text-gray-500 dark:text-gray-400"
        />
      </template>

      <template #item-trailing="{ item }: { item: DirectoryTreeItem }">
        <template v-if="item.isFolder && hasStatuses">
          <span
            v-if="folderChildCountMap.get(item.fullPath)"
            class="text-xs tabular-nums"
            :class="{
              'text-success': folderStatusMap.get(item.fullPath) === 'complete',
              'text-error': folderStatusMap.get(item.fullPath) === 'error',
              'text-primary': folderStatusMap.get(item.fullPath) === 'uploading',
              'text-gray-500 dark:text-gray-500': folderStatusMap.get(item.fullPath) === 'pending',
            }"
          >
            {{ folderChildCountMap.get(item.fullPath)?.done }}/{{
              folderChildCountMap.get(item.fullPath)?.total
            }}
          </span>
        </template>

        <template v-else-if="!item.isFolder">
          <span v-if="!hasStatuses" class="text-xs tabular-nums text-gray-500 dark:text-gray-500">
            {{ formatBytes(item.fileSize ?? 0) }}
          </span>
          <div v-else class="flex items-center gap-1.5">
            <span
              v-if="
                statusMap.get(item.relativePath!)?.status === 'hashing' ||
                statusMap.get(item.relativePath!)?.status === 'uploading'
              "
              class="w-7 text-right text-xs tabular-nums text-gray-500 dark:text-gray-500"
            >
              {{ statusMap.get(item.relativePath!)?.progress ?? 0 }}%
            </span>
            <UIcon
              :name="statusIcon(statusMap.get(item.relativePath!)?.status ?? 'pending')"
              class="size-3.5 transition-colors"
              :class="statusIconClass(statusMap.get(item.relativePath!)?.status ?? 'pending')"
            />
          </div>
        </template>
      </template>

      <template #item-label="{ item }: { item: DirectoryTreeItem }">
        <MiddleTruncatedName
          :name="item.label ?? ''"
          :keep-extension="!item.isFolder"
          :class="labelClass(item)"
        />
        <span
          v-if="statusMap.get(item.relativePath ?? '')?.error"
          class="min-w-0 max-w-40 shrink truncate text-xs text-error"
          :title="statusMap.get(item.relativePath ?? '')?.error"
        >
          {{ statusMap.get(item.relativePath ?? "")?.error }}
        </span>
      </template>
    </UTree>
  </div>
</template>
