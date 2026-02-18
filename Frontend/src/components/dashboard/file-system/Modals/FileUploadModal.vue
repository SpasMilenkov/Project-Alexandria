<script setup lang="ts">
import { ref, computed } from "vue";
import { fileApi } from "@/api/file";
import { useDirectoryStore } from "@/stores/directory";
import type { SelectMenuItem } from "@nuxt/ui";
import { createBLAKE3 } from "hash-wasm";

const toast = useToast();
const directoryStore = useDirectoryStore();

// Upload stages
enum UploadStage {
  IDLE = "idle",
  HASHING = "hashing",
  INITIALIZING = "initializing",
  UPLOADING = "uploading",
  FINALIZING = "finalizing",
  COMPLETE = "complete",
  ERROR = "error",
}

interface StageConfig {
  label: string;
  icon: string;
  progressStart: number;
  progressEnd: number;
}

const stageConfig: Record<UploadStage, StageConfig | null> = {
  [UploadStage.IDLE]: null,
  [UploadStage.HASHING]: {
    label: "Calculating hash",
    icon: "i-lucide-hash",
    progressStart: 0,
    progressEnd: 25,
  },
  [UploadStage.INITIALIZING]: {
    label: "Preparing upload",
    icon: "i-lucide-rocket",
    progressStart: 25,
    progressEnd: 30,
  },
  [UploadStage.UPLOADING]: {
    label: "Uploading file",
    icon: "i-lucide-upload",
    progressStart: 30,
    progressEnd: 90,
  },
  [UploadStage.FINALIZING]: {
    label: "Verifying upload",
    icon: "i-lucide-check-circle",
    progressStart: 90,
    progressEnd: 100,
  },
  [UploadStage.COMPLETE]: {
    label: "Complete",
    icon: "i-lucide-check-circle-2",
    progressStart: 100,
    progressEnd: 100,
  },
  [UploadStage.ERROR]: {
    label: "Error",
    icon: "i-lucide-alert-circle",
    progressStart: 0,
    progressEnd: 0,
  },
};

const props = defineProps<{
  directoryId?: string;
  /**
   * Human-readable name for directoryId. When provided the select menu shows
   * this label immediately without needing a search round-trip.
   */
  directoryName?: string;
  /**
   * Pre-seeded from a drag-and-drop. When provided the modal skips the
   * file-picker and goes straight to the ready-to-upload state.
   */
  droppedFile?: File;
}>();

const emit = defineEmits<{ close: [boolean] }>();

// State — seed selectedFile from the drop if one was provided
const selectedFile = ref<File | null>(props.droppedFile ?? null);
const currentStage = ref<UploadStage>(UploadStage.IDLE);
const overallProgress = ref(0);
const stageProgress = ref(0);
const errorMessage = ref<string | null>(null);
const uploadId = ref<string | null>(null);
const fileHash = ref<string | null>(null);
const selectedDirectoryId = ref<string | null>(props.directoryId || null);

// Parent directory search — pre-seed with the active directory so the select
// menu renders its label immediately rather than showing the raw UUID.
const parentDirectoryOptions = ref<SelectMenuItem[]>(
  props.directoryId && props.directoryName
    ? [{ label: props.directoryName, id: props.directoryId }]
    : [],
);
const isLoadingParentDirs = ref(false);

// Computed properties
const isUploading = computed(() => {
  return (
    currentStage.value !== UploadStage.IDLE &&
    currentStage.value !== UploadStage.COMPLETE &&
    currentStage.value !== UploadStage.ERROR
  );
});

const canUpload = computed(() => {
  return selectedFile.value && !isUploading.value;
});

const canRetry = computed(() => {
  return currentStage.value === UploadStage.ERROR && selectedFile.value;
});

