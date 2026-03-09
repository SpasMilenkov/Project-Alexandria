<script setup lang="ts">
import { computed, ref } from "vue";
import { directoryApi } from "@/api/directory";
import { fileApi } from "@/api/file";
import { createBLAKE3 } from "hash-wasm";
import type { SelectMenuItem } from "@nuxt/ui";
import { useDirectoryStore } from "@/stores/directory";
import { formatBytes } from "@/utils/size.utils";

const toast = useToast();
const directoryStore = useDirectoryStore();

interface FileUploadStatus {
  file: File;
  relativePath: string;
  status: "pending" | "uploading" | "success" | "error";
  progress: number;
  error?: string;
  directoryId?: string;
}

/**
 * Unified shape used for both drag-dropped files (where webkitRelativePath
 * is not set by the browser) and native input files (where we derive the
 * path from webkitRelativePath ourselves in onChange).
 */
interface FileEntry {
  file: File;
  relativePath: string;
}

const props = defineProps<{
  directoryId?: string;
  /**
   * Human-readable name for directoryId. When provided the select menu shows
   * this label immediately without needing a search round-trip.
   */
  directoryName?: string;
  /**
   * Pre-seeded from a drag-and-drop. The FileExplorer has already recursively
   * walked the FileSystem API tree and resolved real File objects + paths,
   * so we can use them directly without any further unwrapping.
   */
  droppedFiles?: FileEntry[];
}>();

const emit = defineEmits<{ close: [boolean] }>();

// Seed from drop if provided, otherwise start empty
const files = ref<FileEntry[]>(props.droppedFiles ?? []);
const fileStatuses = ref<FileUploadStatus[]>([]);
const uploading = ref(false);
const showDetailedProgress = ref(false);
const selectedDirectoryId = ref<string | null>(props.directoryId || null);

// Parent directory search — pre-seed with the active directory so the select
// Menu renders its label immediately rather than showing the raw UUID.
const parentDirectoryOptions = ref<SelectMenuItem[]>(
  props.directoryId && props.directoryName
    ? [{ id: props.directoryId, label: props.directoryName }]
    : [],
);
const isLoadingParentDirs = ref(false);

const BATCH_SIZE = 5;
const CHUNK_SIZE = 16 * 1024 * 1024;

// Computed stats
const totalFiles = computed(() => fileStatuses.value.length);
const completedFiles = computed(
  () => fileStatuses.value.filter((f) => f.status === "success" || f.status === "error").length,
);
const successfulFiles = computed(
  () => fileStatuses.value.filter((f) => f.status === "success").length,
);
const failedFiles = computed(() => fileStatuses.value.filter((f) => f.status === "error"));
const uploadingFiles = computed(
  () => fileStatuses.value.filter((f) => f.status === "uploading").length,
);
const overallProgress = computed(() => {
  if (totalFiles.value === 0) {
    return 0;
  }

  const totalProgress = fileStatuses.value.reduce((sum, f) => {
    if (f.status === "success") {
      return sum + 100;
    }
    if (f.status === "uploading") {
      return sum + f.progress;
    }
    return sum;
  }, 0);

  return Math.round(totalProgress / totalFiles.value);
});

const onChange = (e: Event) => {
  const input = e.target as HTMLInputElement;
  if (!input.files) {
    return;
  }
  // Native <input webkitdirectory> sets webkitRelativePath correctly
  files.value = Array.from(input.files).map((f) => ({
    file: f,
    relativePath: f.webkitRelativePath || f.name,
  }));
  fileStatuses.value = [];
};

const clearFiles = () => {
  files.value = [];
  fileStatuses.value = [];
};

const searchParentDirectory = async (query: string) => {
  if (!query.trim()) {
    return;
  }

  isLoadingParentDirs.value = true;
  try {
    const response = await directoryStore.searchDirectory({
      isDeleted: false,
      nameContains: query,
      pageSize: 20,
    });

    if (response.success && response.data) {
      const newOptions = response.data.items.map((d) => ({
        id: d.id,
        label: d.name,
      }));

      const selectedId = selectedDirectoryId.value;
      const selectedOption = parentDirectoryOptions.value.find((o) => o.id === selectedId);

      parentDirectoryOptions.value = selectedOption
        ? [selectedOption, ...newOptions.filter((o) => o.id !== selectedId)]
        : newOptions;
    }
  } finally {
    isLoadingParentDirs.value = false;
  }
};

