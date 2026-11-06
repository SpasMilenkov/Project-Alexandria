<script setup lang="ts">
import {
  BlobReader,
  BlobWriter,
  type Entry as ZipEntry,
  type FileEntry as ZipFileEntry,
  ZipReader,
  configure,
} from "@zip.js/zip.js";
import { computed, onMounted, ref } from "vue";

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
import { useReportUploadActivity } from "@/composables/useReportUploadActivity";
import { countOf } from "@/features/toast/format";
import { useDirectoryStore } from "@/stores/directory";

// configure zip.js to use its own built-in workers for decompression
configure({ useWebWorkers: true });

const directoryStore = useDirectoryStore();
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
  cancelAll: cancelUpload,
  retryFailedUploads,
  reset: resetUpload,
  statusIcon,
  statusIconClass,
  folderIconClass,
  formatBytes,
} = useDirectoryUpload();

useModalBackGuard(() => emit("close", false));

// props & emits

const props = defineProps<{
  directoryId?: string;
  directoryName?: string;
  droppedFiles?: File[];
}>();
const emit = defineEmits<{ close: [boolean] }>();

// accepted formats

const ACCEPTED_EXTENSIONS = new Set([".zip", ".jar"]);

const ACCEPTED_MIME_TYPES = new Set([
  "application/zip",
  "application/x-zip-compressed",
  "application/x-zip",
  "application/java-archive",
  "application/x-java-archive",
]);

const validateArchive = (file: File): string | null => {
  const ext = "." + (file.name.split(".").pop()?.toLowerCase() ?? "");
  if (ACCEPTED_EXTENSIONS.has(ext) || ACCEPTED_MIME_TYPES.has(file.type)) return null;
  return "Only .zip and .jar files are supported.";
};

// extraction state

type ExtractionPhase = "idle" | "scanning" | "extracting" | "done" | "error";

const archiveFile = ref<File | null>(null);
const extractionPhase = ref<ExtractionPhase>("idle");
const extractionError = ref<string | null>(null);
const extractedFiles = ref<FileEntry[]>([]);
const extractedCount = ref(0);
const totalEntryCount = ref(0);
const extractionAbort = ref(new AbortController());
const validationError = ref<string | null>(null);

const archiveInputRef = ref<HTMLInputElement | null>(null);

// computed

const extractionProgress = computed(() => {
  if (totalEntryCount.value === 0) return 0;
  return Math.round((extractedCount.value / totalEntryCount.value) * 100);
});

const isExtracting = computed(
  () => extractionPhase.value === "scanning" || extractionPhase.value === "extracting",
);

const isUploadPhase = computed(
  () => extractionPhase.value === "done" && extractedFiles.value.length > 0,
);

const treeItems = computed(() => {
  if (fileStatuses.value.length > 0) return buildTreeFromStatuses();
  return buildFileTree(extractedFiles.value);
});

const canUpload = computed(
  () => isUploadPhase.value && !uploading.value && fileStatuses.value.length === 0,
);

const progressBarColor = computed(() => {
  if (isExtracting.value) return "primary" as const;
  return summaryBarColor.value;
});

const progressValue = computed(() =>
  fileStatuses.value.length > 0 ? overallProgress.value : extractionProgress.value,
);

const progressLabel = computed(() => {
  const failed = failedFiles.value.length;
  const failedNote = failed > 0 ? `, ${failed} failed` : "";

  if (extractionPhase.value === "scanning") return "Reading the archive…";
  if (extractionPhase.value === "extracting") {
    return `Unpacking ${extractedCount.value} of ${totalEntryCount.value} files…`;
  }
  if (uploading.value) {
    return `Uploading ${activeFiles.value}, ${successfulFiles.value} done${failedNote}`;
  }
  if (fileStatuses.value.length > 0) {
    return `${completedFiles.value} of ${totalFiles.value} files${failedNote}`;
  }

  return "";
});

const showProgress = computed(() => isExtracting.value || fileStatuses.value.length > 0);

useReportUploadActivity(
  () => {
    const label = archiveFile.value?.name ?? null;

    if (isExtracting.value) {
      return {
        fileCount: totalEntryCount.value,
        kind: "extract",
        label,
        progress: extractionProgress.value,
      };
    }

    if (!uploading.value) return null;

    return { fileCount: totalFiles.value, kind: "upload", label, progress: overallProgress.value };
  },
  () => {
    if (extractionError.value) return "failed";

    // Extraction alone finishing is not news: the upload still waits for a click.
    if (cancelled.value || fileStatuses.value.length === 0) return null;

    if (failedFiles.value.length > 0) return "failed";

    return "done";
  },
);