// Helper functions
function formatBytes(bytes: number, decimals = 2): string {
  if (bytes === 0) return "0 Bytes";
  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ["Bytes", "KB", "MB", "GB", "TB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + " " + sizes[i];
}

function updateProgress(stage: UploadStage, stagePercent: number) {
  const config = stageConfig[stage];
  if (!config) return;

  stageProgress.value = stagePercent;
  overallProgress.value =
    config.progressStart +
    (stagePercent * (config.progressEnd - config.progressStart)) / 100;
}

function getStageStatus(
  stage: UploadStage,
): "pending" | "active" | "complete" | "error" {
  if (currentStage.value === UploadStage.ERROR) {
    const errorIndex = Object.values(UploadStage).indexOf(currentStage.value);
    const stageIndex = Object.values(UploadStage).indexOf(stage);
    return stageIndex <= errorIndex ? "error" : "pending";
  }

  const currentIndex = Object.values(UploadStage).indexOf(currentStage.value);
  const stageIndex = Object.values(UploadStage).indexOf(stage);

  if (stageIndex < currentIndex) return "complete";
  if (stageIndex === currentIndex) return "active";
  return "pending";
}

function removeFile() {
  if (isUploading.value) return;

  selectedFile.value = null;
  currentStage.value = UploadStage.IDLE;
  overallProgress.value = 0;
  stageProgress.value = 0;
  errorMessage.value = null;
  uploadId.value = null;
  fileHash.value = null;
}

const searchParentDirectory = async (query: string) => {
  if (!query.trim()) return;

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

async function computeHash(): Promise<string> {
  currentStage.value = UploadStage.HASHING;
  updateProgress(UploadStage.HASHING, 0);

  const file = selectedFile.value!;
  const hasher = await createBLAKE3();
  hasher.init();

  const chunkSize = 16 * 1024 * 1024;
  let offset = 0;

  while (offset < file.size) {
    const chunk = file.slice(offset, offset + chunkSize);
    const arrayBuffer = await chunk.arrayBuffer();
    hasher.update(new Uint8Array(arrayBuffer));
    offset += chunkSize;

    const hashProgress = Math.round((offset / file.size) * 100);
    updateProgress(UploadStage.HASHING, hashProgress);
  }

  const hash = hasher.digest("hex");
  fileHash.value = hash;
  return hash;
}

async function initializeUpload(
  hash: string,
): Promise<{ uploadId: string; uploadUrl: string }> {
  currentStage.value = UploadStage.INITIALIZING;
  updateProgress(UploadStage.INITIALIZING, 0);

  const file = selectedFile.value!;

  const response = await fileApi.initializeUpload({
    contentType: file.type || "application/octet-stream",
    hash: hash,
    contentLength: file.size,
    directoryId: selectedDirectoryId.value,
  });

  updateProgress(UploadStage.INITIALIZING, 100);
  uploadId.value = response.uploadId;

  return {
    uploadId: response.uploadId,
    uploadUrl: response.uploadUrl,
  };
}

async function uploadToS3(uploadUrl: string): Promise<void> {
  currentStage.value = UploadStage.UPLOADING;
  updateProgress(UploadStage.UPLOADING, 0);

  const file = selectedFile.value!;

  await fileApi.uploadToS3(uploadUrl, file, (percent) => {
    updateProgress(UploadStage.UPLOADING, percent);
  });
}

async function finalizeUpload(): Promise<void> {
  currentStage.value = UploadStage.FINALIZING;
  updateProgress(UploadStage.FINALIZING, 0);

  const file = selectedFile.value!;

  await fileApi.finalizeUpload({
    uploadId: uploadId.value!,
    directoryId: selectedDirectoryId.value || undefined,
    fileName: file.name,
  });

  updateProgress(UploadStage.FINALIZING, 100);
  currentStage.value = UploadStage.COMPLETE;
}

async function startUpload() {
  if (!canUpload.value) return;

  errorMessage.value = null;

  try {
    const hash = await computeHash();
    const { uploadUrl } = await initializeUpload(hash);
    await uploadToS3(uploadUrl);
    await finalizeUpload();

    toast.add({
      title: "Upload Successful",
      description: `${selectedFile.value!.name} uploaded successfully`,
      color: "success",
    });

    setTimeout(() => {
      emit("close", true);
    }, 1000);
  } catch (error: any) {
    currentStage.value = UploadStage.ERROR;

    const errorMsg =
      error.response?.data?.message ||
      error.response?.data?.error ||
      error.message ||
      "An unknown error occurred";

    errorMessage.value = errorMsg;

    toast.add({
      title: "Upload Failed",
      description: errorMsg,
      color: "error",
    });
  }
}

function retryUpload() {
  errorMessage.value = null;
  currentStage.value = UploadStage.IDLE;
  overallProgress.value = 0;
  stageProgress.value = 0;
  uploadId.value = null;
  fileHash.value = null;
  startUpload();
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
          :disabled="isUploading"
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
              <span v-else class="text-muted">Root directory (no parent)</span>
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
          :disabled="isUploading"
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
                <p class="font-medium truncate">{{ selectedFile.name }}</p>
                <p class="text-sm text-muted">
                  {{ formatBytes(selectedFile.size) }}
                </p>
              </div>
            </div>
            <UButton
              v-if="!isUploading"
              icon="i-lucide-x"
              color="neutral"
              variant="ghost"
              size="sm"
              @click="removeFile"
              aria-label="Remove file"
            />
          </div>

          <!-- Multi-Stage Progress Indicator -->
          <div
            v-if="
              isUploading ||
              currentStage === UploadStage.COMPLETE ||
              currentStage === UploadStage.ERROR
            "
            class="space-y-4"
          >
            <div class="space-y-3">
              <!-- Hashing Stage -->
              <div class="flex items-center gap-3">
                <div class="shrink-0">
                  <UIcon
                    v-if="getStageStatus(UploadStage.HASHING) === 'complete'"
                    name="i-lucide-check-circle"
                    class="size-5 text-success"
                  />
                  <UIcon
                    v-else-if="getStageStatus(UploadStage.HASHING) === 'active'"
                    name="i-lucide-loader-circle"
                    class="size-5 text-primary animate-spin"
                  />
                  <UIcon
                    v-else-if="getStageStatus(UploadStage.HASHING) === 'error'"
                    name="i-lucide-x-circle"
                    class="size-5 text-error"
                  />
                  <UIcon
                    v-else
                    name="i-lucide-circle"
                    class="size-5 text-muted"
                  />
                </div>
                <div class="flex-1 min-w-0">
                  <div class="flex items-center justify-between gap-2">
                    <span class="text-sm font-medium">Calculating hash</span>
                    <span
                      v-if="currentStage === UploadStage.HASHING"
                      class="text-xs text-muted"
                    >
                      {{ Math.round(stageProgress) }}%
                    </span>
                  </div>
                </div>
              </div>

              <!-- Initializing Stage -->
              <div class="flex items-center gap-3">
                <div class="shrink-0">
                  <UIcon
                    v-if="
                      getStageStatus(UploadStage.INITIALIZING) === 'complete'
                    "
                    name="i-lucide-check-circle"
                    class="size-5 text-success"
                  />
                  <UIcon
                    v-else-if="
                      getStageStatus(UploadStage.INITIALIZING) === 'active'
                    "
                    name="i-lucide-loader-circle"
                    class="size-5 text-primary animate-spin"
                  />
                  <UIcon
                    v-else-if="
                      getStageStatus(UploadStage.INITIALIZING) === 'error'
                    "
                    name="i-lucide-x-circle"
                    class="size-5 text-error"
                  />
                  <UIcon
                    v-else
                    name="i-lucide-circle"
                    class="size-5 text-muted"
                  />
                </div>
                <div class="flex-1 min-w-0">
                  <span class="text-sm font-medium">Preparing upload</span>
                </div>
              </div>

              <!-- Uploading Stage -->
              <div class="flex items-center gap-3">
                <div class="shrink-0">
                  <UIcon
                    v-if="getStageStatus(UploadStage.UPLOADING) === 'complete'"
                    name="i-lucide-check-circle"
                    class="size-5 text-success"
                  />
                  <UIcon
                    v-else-if="
                      getStageStatus(UploadStage.UPLOADING) === 'active'
                    "
                    name="i-lucide-loader-circle"
                    class="size-5 text-primary animate-spin"
                  />
                  <UIcon
                    v-else-if="
                      getStageStatus(UploadStage.UPLOADING) === 'error'
                    "
                    name="i-lucide-x-circle"
                    class="size-5 text-error"
                  />
                  <UIcon
                    v-else
                    name="i-lucide-circle"
                    class="size-5 text-muted"
                  />
                </div>
                <div class="flex-1 min-w-0">
                  <div class="flex items-center justify-between gap-2">
                    <span class="text-sm font-medium">Uploading file</span>
                    <span
                      v-if="currentStage === UploadStage.UPLOADING"
                      class="text-xs text-muted"
                    >
                      {{ Math.round(stageProgress) }}%
                    </span>
                  </div>
                </div>
              </div>

              <!-- Finalizing Stage -->
              <div class="flex items-center gap-3">
                <div class="shrink-0">
                  <UIcon
                    v-if="getStageStatus(UploadStage.FINALIZING) === 'complete'"
                    name="i-lucide-check-circle"
                    class="size-5 text-success"
                  />
                  <UIcon
                    v-else-if="
                      getStageStatus(UploadStage.FINALIZING) === 'active'
                    "
                    name="i-lucide-loader-circle"
                    class="size-5 text-primary animate-spin"
                  />
                  <UIcon
                    v-else-if="
                      getStageStatus(UploadStage.FINALIZING) === 'error'
                    "
                    name="i-lucide-x-circle"
                    class="size-5 text-error"
                  />
                  <UIcon
                    v-else
                    name="i-lucide-circle"
                    class="size-5 text-muted"
                  />
                </div>
                <div class="flex-1 min-w-0">
                  <span class="text-sm font-medium">Verifying upload</span>
                </div>
              </div>
            </div>

            <!-- Overall Progress Bar -->
            <div class="space-y-2">
              <div class="flex items-center justify-between text-sm">
                <span class="text-muted">
                  {{
                    currentStage === UploadStage.COMPLETE
                      ? "Complete"
                      : "Overall Progress"
                  }}
                </span>
                <span class="font-medium"
                  >{{ Math.round(overallProgress) }}%</span
                >
              </div>
              <UProgress
                :value="overallProgress"
                :color="
                  currentStage === UploadStage.ERROR
                    ? 'error'
                    : currentStage === UploadStage.COMPLETE
                      ? 'success'
                      : 'primary'
                "
              />
            </div>

            <!-- Error Message -->
            <div
              v-if="currentStage === UploadStage.ERROR && errorMessage"
              class="flex items-start gap-2 p-3 bg-error/10 border border-error/20 rounded-lg"
            >
              <UIcon
                name="i-lucide-alert-circle"
                class="size-5 text-error shrink-0 mt-0.5"
              />
              <div class="flex-1 min-w-0">
                <p class="text-sm font-medium text-error">Upload Failed</p>
                <p class="text-xs text-error/80 mt-1">{{ errorMessage }}</p>
              </div>
            </div>

            <!-- Success Message -->
            <div
              v-if="currentStage === UploadStage.COMPLETE"
              class="flex items-center gap-2 text-sm text-success"
            >
              <UIcon name="i-lucide-check-circle" class="size-4" />
              <span>Upload completed successfully!</span>
            </div>
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
          :disabled="isUploading"
          @click="emit('close', false)"
        />
        <div class="flex gap-2">
          <UButton
            v-if="canRetry"
            label="Retry"
            icon="i-lucide-rotate-ccw"
            color="neutral"
            variant="outline"
            @click="retryUpload"
          />
          <UButton
            v-if="!selectedFile || currentStage === UploadStage.IDLE"
            label="Upload"
            icon="i-lucide-upload"
            :disabled="!canUpload"
            @click="startUpload"
          />
        </div>
      </div>
    </template>
  </UModal>
</template>