const uploadSingleFile = async (fileStatus: FileUploadStatus): Promise<boolean> => {
  try {
    const { file } = fileStatus;
    fileStatus.status = "uploading";
    fileStatus.progress = 0;

    const hasher = await createBLAKE3();
    hasher.init();

    let offset = 0;
    while (offset < file.size) {
      const chunk = file.slice(offset, offset + CHUNK_SIZE);
      const arrayBuffer = await chunk.arrayBuffer();
      hasher.update(new Uint8Array(arrayBuffer));
      offset += CHUNK_SIZE;

      const hashProgress = Math.round((offset / file.size) * 25);
      fileStatus.progress = Math.min(hashProgress, 25);
    }

    const fileHash = hasher.digest("hex");

    fileStatus.progress = 25;
    const { uploadId, uploadUrl } = await fileApi.initializeUpload({
      contentLength: file.size,
      contentType: file.type || "application/octet-stream",
      directoryId: fileStatus.directoryId,
      hash: fileHash,
    });
    fileStatus.progress = 30;

    await fileApi.uploadToS3(uploadUrl, file, (percent) => {
      fileStatus.progress = 30 + Math.round((percent * 65) / 100);
    });

    fileStatus.progress = 95;
    await fileApi.finalizeUpload({
      directoryId: fileStatus.directoryId || undefined,
      fileName: file.name,
      uploadId,
    });

    fileStatus.status = "success";
    fileStatus.progress = 100;
    return true;
  } catch (error: any) {
    fileStatus.status = "error";
    fileStatus.error =
      error.response?.data?.message ||
      error.response?.data?.error ||
      error.message ||
      "Unknown error";
    return false;
  }
};

const uploadDirectoryStructure = async () => {
  if (files.value.length === 0) {
    return;
  }

  uploading.value = true;

  try {
    // Pass the already-resolved relative paths to the API
    const paths = files.value.map((f) => f.relativePath);
    const directoryMapping = await directoryApi.uploadDirectory(selectedDirectoryId.value, paths);

    fileStatuses.value = files.value.map(({ file, relativePath }) => ({
      directoryId: directoryMapping[relativePath] || undefined,
      file,
      progress: 0,
      relativePath,
      status: "pending" as const,
    }));

    const pendingFiles = [...fileStatuses.value];

    while (pendingFiles.length > 0) {
      const batch = pendingFiles.splice(0, BATCH_SIZE);
      await Promise.all(batch.map((fileStatus) => uploadSingleFile(fileStatus)));
    }

    const failed = failedFiles.value.length;
    const success = successfulFiles.value;

    if (failed === 0) {
      toast.add({
        color: "success",
        description: `Successfully uploaded ${success} ${success === 1 ? "file" : "files"}`,
        title: "Upload Complete",
      });

      setTimeout(() => {
        emit("close", true);
      }, 1000);
    } else {
      toast.add({
        color: "warning",
        description: `${success} succeeded, ${failed} failed`,
        title: "Upload Finished with Errors",
      });
    }
  } catch (error: any) {
    toast.add({
      color: "error",
      description:
        error.response?.data?.message || error.message || "Failed to create directory structure",
      title: "Directory Structure Creation Failed",
    });
  } finally {
    uploading.value = false;
  }
};

async function retryFailedUploads() {
  const failed = fileStatuses.value.filter((f) => f.status === "error");
  if (failed.length === 0) {
    return;
  }

  uploading.value = true;

  failed.forEach((f) => {
    f.status = "pending";
    f.progress = 0;
    f.error = undefined;
  });

  const pendingFiles = [...failed];

  while (pendingFiles.length > 0) {
    const batch = pendingFiles.splice(0, BATCH_SIZE);
    await Promise.all(batch.map((fileStatus) => uploadSingleFile(fileStatus)));
  }

  const stillFailed = fileStatuses.value.filter((f) => f.status === "error").length;

  if (stillFailed === 0) {
    toast.add({
      color: "success",
      description: "All failed uploads completed successfully",
      title: "Retry Successful",
    });
  } else {
    toast.add({
      color: "warning",
      description: `${stillFailed} ${stillFailed === 1 ? "file" : "files"} still failed`,
      title: "Retry Complete",
    });
  }

  uploading.value = false;
}