interface ArchiveStatus {
  icon: string;
  iconClass: string;
  text: string;
}

// Plain status line under the archive name; the progress bar below carries the numbers.
const archiveStatus = computed((): ArchiveStatus => {
  if (isExtracting.value) {
    return {
      icon: "i-lucide-loader-circle",
      iconClass: "text-primary motion-safe:animate-spin",
      text: "Unpacking…",
    };
  }

  if (extractionPhase.value === "done") {
    return {
      icon: "i-lucide-check-circle",
      iconClass: "text-success",
      text: `${countOf(extractedFiles.value.length, "file")} unpacked`,
    };
  }

  if (extractionPhase.value === "error") {
    return {
      icon: "i-lucide-x-circle",
      iconClass: "text-error",
      text: extractionError.value ?? "Couldn't unpack the archive",
    };
  }

  return { icon: "", iconClass: "", text: "Ready to unpack" };
});

// file selection

const handleFileChange = (e: Event) => {
  const input = e.target as HTMLInputElement;
  const file = input.files?.[0] ?? null;
  input.value = "";

  if (!file) return;

  const err = validateArchive(file);
  if (err) {
    validationError.value = err;
    return;
  }

  validationError.value = null;
  archiveFile.value = file;
  extractedFiles.value = [];
  extractedCount.value = 0;
  totalEntryCount.value = 0;
  extractionPhase.value = "idle";
  extractionError.value = null;
  extractionAbort.value = new AbortController();
  resetUpload();
};

// extraction

const extractArchive = async () => {
  if (!archiveFile.value || isExtracting.value) return;

  extractionAbort.value = new AbortController();
  const { signal } = extractionAbort.value;
  extractedFiles.value = [];
  extractedCount.value = 0;
  extractionError.value = null;

  try {
    extractionPhase.value = "scanning";

    const reader = new ZipReader(new BlobReader(archiveFile.value));
    const entries = await reader.getEntries();

    const fileEntries = entries.filter(
      (entry: ZipEntry): entry is ZipFileEntry =>
        !entry.directory &&
        !entry.filename.startsWith("__MACOSX/") &&
        !entry.filename.endsWith("/"),
    );

    totalEntryCount.value = fileEntries.length;
    extractionPhase.value = "extracting";

    for (const entry of fileEntries) {
      if (signal.aborted) break;

      const blob = await entry.getData(new BlobWriter(), { signal });

      const relativePath = entry.filename;
      const fileName = relativePath.split("/").pop() ?? relativePath;

      // No type is set on purpose: the server detects the real type from the file content.
      extractedFiles.value.push({
        file: new File([blob], fileName),
        relativePath,
      });

      extractedCount.value++;
    }

    await reader.close();

    if (signal.aborted) {
      extractionPhase.value = "error";
      extractionError.value = "Extraction cancelled";
    } else {
      extractionPhase.value = "done";
    }
  } catch (err: any) {
    const isCancelled = err instanceof DOMException && err.name === "AbortError";
    extractionPhase.value = "error";
    extractionError.value = isCancelled
      ? "Extraction cancelled"
      : (err.message ?? "Failed to read archive");
  }
};

const cancelExtraction = () => {
  extractionAbort.value.abort();
};

const selectedDirectoryId = ref<string | undefined>(props.directoryId ?? undefined);
const destinationName = ref(props.directoryName ?? "Home");

// Names what is being uploaded so the toast still makes sense after the modal has closed.
const uploadSubject = computed(() => {
  const name = archiveFile.value?.name;

  return name ? `"${name}"` : "the archive";
});

const uploadSummary = (count: number, outcome: string) =>
  `${countOf(count, "file")} ${outcome} to ${destinationName.value}`;

// upload

