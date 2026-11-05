<script setup lang="ts">
import { computed, ref } from "vue";

import { directoryApi } from "@/api/directory";
import UploadEmptyState from "@/components/dashboard/file-system/Modals/upload/UploadEmptyState.vue";
import UploadFileTree from "@/components/dashboard/file-system/Modals/upload/UploadFileTree.vue";
import UploadModalFooter from "@/components/dashboard/file-system/Modals/upload/UploadModalFooter.vue";
import UploadModalShell from "@/components/dashboard/file-system/Modals/upload/UploadModalShell.vue";
import UploadPanel from "@/components/dashboard/file-system/Modals/upload/UploadPanel.vue";
import UploadProgressSummary from "@/components/dashboard/file-system/Modals/upload/UploadProgressSummary.vue";
import { useAppToast } from "@/composables/useAppToast";
import { type FileEntry, useDirectoryUpload } from "@/composables/useDirectoryUpload";
import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { countOf } from "@/features/toast/format";

const appToast = useAppToast("upload");

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
const destinationName = ref(props.directoryName ?? "Home");

const rootFolderName = computed(() => files.value[0]?.relativePath.split("/")[0]);

// Names what is being uploaded so the toast still makes sense after the modal has closed.
const uploadSubject = computed(() =>
  rootFolderName.value ? `"${rootFolderName.value}"` : "the folder",
);

const totalSize = computed(() => files.value.reduce((sum, entry) => sum + entry.file.size, 0));

const uploadSummary = (count: number, outcome: string) =>
  `${countOf(count, "file")} ${outcome} to ${destinationName.value}`;

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

// One plain sentence instead of coloured fragments, so the summary reads the same in every modal.
const progressLabel = computed(() => {
  const failed = failedFiles.value.length;
  const failedNote = failed > 0 ? `, ${failed} failed` : "";

  if (uploading.value)
    return `Uploading ${activeFiles.value}, ${successfulFiles.value} done${failedNote}`;

  return `${completedFiles.value} of ${totalFiles.value} files${failedNote}`;
});

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
      const finishedEarly = successfulFiles.value;

      appToast.info(
        "Upload cancelled",
        finishedEarly > 0
          ? uploadSummary(finishedEarly, "finished before you cancelled")
          : undefined,
      );
      return;
    }

    const failed = failedFiles.value.length;
    const success = successfulFiles.value;

    if (failed === 0) {
      appToast.success(`Uploaded ${uploadSubject.value}`, uploadSummary(success, "uploaded"));
      setTimeout(() => emit("close", true), 1000);
    } else {
      appToast.warning(
        `Uploaded ${success} of ${success + failed} files from ${uploadSubject.value}`,
        `${countOf(failed, "file")} failed. Use Retry in the upload window.`,
      );
    }
  } catch (err) {
    appToast.error(`Couldn't create folders for ${uploadSubject.value}`, err, {
      retry: uploadDirectoryStructure,
    });
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
    appToast.success(`Uploaded ${uploadSubject.value}`, uploadSummary(success, "uploaded"));
    setTimeout(() => emit("close", true), 1000);
  } else {
    appToast.warning(
      `${countOf(stillFailed, "file")} from ${uploadSubject.value} still failed`,
      "Check each file's error in the upload window.",
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
    v-model:destination-name="destinationName"
    title="Upload a folder"
    description="Everything inside it is uploaded, keeping its subfolders."
    :initial-id="directoryId"
    :initial-name="directoryName"
    :picker-disabled="uploading"
    @close="emit('close', false)"
  >
    <UploadEmptyState
      v-if="files.length === 0"
      icon="i-lucide-folder-up"
      title="Choose a folder"
      subtitle="Its files and subfolders are recreated at the destination."
      button-label="Choose folder"
      button-icon="i-lucide-folder-open"
      hint="You can also drop a folder onto the file explorer."
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

    <UploadPanel v-else>
      <template #header>
        <p class="min-w-0 flex-1 truncate text-sm text-gray-600 dark:text-gray-400">
          <span class="font-medium text-gray-900 dark:text-gray-100">
            {{ countOf(files.length, "file") }}
          </span>
          · {{ formatBytes(totalSize) }}
        </p>

        <UButton
          v-if="!uploading && fileStatuses.length === 0"
          icon="i-lucide-x"
          size="sm"
          variant="ghost"
          color="neutral"
          square
          class="justify-center"
          aria-label="Clear folder"
          @click="clearFiles"
        />
      </template>

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

      <template #footer>
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
            <template #label>{{ progressLabel }}</template>
          </UploadProgressSummary>
        </Transition>
      </template>
    </UploadPanel>

    <template #footer>
      <UploadModalFooter
        :working="uploading"
        :has-session="fileStatuses.length > 0"
        :has-failures="failedFiles.length > 0 && !uploading"
        :destination-name="destinationName"
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