const getStatusIcon = (status: FileUploadStatus["status"]) => {
  switch (status) {
    case "pending":
      return "i-lucide-clock";
    case "uploading":
      return "i-lucide-loader-circle";
    case "success":
      return "i-lucide-check-circle";
    case "error":
      return "i-lucide-x-circle";
  }
};

const getStatusColor = (status: FileUploadStatus["status"]) => {
  switch (status) {
    case "pending":
      return "text-muted";
    case "uploading":
      return "text-primary";
    case "success":
      return "text-success";
    case "error":
      return "text-error";
  }
};
</script>

<template>
  <UModal
    :close="{ onClick: () => emit('close', false) }"
    title="Upload Directory"
    :ui="{ body: 'space-y-6', width: 'max-w-4xl' }"
  >
    <template #body>
      <p class="text-sm text-muted">Select a folder to upload all its files and subdirectories</p>

      <!-- Parent Directory Selection -->
      <div class="space-y-2">
        <label class="block text-sm font-medium"> Upload to Directory (Optional) </label>
        <USelectMenu
          v-model="selectedDirectoryId"
          :items="parentDirectoryOptions"
          :loading="isLoadingParentDirs"
          @update:search-term="searchParentDirectory"
          placeholder="Search for directory..."
          value-key="id"
          display-key="label"
          searchable
          :debounce="300"
          :disabled="uploading"
          class="w-full"
        >
          <template #default="{ modelValue }">
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-folder" class="size-4 text-muted" />
              <span v-if="modelValue">
                {{ parentDirectoryOptions.find((i) => i.id === modelValue)?.label }}
              </span>
              <span v-else class="text-muted">Root directory (no parent)</span>
            </div>
          </template>
        </USelectMenu>
        <p class="text-xs text-muted">Leave empty to upload to the root directory</p>
      </div>

      <!-- Directory Upload Area — only shown when no files are loaded yet -->
      <div
        v-if="files.length === 0"
        class="border-2 border-dashed rounded-lg p-8 text-center space-y-4"
      >
        <div class="flex justify-center">
          <UIcon name="i-lucide-folder-up" class="size-12 text-muted" />
        </div>
        <label for="directory-upload" class="inline-block cursor-pointer">
          <input
            id="directory-upload"
            type="file"
            webkitdirectory
            multiple
            @change="onChange"
            class="hidden"
          />
          <UButton
            as="span"
            label="Select Directory"
            icon="i-lucide-folder-open"
            variant="outline"
            color="neutral"
          />
        </label>
      </div>

      <!-- Files Preview (Before Upload) -->
      <div v-if="files.length > 0 && fileStatuses.length === 0" class="space-y-3">
        <div class="flex items-center justify-between">
          <p class="text-sm font-medium">
            {{ files.length }}
            {{ files.length === 1 ? "file" : "files" }} selected
          </p>
          <UButton
            label="Clear"
            icon="i-lucide-x"
            size="xs"
            variant="ghost"
            color="neutral"
            @click="clearFiles"
            :disabled="uploading"
          />
        </div>
        <div class="border rounded-lg max-h-64 overflow-y-auto">
          <div
            v-for="(entry, index) in files"
            :key="index"
            class="flex items-center gap-3 px-4 py-2 border-b last:border-b-0"
          >
            <UIcon name="i-lucide-file" class="size-4 text-muted shrink-0" />
            <span class="text-sm truncate flex-1">
              {{ entry.relativePath }}
            </span>
            <span class="text-xs text-muted shrink-0">
              {{ formatBytes(entry.file.size) }}
            </span>
          </div>
        </div>
      </div>

      <!-- Upload Progress Section -->
      <div v-if="fileStatuses.length > 0" class="space-y-4">
        <div class="border rounded-lg p-4 space-y-3">
          <div class="flex items-center justify-between">
            <div class="space-y-1">
              <p class="text-sm font-medium">Upload Progress</p>
              <p class="text-xs text-muted">
                {{ completedFiles }} of {{ totalFiles }} files completed
                <span v-if="uploadingFiles > 0" class="text-primary">
                  ({{ uploadingFiles }} uploading</span
                ><span v-if="successfulFiles > 0" class="text-success"
                  >, {{ successfulFiles }} succeeded</span
                ><span v-if="failedFiles.length > 0" class="text-error"
                  >, {{ failedFiles.length }} failed</span
                ><span v-if="uploadingFiles > 0">)</span>
              </p>
            </div>
            <div class="flex items-center gap-2">
              <UButton
                v-if="!uploading && fileStatuses.length > 0"
                :label="showDetailedProgress ? 'Hide Details' : 'Show Details'"
                :icon="showDetailedProgress ? 'i-lucide-chevron-up' : 'i-lucide-list'"
                size="xs"
                variant="ghost"
                color="neutral"
                @click="showDetailedProgress = !showDetailedProgress"
              />
            </div>
          </div>

          <div class="space-y-2">
            <div class="flex items-center justify-between text-sm">
              <span class="text-muted">Overall</span>
              <span class="font-medium">{{ overallProgress }}%</span>
            </div>
            <UProgress :value="overallProgress" />
          </div>
        </div>

        <!-- Detailed Progress List -->
        <div v-if="showDetailedProgress" class="border rounded-lg max-h-96 overflow-y-auto">
          <div
            v-for="(fileStatus, index) in fileStatuses"
            :key="index"
            class="px-4 py-3 border-b last:border-b-0"
          >
            <div class="flex items-start gap-3">
              <UIcon
                :name="getStatusIcon(fileStatus.status)"
                :class="[
                  'size-4 shrink-0 mt-0.5',
                  getStatusColor(fileStatus.status),
                  fileStatus.status === 'uploading' ? 'animate-spin' : '',
                ]"
              />
              <div class="flex-1 min-w-0 space-y-2">
                <div class="flex items-start justify-between gap-2">
                  <div class="flex-1 min-w-0">
                    <p class="text-sm truncate">
                      {{ fileStatus.relativePath }}
                    </p>
                    <p v-if="fileStatus.error" class="text-xs text-error mt-0.5">
                      {{ fileStatus.error }}
                    </p>
                  </div>
                  <span class="text-xs text-muted shrink-0">
                    {{ formatBytes(fileStatus.file.size) }}
                  </span>
                </div>

                <div v-if="fileStatus.status === 'uploading'" class="space-y-1">
                  <div class="flex items-center justify-between text-xs">
                    <span class="text-muted">Uploading</span>
                    <span class="font-medium">{{ fileStatus.progress }}%</span>
                  </div>
                  <UProgress :value="fileStatus.progress" size="xs" />
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Failed Files Summary -->
        <div
          v-if="!uploading && failedFiles.length > 0"
          class="border border-error/20 rounded-lg p-4 space-y-3"
        >
          <div class="flex items-start gap-2">
            <UIcon name="i-lucide-alert-triangle" class="size-5 text-error" />
            <div class="flex-1">
              <p class="text-sm font-medium">
                {{ failedFiles.length }}
                {{ failedFiles.length === 1 ? "file" : "files" }} failed to upload
              </p>
              <p class="text-xs text-muted mt-1">
                Review the details above and retry failed uploads
              </p>
            </div>
          </div>
          <UButton
            label="Retry Failed Uploads"
            icon="i-lucide-refresh-cw"
            size="sm"
            color="neutral"
            variant="outline"
            @click="retryFailedUploads"
          />
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-between w-full gap-2">
        <UButton
          v-if="fileStatuses.length > 0 && !uploading"
          label="Start Over"
          icon="i-lucide-rotate-ccw"
          variant="ghost"
          color="neutral"
          @click="clearFiles"
        />
        <UButton
          v-else
          color="neutral"
          variant="ghost"
          label="Cancel"
          :disabled="uploading"
          @click="emit('close', false)"
        />
        <UButton
          label="Upload"
          icon="i-lucide-upload"
          :disabled="files.length === 0 || uploading"
          :loading="uploading"
          @click="uploadDirectoryStructure"
        />
      </div>
    </template>
  </UModal>
</template>
