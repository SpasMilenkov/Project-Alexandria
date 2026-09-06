<script setup lang="ts">
import type {
  DirectoryTreeItem,
  FileUploadStatus,
  FolderStatus,
  UploadStatus,
} from "@/composables/useDirectoryUpload";

defineProps<{
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
</script>

<template>
  <div class="max-h-72 overflow-y-auto px-2 py-1.5">
    <UTree
      :items="items"
      :virtualize="virtualize"
      :get-key="(item: DirectoryTreeItem) => item.fullPath"
      color="neutral"
      size="sm"
      :ui="{ listWithChildren: 'border-s border-gray-200/50 dark:border-gray-700/50' }"
    >
      <template #item-leading="{ item }: { item: DirectoryTreeItem }">
        <UIcon
          v-if="item.isFolder"
          name="i-lucide-folder"
          class="size-4 transition-colors"
          :class="hasStatuses ? folderIconClass(item.fullPath) : 'text-muted'"
        />
        <UIcon v-else name="i-lucide-file" class="size-4 text-muted" />
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
              'text-muted': folderStatusMap.get(item.fullPath) === 'pending',
            }"
          >
            {{ folderChildCountMap.get(item.fullPath)?.done }}/{{
              folderChildCountMap.get(item.fullPath)?.total
            }}
          </span>
        </template>

        <template v-else-if="!item.isFolder">
          <span v-if="!hasStatuses" class="text-xs text-muted tabular-nums">
            {{ formatBytes(item.fileSize ?? 0) }}
          </span>
          <div v-else class="flex items-center gap-1.5">
            <span
              v-if="
                statusMap.get(item.relativePath!)?.status === 'hashing' ||
                statusMap.get(item.relativePath!)?.status === 'uploading'
              "
              class="text-xs text-muted tabular-nums w-7 text-right"
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
        <span
          class="truncate"
          :class="{
            'text-error': statusMap.get(item.relativePath ?? '')?.status === 'error',
          }"
        >
          {{ item.label }}
        </span>
        <span
          v-if="statusMap.get(item.relativePath ?? '')?.error"
          class="ml-1.5 text-xs text-error/70 truncate max-w-32"
        >
          — {{ statusMap.get(item.relativePath ?? "")?.error }}
        </span>
      </template>
    </UTree>
  </div>
</template>
