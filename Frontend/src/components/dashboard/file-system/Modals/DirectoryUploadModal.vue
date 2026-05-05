<script setup lang="ts">
import { computed, ref } from "vue";
import { directoryApi } from "@/api/directory";
import {
  type DirectoryTreeItem,
  type FileEntry,
  useDirectoryUpload,
} from "@/composables/useDirectoryUpload";
import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { useAppToast } from "@/composables/useAppToast";
import DirectoryPicker from "@/components/common/DirectoryPicker.vue";

const appToast = useAppToast();

const {
  fileStatuses,
  uploading,
  cancelled,
  totalFiles,
  completedFiles,
  successfulFiles,
  failedFiles,
  activeFiles,
  overallProgress,
  summaryBarColor,
  shouldVirtualize,
  statusMap,
  folderStatusMap,
  folderChildCountMap,
  buildFileTree,
  buildTreeFromStatuses,
  startUploadPipeline,
  cancelAll,
  retryFailedUploads,
  reset,
  statusIcon,
  statusIconClass,
  folderIconClass,
  formatBytes,
} = useDirectoryUpload();

// props & emits

const props = defineProps<{
  directoryId?: string;
  directoryName?: string;
  droppedFiles?: FileEntry[];
}>();

useModalBackGuard(() => emit("close", false));

const emit = defineEmits<{ close: [boolean] }>();

// state

const files = ref<FileEntry[]>(props.droppedFiles ?? []);
const selectedDirectoryId = ref<string | undefined>(props.directoryId ?? undefined);

// computed

const treeItems = computed(() =>
  fileStatuses.value.length > 0 ? buildTreeFromStatuses() : buildFileTree(files.value),
);

const canUpload = computed(
  () => files.value.length > 0 && !uploading.value && fileStatuses.value.length === 0,
);

// upload

const uploadDirectoryStructure = async () => {
  if (!canUpload.value) return;

  uploading.value = true;
  cancelled.value = false;

  try {
    const paths = files.value.map((f) => f.relativePath);
    const directoryMapping = await directoryApi.uploadDirectory(selectedDirectoryId.value, paths);

    await startUploadPipeline(files.value, directoryMapping);

    if (cancelled.value) {
      appToast.info("Upload cancelled");
      return;
    }

    const failed = failedFiles.value.length;
    const success = successfulFiles.value;

    if (failed === 0) {
      appToast.success(
        "Upload Complete",
        `${success} ${success === 1 ? "file" : "files"} uploaded`,
      );
      setTimeout(() => emit("close", true), 1000);
    } else {
      appToast.warning("Upload Finished with Errors", `${success} succeeded, ${failed} failed`);
    }
  } catch (err) {
    appToast.error("Directory Structure Failed", err);
  } finally {
    uploading.value = false;
  }
};

const handleRetry = async () => {
  uploading.value = true;
  await retryFailedUploads();

  const stillFailed = failedFiles.value.length;
  const success = successfulFiles.value;

  if (stillFailed === 0) {
    appToast.success("Upload Complete", `${success} ${success === 1 ? "file" : "files"} uploaded`);
    setTimeout(() => emit("close", true), 1000);
  } else {
    appToast.warning(
      "Retry Complete",
      `${stillFailed} ${stillFailed === 1 ? "file" : "files"} still failed`,
    );
    uploading.value = false;
  }
};

const clearFiles = () => {
  files.value = [];
  reset();
};
</script>

