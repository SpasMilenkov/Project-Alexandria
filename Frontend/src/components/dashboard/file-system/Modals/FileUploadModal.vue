<script setup lang="ts">
import { ref, computed } from "vue";
import apiClient from "@/api/client";
import { useDirectoryStore } from "@/stores/directory";
import type { SelectMenuItem } from "@nuxt/ui";
import { createSHA256 } from "hash-wasm";
const toast = useToast();
const directoryStore = useDirectoryStore();

interface UploadFileResponse {
  objectName: string;
  url: string;
  versionId: string;
  size: number;
  fileId: string;
}

const props = defineProps<{
  directoryId?: string;
}>();

const emit = defineEmits<{ close: [boolean] }>();

const selectedFile = ref<File | null>(null);
const uploading = ref(false);
const uploadProgress = ref(0);
const uploadResult = ref<UploadFileResponse | null>(null);
const selectedDirectoryId = ref<string | null>(props.directoryId || null);

// Parent directory search
const parentDirectoryOptions = ref<SelectMenuItem[]>([]);
const isLoadingParentDirs = ref(false);

// Format bytes to human readable
function formatBytes(bytes: number, decimals = 2): string {
  if (bytes === 0) return "0 Bytes";
  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ["Bytes", "KB", "MB", "GB", "TB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + " " + sizes[i];
}

// Remove selected file
function removeFile() {
  selectedFile.value = null;
  uploadResult.value = null;
  uploadProgress.value = 0;
}

// Search for parent directories
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
        (o) => o.id === selectedId
      );

      parentDirectoryOptions.value = selectedOption
        ? [selectedOption, ...newOptions.filter((o) => o.id !== selectedId)]
        : newOptions;
    }
  } finally {
    isLoadingParentDirs.value = false;
  }
};

// Check if form is valid
const canUpload = computed(() => {
  return selectedFile.value && !uploading.value;
});

// Upload file

async function uploadFile() {
  if (!canUpload.value) return;

  uploading.value = true;
  uploadProgress.value = 0;
  uploadResult.value = null;

  try {
    const file = selectedFile.value!;

    const hasher = await createSHA256();
    hasher.init();

    // Process file in chunks (memory efficient)
    const chunkSize = 1024 * 1024; // 1MB chunks
    let offset = 0;

    while (offset < file.size) {
      const chunk = file.slice(offset, offset + chunkSize);
      const arrayBuffer = await chunk.arrayBuffer();
      hasher.update(new Uint8Array(arrayBuffer));
      offset += chunkSize;

      // Optional: Show hashing progress
      const hashProgress = Math.round((offset / file.size) * 50); // 0-50%
      uploadProgress.value = hashProgress;
    }

    const fileHash = hasher.digest("hex");
    console.log("Computed SHA-256:", fileHash);

    const formData = new FormData();
    formData.append("filename", file.name);
    formData.append("sha256", fileHash);

    if (selectedDirectoryId.value) {
      formData.append("directoryId", selectedDirectoryId.value);
    }

    formData.append("file", file);

    const response = await apiClient.post<UploadFileResponse>(
      "/files/upload",
      formData,
      {
        headers: {
          "Content-Type": "multipart/form-data",
        },
        onUploadProgress: (progressEvent) => {
          if (progressEvent.total) {
            // Upload progress is 50-100%
            const uploadPct = Math.round(
              (progressEvent.loaded * 100) / progressEvent.total
            );
            uploadProgress.value = 50 + uploadPct / 2;
          }
        },
      }
    );

    uploadResult.value = response.data;

    toast.add({
      title: "Upload Successful",
      description: `${file.name} uploaded successfully`,
      color: "success",
    });

    setTimeout(() => {
      emit("close", true);
    }, 1000);
  } catch (error: any) {
    const errorMessage =
      error.response?.data?.message ||
      error.response?.data?.error ||
      error.message ||
      "An unknown error occurred";

    toast.add({
      title: "Upload Failed",
      description: errorMessage,
      color: "error",
    });
  } finally {
    uploading.value = false;
  }
}
</script>

<template>
  <UModal
    :close="{ onClick: () => emit('close', false) }"
    title="Upload File"
    :ui="{ body: 'space-y-6' }"
  >
    <template #body>
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

      <!-- File Upload Area -->
      <div class="space-y-4">
        <UFileUpload
          v-if="!selectedFile"
          v-model="selectedFile"
          label="Drop your file here"
          description="Any file type, any size"
          icon="i-lucide-upload"
          class="min-h-48"
          :disabled="uploading"
        >
          <template #actions="{ open }">
            <UButton
              label="Select File"
              icon="i-lucide-file-plus"
              color="neutral"
              variant="outline"
              @click="open()"
            />
          </template>
        </UFileUpload>

        <!-- Selected File Preview -->
        <div v-else class="border rounded-lg p-4 space-y-4">
          <div class="flex items-start justify-between gap-4">
            <div class="flex items-center gap-3 flex-1 min-w-0">
              <div class="shrink-0">
                <UIcon name="i-lucide-file" class="size-10 text-primary" />
              </div>
              <div class="flex-1 min-w-0">
                <p class="font-medium truncate">
                  {{ selectedFile.name }}
                </p>
                <p class="text-sm text-muted">
                  {{ formatBytes(selectedFile.size) }}
                </p>
              </div>
            </div>
            <UButton
              v-if="!uploading"
              icon="i-lucide-x"
              color="neutral"
              variant="ghost"
              size="sm"
              @click="removeFile"
              aria-label="Remove file"
            />
          </div>

          <!-- Upload Progress -->
          <div v-if="uploading" class="space-y-2">
            <div class="flex items-center justify-between text-sm">
              <span class="text-muted">Uploading...</span>
              <span class="font-medium">{{ uploadProgress }}%</span>
            </div>
            <UProgress :value="uploadProgress" />
          </div>

          <!-- Upload Success -->
          <div
            v-if="uploadResult"
            class="flex items-center gap-2 text-sm text-success"
          >
            <UIcon name="i-lucide-check-circle" class="size-4" />
            <span>Upload completed successfully!</span>
          </div>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-between w-full gap-2">
        <UButton
          color="neutral"
          variant="ghost"
          label="Cancel"
          :disabled="uploading"
          @click="emit('close', false)"
        />
        <UButton
          label="Upload"
          icon="i-lucide-upload"
          :loading="uploading"
          :disabled="!canUpload"
          @click="uploadFile"
        />
      </div>
    </template>
  </UModal>
</template>
