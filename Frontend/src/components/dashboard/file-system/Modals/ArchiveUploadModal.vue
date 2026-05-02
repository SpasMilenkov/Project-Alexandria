<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { BlobReader, BlobWriter, ZipReader, configure } from "@zip.js/zip.js";
import { directoryApi } from "@/api/directory";
import type { SelectMenuItem } from "@nuxt/ui";
import { useDirectoryStore } from "@/stores/directory";
import {
  type DirectoryTreeItem,
  type FileEntry,
  useDirectoryUpload,
} from "@/composables/useDirectoryUpload";
import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { useAppToast } from "@/composables/useAppToast";
import DirectoryPicker from "@/components/common/DirectoryPicker.vue";

// configure zip.js to use its own built-in workers for decompression
configure({ useWebWorkers: true });

const directoryStore = useDirectoryStore();
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
  if (extractionPhase.value === "scanning") return "Reading archive…";
  if (extractionPhase.value === "extracting")
    return `Extracting ${extractedCount.value} of ${totalEntryCount.value} files…`;
  if (uploading.value) return `${activeFiles.value} uploading`;
  if (fileStatuses.value.length > 0) return `${completedFiles.value} of ${totalFiles.value} files`;
  return "";
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
      (e) => !e.directory && !e.filename.startsWith("__MACOSX/") && !e.filename.endsWith("/"),
    );

    totalEntryCount.value = fileEntries.length;
    extractionPhase.value = "extracting";

    for (const entry of fileEntries) {
      if (signal.aborted) break;

      const blob = await entry.getData!(new BlobWriter(), { signal });

      const relativePath = entry.filename;
      const fileName = relativePath.split("/").pop() ?? relativePath;
      const mimeType = mimeFromExtension(fileName);

      extractedFiles.value.push({
        file: new File([blob], fileName, { type: mimeType }),
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
    appToast.error("Upload Failed", err);
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

// mime helpers

const EXTENSION_MIME: Record<string, string> = {
  css: "text/css",
  csv: "text/csv",
  gif: "image/gif",
  html: "text/html",
  ico: "image/x-icon",
  jpeg: "image/jpeg",
  jpg: "image/jpeg",
  js: "application/javascript",
  json: "application/json",
  md: "text/markdown",
  pdf: "application/pdf",
  png: "image/png",
  svg: "image/svg+xml",
  ts: "application/typescript",
  txt: "text/plain",
  wasm: "application/wasm",
  webp: "image/webp",
  xml: "application/xml",
  yaml: "application/yaml",
  yml: "application/yaml",
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

const mimeFromExtension = (fileName: string): string => {
  const ext = fileName.split(".").pop()?.toLowerCase() ?? "";
  return EXTENSION_MIME[ext] ?? "application/octet-stream";
};
</script>

<template>
  <UModal
    :close="{ onClick: () => emit('close', false) }"
    title="Upload Archive"
    :ui="{ body: 'space-y-4' }"
  >
    <template #body>
      <p class="text-sm text-muted">
        Upload a <code class="text-xs font-mono">.zip</code> or
        <code class="text-xs font-mono">.jar</code> archive — its contents will be extracted and
        uploaded as a directory.
      </p>

      <DirectoryPicker
        v-model="selectedDirectoryId"
        :initial-id="directoryId"
        :initial-name="directoryName"
        :disabled="uploading"
      />

      <!-- empty: archive picker -->
      <div
        v-if="!archiveFile"
        class="rounded-lg border border-dashed border-gray-200/70 dark:border-gray-700/70 p-8 flex flex-col items-center gap-4"
      >
        <UIcon name="i-lucide-archive" class="size-10 text-muted" />
        <div class="flex flex-col items-center gap-2">
          <UButton
            label="Select Archive"
            icon="i-lucide-file-archive"
            variant="outline"
            color="neutral"
            @click="archiveInputRef?.click()"
          />
          <p class="text-xs text-muted">.zip and .jar only</p>
        </div>

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
      </div>

      <!-- archive selected -->
      <div
        v-else
        class="rounded-lg border border-gray-200/70 dark:border-gray-700/70 overflow-hidden"
      >
        <!-- archive file row -->
        <div
          class="flex items-center gap-3 px-3 py-2.5 border-b border-gray-200/70 dark:border-gray-700/70 bg-white/40 dark:bg-white/3"
        >
          <UIcon name="i-lucide-file-archive" class="size-4 text-muted shrink-0" />
          <div class="flex-1 min-w-0">
            <p class="text-sm font-medium truncate text-gray-700 dark:text-gray-300">
              {{ archiveFile.name }}
            </p>
            <p class="text-xs text-muted">{{ formatBytes(archiveFile.size) }}</p>
          </div>

          <!-- extraction phase badge -->
          <div class="flex items-center gap-1.5 shrink-0">
            <template v-if="extractionPhase === 'idle'">
              <span class="text-xs text-muted">Ready to extract</span>
            </template>
            <template v-else-if="isExtracting">
              <UIcon name="i-lucide-loader-circle" class="size-3.5 text-primary animate-spin" />
              <span class="text-xs text-primary">Extracting…</span>
            </template>
            <template v-else-if="extractionPhase === 'done'">
              <UIcon name="i-lucide-check-circle" class="size-3.5 text-success" />
              <span class="text-xs text-success">{{ extractedFiles.length }} files extracted</span>
            </template>
            <template v-else-if="extractionPhase === 'error'">
              <UIcon name="i-lucide-x-circle" class="size-3.5 text-error" />
              <span class="text-xs text-error">{{ extractionError }}</span>
            </template>
          </div>

          <UButton
            v-if="!isExtracting && !uploading"
            icon="i-lucide-x"
            size="xs"
            variant="ghost"
            color="neutral"
            aria-label="Remove archive"
            @click="reset"
          />
        </div>

        <!-- extraction + upload progress bar -->
        <Transition
          enter-active-class="transition-all duration-200 ease-out"
          leave-active-class="transition-all duration-150 ease-in"
          enter-from-class="opacity-0"
          leave-to-class="opacity-0"
        >
          <div
            v-if="isExtracting || fileStatuses.length > 0"
            class="px-3 pt-2.5 pb-2 border-b border-gray-200/70 dark:border-gray-700/70 space-y-1.5"
          >
            <div class="flex items-center justify-between text-xs">
              <span class="text-muted">{{ progressLabel }}</span>
              <span class="tabular-nums font-medium">{{ progressValue }}%</span>
            </div>
            <UProgress :value="progressValue" :color="progressBarColor" size="xs" />
          </div>
        </Transition>

        <!-- live file tree — shown once we have any extracted entries -->
        <div
          v-if="extractedFiles.length > 0 || fileStatuses.length > 0"
          class="max-h-64 overflow-y-auto px-2 py-1.5"
        >
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
              <!-- folder: child count during upload -->
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

              <!-- leaf: size (extraction) or status (upload) -->
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

        <!-- upload summary bar -->
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
        <!-- left -->
        <div class="flex gap-2">
          <UButton
            v-if="isExtracting"
            label="Cancel"
            icon="i-lucide-x"
            variant="outline"
            color="error"
            @click="cancelExtraction"
          />
          <UButton
            v-else-if="uploading"
            label="Cancel"
            icon="i-lucide-x"
            variant="outline"
            color="error"
            @click="cancelUpload"
          />
          <UButton
            v-else-if="archiveFile"
            label="Start Over"
            icon="i-lucide-rotate-ccw"
            variant="ghost"
            color="neutral"
            @click="reset"
          />
          <UButton
            v-else
            label="Cancel"
            variant="outline"
            color="neutral"
            @click="emit('close', false)"
          />
        </div>

        <!-- right -->
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
            v-if="extractionPhase === 'idle' && archiveFile"
            label="Extract"
            icon="i-lucide-package-open"
            color="primary"
            variant="solid"
            @click="extractArchive"
          />
          <UButton
            v-else-if="extractionPhase === 'error'"
            label="Retry Extract"
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
        </div>
      </div>
    </template>
  </UModal>
</template>