<template>
  <UModal
    :close="{ onClick: () => emit('close', false) }"
    title="Upload Directory"
    :ui="{ body: 'space-y-4' }"
  >
    <template #body>
      <p class="text-sm text-muted">Select a folder to upload all its files and subdirectories.</p>

      <DirectoryPicker
        v-model="selectedDirectoryId"
        :initial-id="directoryId"
        :initial-name="directoryName"
        :disabled="uploading"
      />

      <!-- empty: folder picker -->
      <div
        v-if="files.length === 0"
        class="rounded-lg border border-dashed border-gray-200/70 dark:border-gray-700/70 p-8 flex flex-col items-center gap-4"
      >
        <UIcon name="i-lucide-folder-up" class="size-10 text-muted" />
        <label for="directory-upload" class="cursor-pointer">
          <input
            id="directory-upload"
            type="file"
            webkitdirectory
            multiple
            class="hidden"
            @change="
              (e) => {
                const input = e.target as HTMLInputElement;
                if (!input.files) return;
                files = Array.from(input.files).map((f) => ({
                  file: f,
                  relativePath: f.webkitRelativePath || f.name,
                }));
              }
            "
          />
          <UButton
            as="span"
            label="Select Folder"
            icon="i-lucide-folder-open"
            variant="outline"
            color="neutral"
          />
        </label>
        <p class="text-xs text-muted">Or drag and drop a folder onto the file explorer</p>
      </div>

      <!-- file tree -->
      <div
        v-else
        class="rounded-lg border border-gray-200/70 dark:border-gray-700/70 overflow-hidden"
      >
        <div
          class="flex items-center justify-between px-3 py-2 border-b border-gray-200/70 dark:border-gray-700/70 bg-white/40 dark:bg-white/3"
        >
          <span class="text-xs text-muted">
            {{ files.length }} {{ files.length === 1 ? "file" : "files" }}
          </span>
          <UButton
            v-if="!uploading"
            icon="i-lucide-x"
            label="Clear"
            size="xs"
            variant="ghost"
            color="neutral"
            @click="clearFiles"
          />
        </div>

        <div class="max-h-72 overflow-y-auto px-2 py-1.5">
          <UTree
            :items="treeItems"
            :virtualize="shouldVirtualize"
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
                :class="fileStatuses.length > 0 ? folderIconClass(item.fullPath) : 'text-muted'"
              />
              <UIcon v-else name="i-lucide-file" class="size-4 text-muted" />
            </template>

            <template #item-trailing="{ item }: { item: DirectoryTreeItem }">
              <template v-if="item.isFolder && fileStatuses.length > 0">
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
                <span v-if="fileStatuses.length === 0" class="text-xs text-muted tabular-nums">
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

        <Transition
          enter-active-class="transition-all duration-200 ease-out"
          leave-active-class="transition-all duration-150 ease-in"
          enter-from-class="opacity-0"
          leave-to-class="opacity-0"
        >
          <div
            v-if="fileStatuses.length > 0"
            class="px-3 py-2.5 border-t border-gray-200/70 dark:border-gray-700/70 bg-white/40 dark:bg-white/3 space-y-1.5"
          >
            <div class="flex items-center justify-between text-xs">
              <span class="text-muted">
                <template v-if="uploading">
                  {{ activeFiles }} uploading
                  <span v-if="successfulFiles > 0" class="text-success">
                    · {{ successfulFiles }} done</span
                  >
                  <span v-if="failedFiles.length > 0" class="text-error">
                    · {{ failedFiles.length }} failed</span
                  >
                </template>
                <template v-else> {{ completedFiles }} of {{ totalFiles }} files </template>
              </span>
              <span class="tabular-nums font-medium">{{ overallProgress }}%</span>
            </div>
            <UProgress :value="overallProgress" :color="summaryBarColor" size="xs" />
          </div>
        </Transition>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-between w-full gap-2">
        <div class="flex gap-2">
          <UButton
            v-if="uploading"
            label="Cancel"
            icon="i-lucide-x"
            variant="outline"
            color="error"
            @click="cancelAll"
          />
          <UButton
            v-else-if="fileStatuses.length > 0"
            label="Start Over"
            icon="i-lucide-rotate-ccw"
            variant="ghost"
            color="neutral"
            @click="clearFiles"
          />
          <UButton
            v-else
            label="Cancel"
            variant="outline"
            color="neutral"
            @click="emit('close', false)"
          />
        </div>

        <div class="flex gap-2">
          <UButton
            v-if="failedFiles.length > 0 && !uploading"
            label="Retry Failed"
            icon="i-lucide-refresh-cw"
            variant="outline"
            color="neutral"
            @click="handleRetry"
          />
          <UButton
            v-if="canUpload"
            label="Upload"
            icon="i-lucide-upload"
            color="primary"
            variant="solid"
            @click="uploadDirectoryStructure"
          />
        </div>
      </div>
    </template>
  </UModal>
</template>