const startUpload = async () => {
  if (!canUpload.value) return;

  uploading.value = true;
  cancelled.value = false;

  try {
    const paths = extractedFiles.value.map((f) => f.relativePath);
    const directoryMapping = await directoryApi.uploadDirectory(selectedDirectoryId.value, paths);

    await startUploadPipeline(extractedFiles.value, directoryMapping);

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
    appToast.error(`Couldn't upload ${uploadSubject.value}`, err, { retry: startUpload });
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

const reset = () => {
  archiveFile.value = null;
  extractedFiles.value = [];
  extractedCount.value = 0;
  totalEntryCount.value = 0;
  extractionPhase.value = "idle";
  extractionError.value = null;
  validationError.value = null;
  extractionAbort.value = new AbortController();
  resetUpload();
};

onMounted(() => {
  const dropped = props.droppedFiles?.[0];
  if (!dropped) return;

  const err = validateArchive(dropped);
  if (err) {
    validationError.value = err;
    return;
  }

  archiveFile.value = dropped;
  extractionAbort.value = new AbortController();
});
</script>

<template>
  <UploadModalShell
    v-model="selectedDirectoryId"
    v-model:destination-name="destinationName"
    title="Upload an archive"
    description="The archive is unpacked and its contents are uploaded as a folder."
    :initial-id="directoryId"
    :initial-name="directoryName"
    :picker-disabled="uploading"
    @close="emit('close', false)"
  >
    <UploadEmptyState
      v-if="!archiveFile"
      icon="i-lucide-archive"
      title="Choose an archive"
      subtitle="Works with .zip and .jar files."
      button-label="Choose archive"
      button-icon="i-lucide-file-archive"
      @select="archiveInputRef?.click()"
    >
      <input
        ref="archiveInputRef"
        type="file"
        accept=".zip,.jar,application/zip,application/java-archive"
        class="hidden"
        @change="handleFileChange"
      />

      <Transition
        enter-active-class="transition-all duration-200 ease-out"
        leave-active-class="transition-all duration-150 ease-in"
        enter-from-class="opacity-0"
        leave-to-class="opacity-0"
      >
        <p v-if="validationError" class="text-xs text-error">{{ validationError }}</p>
      </Transition>
    </UploadEmptyState>

    <UploadPanel v-else>
      <template #header>
        <UIcon
          name="i-lucide-file-archive"
          class="size-5 shrink-0 text-gray-500 dark:text-gray-500"
        />

        <div class="min-w-0 flex-1">
          <p class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
            {{ archiveFile.name }}
          </p>
          <p class="flex min-w-0 items-center gap-1.5 text-xs text-gray-600 dark:text-gray-400">
            <span class="shrink-0">{{ formatBytes(archiveFile.size) }}</span>
            <span aria-hidden="true">·</span>
            <UIcon
              v-if="archiveStatus.icon"
              :name="archiveStatus.icon"
              class="size-3.5 shrink-0"
              :class="archiveStatus.iconClass"
            />
            <span class="truncate" :title="archiveStatus.text">{{ archiveStatus.text }}</span>
          </p>
        </div>

        <UButton
          v-if="!isExtracting && !uploading"
          icon="i-lucide-x"
          size="sm"
          variant="ghost"
          color="neutral"
          square
          class="justify-center"
          aria-label="Remove archive"
          @click="reset"
        />
      </template>

      <UploadFileTree
        v-if="extractedFiles.length > 0 || fileStatuses.length > 0"
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
            v-if="showProgress"
            :percent="progressValue"
            :color="progressBarColor"
          >
            <template #label>{{ progressLabel }}</template>
          </UploadProgressSummary>
        </Transition>
      </template>
    </UploadPanel>

    <template #footer>
      <UploadModalFooter
        :working="isExtracting || uploading"
        :has-session="!!archiveFile"
        :has-failures="failedFiles.length > 0 && !uploading"
        :destination-name="destinationName"
        @cancel="isExtracting ? cancelExtraction() : cancelUpload()"
        @start-over="reset"
        @close="emit('close', false)"
        @retry="handleRetry"
      >
        <template #primary>
          <UButton
            v-if="extractionPhase === 'idle' && archiveFile"
            label="Unpack"
            icon="i-lucide-package-open"
            color="primary"
            variant="solid"
            @click="extractArchive"
          />
          <UButton
            v-else-if="extractionPhase === 'error'"
            label="Try unpacking again"
            icon="i-lucide-rotate-ccw"
            color="neutral"
            variant="outline"
            @click="extractArchive"
          />
          <UButton
            v-else-if="canUpload"
            label="Upload"
            icon="i-lucide-upload"
            color="primary"
            variant="solid"
            @click="startUpload"
          />
        </template>
      </UploadModalFooter>
    </template>
  </UploadModalShell>
</template>
