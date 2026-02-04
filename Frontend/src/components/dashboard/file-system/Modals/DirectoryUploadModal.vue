<script setup lang="ts">
import { ref, computed } from "vue";
import apiClient from "@/api/client";
import { directoryApi } from "@/api/directory";
import { createSHA256 } from "hash-wasm";
import type { SelectMenuItem } from "@nuxt/ui";
import { useDirectoryStore } from "@/stores/directory";

const toast = useToast();
const directoryStore = useDirectoryStore();

interface FileUploadStatus {
  file: File;
  relativePath: string;
  status: "pending" | "hashing" | "uploading" | "success" | "error";
  progress: number;
  error?: string;
  directoryId?: string;
}

const props = defineProps<{
  directoryId?: string;
}>();

const emit = defineEmits<{ close: [boolean] }>();

const files = ref<File[]>([]);
const fileStatuses = ref<FileUploadStatus[]>([]);
const uploading = ref(false);
const showDetailedProgress = ref(false);
const selectedDirectoryId = ref<string | null>(props.directoryId || null);

// Parent directory search
const parentDirectoryOptions = ref<SelectMenuItem[]>([]);
const isLoadingParentDirs = ref(false);

const BATCH_SIZE = 5;

// Computed stats
const totalFiles = computed(() => fileStatuses.value.length);
const completedFiles = computed(
  () =>
    fileStatuses.value.filter(
      (f) => f.status === "success" || f.status === "error",
    ).length,
);
const successfulFiles = computed(
  () => fileStatuses.value.filter((f) => f.status === "success").length,
);
const failedFiles = computed(() =>
  fileStatuses.value.filter((f) => f.status === "error"),
);
const overallProgress = computed(() => {
  if (totalFiles.value === 0) return 0;
  return Math.round((completedFiles.value / totalFiles.value) * 100);
});

// Format bytes to human readable
// TODO: if this gets typed out once more I am extracting it in a util
function formatBytes(bytes: number, decimals = 2): string {
  if (bytes === 0) return "0 Bytes";
  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ["Bytes", "KB", "MB", "GB", "TB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + " " + sizes[i];
}

const onChange = (e: Event) => {
  const input = e.target as HTMLInputElement;
  if (!input.files) return;
  files.value = Array.from(input.files);
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
      nameContains: query,
      pageSize: 20,
      isDeleted: false,
    });

    if (response.success && response.data) {
      const newOptions = response.data.items.map((d) => ({
        label: d.name,
        id: d.id,
      }));

      const selectedId = selectedDirectoryId.value;
      const selectedOption = parentDirectoryOptions.value.find(
        (o) => o.id === selectedId,
      );

      parentDirectoryOptions.value = selectedOption
        ? [selectedOption, ...newOptions.filter((o) => o.id !== selectedId)]
        : newOptions;
    }
  } finally {
    isLoadingParentDirs.value = false;
  }
};

