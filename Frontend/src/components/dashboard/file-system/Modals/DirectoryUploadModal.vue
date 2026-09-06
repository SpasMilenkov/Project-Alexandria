<script setup lang="ts">
import { computed, ref } from "vue";

import { directoryApi } from "@/api/directory";
import { useAppToast } from "@/composables/useAppToast";
import { type FileEntry, useDirectoryUpload } from "@/composables/useDirectoryUpload";
import { useModalBackGuard } from "@/composables/useModalBackGuard";

import UploadEmptyState from "./upload/UploadEmptyState.vue";
import UploadFileTree from "./upload/UploadFileTree.vue";
import UploadModalFooter from "./upload/UploadModalFooter.vue";
import UploadModalShell from "./upload/UploadModalShell.vue";
import UploadProgressSummary from "./upload/UploadProgressSummary.vue";

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
const directoryInputRef = ref<HTMLInputElement | null>(null);

const handleDirectoryChange = (e: Event) => {
  const input = e.target as HTMLInputElement;
  if (!input.files) return;
  files.value = Array.from(input.files).map((f) => ({
    file: f,
    relativePath: f.webkitRelativePath || f.name,
  }));
};

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
  <UploadModalShell
    v-model="selectedDirectoryId"
    title="Upload Directory"
    :initial-id="directoryId"
    :initial-name="directoryName"
    :picker-disabled="uploading"
    @close="emit('close', false)"
  >
    <template #intro>
      <p class="text-sm text-muted">Select a folder to upload all its files and subdirectories.</p>
    </template>

    <!-- empty: folder picker -->
    <UploadEmptyState
      v-if="files.length === 0"
      tall
      icon="i-lucide-folder-up"
      button-label="Select Folder"
      button-icon="i-lucide-folder-open"
      hint="Or drag and drop a folder onto the file explorer"
      @select="directoryInputRef?.click()"
    >
      <input
        ref="directoryInputRef"
        type="file"
        webkitdirectory
        multiple
        class="hidden"
        @change="handleDirectoryChange"
      />
    </UploadEmptyState>

    <!-- file tree -->
    <div
      v-else
      class="rounded-lg border border-gray-200/70 dark:border-gray-700/70 overflow-hidden"
    >
      <div
        class="flex items-center justify-between px-3 py-2.5 border-b border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface"
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

      <UploadFileTree
        :items="treeItems"
        :virtualize="shouldVirtualize"
        :has-statuses="fileStatuses.length > 0"
        :status-map="statusMap"
        :folder-status-map="folderStatusMap"
        :folder-child-count-map="folderChildCountMap"
        :status-icon="statusIcon"
        :status-icon-class="statusIconClass"
        :folder-icon-class="folderIconClass"
        :format-bytes="formatBytes"
      />

      <Transition
        enter-active-class="transition-all duration-200 ease-out"
        leave-active-class="transition-all duration-150 ease-in"
        enter-from-class="opacity-0"
        leave-to-class="opacity-0"
      >
        <UploadProgressSummary
          v-if="fileStatuses.length > 0"
          :percent="overallProgress"
          :color="summaryBarColor"
        >
          <template #label>
            <span>
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
          </template>
        </UploadProgressSummary>
      </Transition>
    </div>

    <template #footer>
      <UploadModalFooter
        :working="uploading"
        :has-session="fileStatuses.length > 0"
        :has-failures="failedFiles.length > 0 && !uploading"
        @cancel="cancelAll"
        @start-over="clearFiles"
        @close="emit('close', false)"
        @retry="handleRetry"
      >
        <template #primary>
          <UButton
            v-if="canUpload"
            label="Upload"
            icon="i-lucide-upload"
            color="primary"
            variant="solid"
            @click="uploadDirectoryStructure"
          />
        </template>
      </UploadModalFooter>
    </template>
  </UploadModalShell>
</template>