// Upload a single file with hash computation
async function uploadSingleFile(
  fileStatus: FileUploadStatus,
): Promise<boolean> {
  try {
    const file = fileStatus.file;

    fileStatus.status = "hashing";
    const hasher = await createSHA256();
    hasher.init();

    const chunkSize = 1024 * 1024; // 1MB chunks
    let offset = 0;

    while (offset < file.size) {
      const chunk = file.slice(offset, offset + chunkSize);
      const arrayBuffer = await chunk.arrayBuffer();
      hasher.update(new Uint8Array(arrayBuffer));
      offset += chunkSize;

      // Hashing progress (0-30%)
      const hashProgress = Math.round((offset / file.size) * 30);
      fileStatus.progress = hashProgress;
    }

    const fileHash = hasher.digest("hex");

    // Upload
    fileStatus.status = "uploading";
    const formData = new FormData();
    formData.append("filename", file.name);
    formData.append("sha256", fileHash);

    if (fileStatus.directoryId) {
      formData.append("directoryId", fileStatus.directoryId);
    }

    formData.append("file", file);

    await apiClient.post("/files/upload", formData, {
      headers: {
        "Content-Type": "multipart/form-data",
      },
      onUploadProgress: (progressEvent) => {
        if (progressEvent.total) {
          // Upload progress (30-100%)
          const uploadPct = Math.round(
            (progressEvent.loaded * 100) / progressEvent.total,
          );
          fileStatus.progress = 30 + Math.round((uploadPct * 70) / 100);
        }
      },
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
}

// Upload directory structure with batch processing
async function uploadDirectoryStructure() {
  if (files.value.length === 0) return;

  uploading.value = true;

  try {
    const paths = files.value.map((f) => f.webkitRelativePath);
    const directoryMapping = await directoryApi.uploadDirectory(
      selectedDirectoryId.value,
      paths,
    );

    fileStatuses.value = files.value.map((file) => ({
      file,
      relativePath: file.webkitRelativePath,
      status: "pending" as const,
      progress: 0,
      directoryId: directoryMapping[file.webkitRelativePath] || undefined,
    }));

    const pendingFiles = [...fileStatuses.value];

    while (pendingFiles.length > 0) {
      const batch = pendingFiles.splice(0, BATCH_SIZE);
      await Promise.all(
        batch.map((fileStatus) => uploadSingleFile(fileStatus)),
      );
    }

    const failed = failedFiles.value.length;
    const success = successfulFiles.value;

    if (failed === 0) {
      toast.add({
        title: "Upload Complete",
        description: `Successfully uploaded ${success} ${
          success === 1 ? "file" : "files"
        }`,
        color: "success",
      });

      setTimeout(() => {
        emit("close", true);
      }, 1000);
    } else {
      toast.add({
        title: "Upload Finished with Errors",
        description: `${success} succeeded, ${failed} failed`,
        color: "warning",
      });
    }
  } catch (error: any) {
    toast.add({
      title: "Directory Structure Creation Failed",
      description:
        error.response?.data?.message ||
        error.message ||
        "Failed to create directory structure",
      color: "error",
    });
    uploading.value = false;
  }

  uploading.value = false;
}

// Retry failed uploads
async function retryFailedUploads() {
  const failed = fileStatuses.value.filter((f) => f.status === "error");
  if (failed.length === 0) return;

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

  const stillFailed = fileStatuses.value.filter(
    (f) => f.status === "error",
  ).length;

  if (stillFailed === 0) {
    toast.add({
      title: "Retry Successful",
      description: "All failed uploads completed successfully",
      color: "success",
    });
  } else {
    toast.add({
      title: "Retry Complete",
      description: `${stillFailed} ${
        stillFailed === 1 ? "file" : "files"
      } still failed`,
      color: "warning",
    });
  }

  uploading.value = false;
}

// Get status icon
function getStatusIcon(status: FileUploadStatus["status"]) {
  switch (status) {
    case "pending":
      return "i-lucide-clock";
    case "hashing":
      return "i-lucide-hash";
    case "uploading":
      return "i-lucide-upload";
    case "success":
      return "i-lucide-check-circle";
    case "error":
      return "i-lucide-x-circle";
  }
}

// Get status color
function getStatusColor(status: FileUploadStatus["status"]) {
  switch (status) {
    case "pending":
      return "text-muted";
    case "hashing":
    case "uploading":
      return "text-primary";
    case "success":
      return "text-success";
    case "error":
      return "text-error";
  }
}
</script>

<template>
  <UModal
    :close="{ onClick: () => emit('close', false) }"
    title="Upload Directory"
    :ui="{ body: 'space-y-6', width: 'max-w-4xl' }"
  >
    <template #body>
      <p class="text-sm text-muted">
        Select a folder to upload all its files and subdirectories
      </p>

      <!-- Parent Directory Selection -->
      <div class="space-y-2">
        <label class="block text-sm font-medium">
          Upload to Directory (Optional)
        </label>
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
                {{
                  parentDirectoryOptions.find((i) => i.id === modelValue)?.label
                }}
              </span>
              <span v-else class="text-muted">
                Root directory (no parent)
              </span>
            </div>
          </template>
        </USelectMenu>
        <p class="text-xs text-muted">
          Leave empty to upload to the root directory
        </p>
      </div>

      <!-- Directory Upload Area -->
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
      <div
        v-if="files.length > 0 && fileStatuses.length === 0"
        class="space-y-3"
      >
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
          />
        </div>
        <div class="border rounded-lg max-h-64 overflow-y-auto">
          <div
            v-for="(file, index) in files"
            :key="index"
            class="flex items-center gap-3 px-4 py-2 border-b last:border-b-0 hover:bg-gray-50 dark:hover:bg-gray-800/50"
          >
            <UIcon name="i-lucide-file" class="size-4 text-muted shrink-0" />
            <span class="text-sm truncate flex-1">
              {{ file.webkitRelativePath }}
            </span>
            <span class="text-xs text-muted shrink-0">
              {{ formatBytes(file.size) }}
            </span>
          </div>
        </div>
      </div>

      <!-- Upload Progress Section -->
      <div v-if="fileStatuses.length > 0" class="space-y-4">
        <!-- Overall Progress -->
        <div class="border rounded-lg p-4 space-y-3">
          <div class="flex items-center justify-between">
            <div class="space-y-1">
              <p class="text-sm font-medium">Upload Progress</p>
              <p class="text-xs text-muted">
                {{ completedFiles }} of {{ totalFiles }} files completed
                <span v-if="successfulFiles > 0" class="text-success">
                  ({{ successfulFiles }} succeeded</span
                ><span v-if="failedFiles.length > 0" class="text-error"
                  >, {{ failedFiles.length }} failed)</span
                >
              </p>
            </div>
            <div class="flex items-center gap-2">
              <UButton
                v-if="!uploading && fileStatuses.length > 0"
                :label="showDetailedProgress ? 'Hide Details' : 'Show Details'"
                :icon="
                  showDetailedProgress ? 'i-lucide-chevron-up' : 'i-lucide-list'
                "
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
        <div
          v-if="showDetailedProgress"
          class="border rounded-lg max-h-96 overflow-y-auto"
        >
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
                ]"
              />
              <div class="flex-1 min-w-0 space-y-2">
                <div class="flex items-start justify-between gap-2">
                  <div class="flex-1 min-w-0">
                    <p class="text-sm truncate">
                      {{ fileStatus.relativePath }}
                    </p>
                    <p
                      v-if="fileStatus.error"
                      class="text-xs text-error mt-0.5"
                    >
                      {{ fileStatus.error }}
                    </p>
                  </div>
                  <span class="text-xs text-muted shrink-0">
                    {{ formatBytes(fileStatus.file.size) }}
                  </span>
                </div>

                <!-- Individual file progress -->
                <div
                  v-if="
                    fileStatus.status === 'hashing' ||
                    fileStatus.status === 'uploading'
                  "
                  class="space-y-1"
                >
                  <div class="flex items-center justify-between text-xs">
                    <span class="text-muted capitalize">{{
                      fileStatus.status
                    }}</span>
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
                {{ failedFiles.length === 1 ? "file" : "files" }} failed to
                upload
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
