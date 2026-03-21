<script setup lang="ts">
import { computed, nextTick, ref, watch } from "vue";
import { fileApi } from "@/api/file";
import { useDirectoryStore } from "@/stores/directory";
import type { SelectMenuItem } from "@nuxt/ui";
import { formatBytes } from "@/utils/size.utils";
import type { WorkerOutMessage } from "@/workers/blake3.worker";

const toast = useToast();
const directoryStore = useDirectoryStore();

// enums & constants

enum UploadStage {
  IDLE = "idle",
  HASHING = "hashing",
  INITIALIZING = "initializing",
  UPLOADING = "uploading",
  FINALIZING = "finalizing",
  COMPLETE = "complete",
  ERROR = "error",
}

const PIPELINE_STAGES = [
  UploadStage.HASHING,
  UploadStage.INITIALIZING,
  UploadStage.UPLOADING,
  UploadStage.FINALIZING,
] as const;

type PipelineStage = (typeof PIPELINE_STAGES)[number];

const STAGE_STEPS: { stage: PipelineStage; label: string; icon: string }[] = [
  { icon: "i-lucide-hash", label: "Hash", stage: UploadStage.HASHING },
  { icon: "i-lucide-rocket", label: "Prep", stage: UploadStage.INITIALIZING },
  { icon: "i-lucide-upload", label: "Upload", stage: UploadStage.UPLOADING },
  { icon: "i-lucide-shield-check", label: "Verify", stage: UploadStage.FINALIZING },
];

const STAGE_RANGES: Partial<Record<UploadStage, { start: number; end: number }>> = {
  [UploadStage.HASHING]: { end: 25, start: 0 },
  [UploadStage.INITIALIZING]: { end: 30, start: 25 },
  [UploadStage.UPLOADING]: { end: 90, start: 30 },
  [UploadStage.FINALIZING]: { end: 100, start: 90 },
  [UploadStage.COMPLETE]: { end: 100, start: 100 },
};

// types

interface FileUploadState {
  id: string;
  file: File;
  stage: UploadStage;
  stageProgress: number;
  overallProgress: number;
  uploadId: string | null;
  fileHash: string | null;
  errorMessage: string | null;
  errorAtStage: UploadStage | null;
  abortController: AbortController;
}

type StageStatus = "pending" | "active" | "complete" | "error";

// props & emits

const props = defineProps<{
  directoryId?: string;
  directoryName?: string;
  droppedFiles?: File[];
}>();

const emit = defineEmits<{ close: [boolean] }>();

// state

const createUploadState = (file: File): FileUploadState => ({
  abortController: new AbortController(),
  errorAtStage: null,
  errorMessage: null,
  file,
  fileHash: null,
  id: crypto.randomUUID(),
  overallProgress: 0,
  stage: UploadStage.IDLE,
  stageProgress: 0,
  uploadId: null,
});

const uploads = ref<FileUploadState[]>((props.droppedFiles ?? []).map(createUploadState));

const selectedDirectoryId = ref<string | null>(props.directoryId ?? null);

const parentDirectoryOptions = ref<SelectMenuItem[]>(
  props.directoryId && props.directoryName
    ? [{ id: props.directoryId, label: props.directoryName }]
    : [],
);

const isLoadingParentDirs = ref(false);

// used as a temporary sink for the UFileUpload component; consumed & reset each time
const filePickerValue = ref<File[]>([]);

// ref for the hidden <input> used by the "Add more" button
const addMoreInputRef = ref<HTMLInputElement | null>(null);

watch(filePickerValue, (val) => {
  if (!val || val.length === 0) return;
  addFiles(val);
  nextTick(() => {
    filePickerValue.value = [];
  });
});

// computed

const isUploading = computed(() => uploads.value.some((u) => isPipeline(u.stage)));

const allComplete = computed(
  () => uploads.value.length > 0 && uploads.value.every((u) => u.stage === UploadStage.COMPLETE),
);

const hasFailures = computed(() => uploads.value.some((u) => u.stage === UploadStage.ERROR));

const idleCount = computed(() => uploads.value.filter((u) => u.stage === UploadStage.IDLE).length);

const completedCount = computed(
  () => uploads.value.filter((u) => u.stage === UploadStage.COMPLETE).length,
);

const canUpload = computed(() => idleCount.value > 0 && !isUploading.value);

const showProgressSummary = computed(
  () => isUploading.value || allComplete.value || hasFailures.value,
);

const overallSummaryProgress = computed(() => {
  if (uploads.value.length === 0) return 0;
  const sum = uploads.value.reduce((acc, u) => acc + u.overallProgress, 0);
  return Math.round(sum / uploads.value.length);
});

const summaryBarColor = computed(() => {
  if (allComplete.value) return "success" as const;
  if (hasFailures.value && !isUploading.value) return "error" as const;
  return "primary" as const;
});

// helpers

const isPipeline = (stage: UploadStage): boolean =>
  (PIPELINE_STAGES as readonly UploadStage[]).includes(stage);

const updateProgress = (state: FileUploadState, stagePercent: number) => {
  const range = STAGE_RANGES[state.stage];
  if (!range) return;
  state.stageProgress = stagePercent;
  state.overallProgress = range.start + (stagePercent * (range.end - range.start)) / 100;
};

const getStageStatus = (state: FileUploadState, target: UploadStage): StageStatus => {
  if (state.stage === UploadStage.COMPLETE) return "complete";
  if (state.stage === UploadStage.IDLE) return "pending";

  if (state.stage === UploadStage.ERROR && state.errorAtStage) {
    const errIdx = PIPELINE_STAGES.indexOf(state.errorAtStage as PipelineStage);
    const targetIdx = PIPELINE_STAGES.indexOf(target as PipelineStage);
    if (errIdx === -1) return "error";
    return targetIdx < errIdx ? "complete" : "error";
  }

  const currentIdx = PIPELINE_STAGES.indexOf(state.stage as PipelineStage);
  const targetIdx = PIPELINE_STAGES.indexOf(target as PipelineStage);

  if (targetIdx < currentIdx) return "complete";
  if (targetIdx === currentIdx) return "active";
  return "pending";
};

// worker-based hashing

const hashWithWorker = (
  file: File,
  id: string,
  onProgress: (pct: number) => void,
  signal: AbortSignal,
): Promise<string> =>
  new Promise((resolve, reject) => {
    const worker = new Worker(new URL("@/workers/blake3.worker.ts", import.meta.url), {
      type: "module",
    });

    const onAbort = () => {
      worker.terminate();
      reject(new DOMException("Upload cancelled", "AbortError"));
    };

    signal.addEventListener("abort", onAbort, { once: true });

    worker.onmessage = ({ data }: MessageEvent<WorkerOutMessage>) => {
      if (data.id !== id) return;

      if (data.type === "progress") {
        onProgress(data.percent);
      } else if (data.type === "done") {
        signal.removeEventListener("abort", onAbort);
        worker.terminate();
        resolve(data.hash);
      } else if (data.type === "error") {
        signal.removeEventListener("abort", onAbort);
        worker.terminate();
        reject(new Error(data.message));
      }
    };

    worker.onerror = (err) => {
      signal.removeEventListener("abort", onAbort);
      worker.terminate();
      reject(err);
    };

    worker.postMessage({ file, id });
  });

// concurrency limiter

const runConcurrent = async <T,>(
  tasks: (() => Promise<T>)[],
  limit: number,
): Promise<PromiseSettledResult<T>[]> => {
  const results: PromiseSettledResult<T>[] = new Array(tasks.length);
  let cursor = 0;

  const runSlot = async () => {
    while (cursor < tasks.length) {
      const i = cursor++;
      try {
        results[i] = { status: "fulfilled", value: await tasks[i]() };
      } catch (e) {
        results[i] = { status: "rejected", reason: e };
      }
    }
  };

  await Promise.all(Array.from({ length: Math.min(limit, tasks.length) }, runSlot));
  return results;
};

// upload pipeline

const uploadSingle = async (state: FileUploadState) => {
  const { signal } = state.abortController;

  // hashing
  state.stage = UploadStage.HASHING;
  updateProgress(state, 0);
  const hash = await hashWithWorker(
    state.file,
    state.id,
    (pct) => updateProgress(state, pct),
    signal,
  );
  state.fileHash = hash;

  // initializing
  state.stage = UploadStage.INITIALIZING;
  updateProgress(state, 0);
  const response = await fileApi.initializeUpload({
    contentLength: state.file.size,
    contentType: state.file.type || "application/octet-stream",
    directoryId: selectedDirectoryId.value,
    hash,
  });
  state.uploadId = response.uploadId;
  updateProgress(state, 100);

  // uploading
  state.stage = UploadStage.UPLOADING;
  updateProgress(state, 0);
  await fileApi.uploadToS3(
    response.uploadUrl,
    state.file,
    (pct) => updateProgress(state, pct),
    signal,
  );

  // finalizing
  state.stage = UploadStage.FINALIZING;
  updateProgress(state, 0);
  await fileApi.finalizeUpload({
    directoryId: selectedDirectoryId.value || undefined,
    fileName: state.file.name,
    uploadId: state.uploadId!,
  });
  updateProgress(state, 100);

  state.stage = UploadStage.COMPLETE;
  state.overallProgress = 100;
};

const startUpload = async () => {
  if (!canUpload.value) return;

  const pending = uploads.value.filter((u) => u.stage === UploadStage.IDLE);

  const tasks = pending.map((state) => async () => {
    try {
      await uploadSingle(state);
    } catch (err: any) {
      const isCancelled = err instanceof DOMException && err.name === "AbortError";
      state.errorAtStage = state.stage;
      state.stage = UploadStage.ERROR;
      state.errorMessage = isCancelled
        ? "Cancelled"
        : (err.response?.data?.message ??
          err.response?.data?.error ??
          err.message ??
          "Unknown error");

      if (!isCancelled) {
        toast.add({ color: "error", description: state.errorMessage!, title: "Upload failed" });
      }
    }
  });

  await runConcurrent(tasks, 4);

  if (allComplete.value) {
    toast.add({ color: "success", title: "All files uploaded successfully" });
    setTimeout(() => emit("close", true), 1000);
  }
};

// file management

const addFiles = (files: File[]) => {
  const existing = new Set(uploads.value.map((u) => `${u.file.name}-${u.file.size}`));
  const deduped = files.filter((f) => !existing.has(`${f.name}-${f.size}`));
  uploads.value.push(...deduped.map(createUploadState));
};

const handleAddMoreChange = (e: Event) => {
  const input = e.target as HTMLInputElement;
  const files = Array.from(input.files ?? []);
  if (files.length) addFiles(files);
  input.value = "";
};

const handleRemoveOrCancel = (state: FileUploadState) => {
  if (isPipeline(state.stage)) {
    state.abortController.abort();
    state.errorAtStage = state.stage;
    state.stage = UploadStage.ERROR;
    state.errorMessage = "Cancelled";
  } else {
    uploads.value = uploads.value.filter((u) => u.id !== state.id);
  }
};

const retryFailed = async () => {
  uploads.value
    .filter((u) => u.stage === UploadStage.ERROR)
    .forEach((u) => {
      u.stage = UploadStage.IDLE;
      u.overallProgress = 0;
      u.stageProgress = 0;
      u.errorMessage = null;
      u.errorAtStage = null;
      u.uploadId = null;
      u.fileHash = null;
      u.abortController = new AbortController();
    });
  await startUpload();
};

// directory search

const searchParentDirectory = async (query: string) => {
  if (!query.trim()) return;

  isLoadingParentDirs.value = true;
  try {
    const response = await directoryStore.searchDirectory({
      isDeleted: false,
      nameContains: query,
      pageSize: 20,
    });

    if (response.success && response.data) {
      const newOptions = response.data.items.map((d) => ({ id: d.id, label: d.name }));
      const selectedId = selectedDirectoryId.value;
      const kept = parentDirectoryOptions.value.find((o) => o.id === selectedId);
      parentDirectoryOptions.value = kept
        ? [kept, ...newOptions.filter((o) => o.id !== selectedId)]
        : newOptions;
    }
  } finally {
    isLoadingParentDirs.value = false;
  }
};
</script>

<template>
  <UModal
    :close="{ onClick: () => emit('close', false) }"
    title="Upload Files"
    :ui="{ body: 'space-y-4' }"
  >
    <template #body>
      <!-- directory selector -->
      <div class="space-y-1.5">
        <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Upload to Directory
        </label>
        <USelectMenu
          v-model="selectedDirectoryId"
          :items="parentDirectoryOptions"
          :loading="isLoadingParentDirs"
          :disabled="isUploading"
          placeholder="Search for directory..."
          value-key="id"
          display-key="label"
          searchable
          :debounce="300"
          class="w-full"
          @update:search-term="searchParentDirectory"
        >
          <template #default="{ modelValue }">
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-folder" class="size-4 text-muted" />
              <span v-if="modelValue">
                {{ parentDirectoryOptions.find((i) => i.id === modelValue)?.label }}
              </span>
              <span v-else class="text-muted">Root directory</span>
            </div>
          </template>
        </USelectMenu>
        <p class="text-xs text-muted">Leave empty to upload to the root directory</p>
      </div>

      <!-- empty state: drop zone -->
      <UFileUpload
        v-if="uploads.length === 0"
        v-model="filePickerValue"
        multiple
        label="Drop files here"
        description="Any file type — up to 4 upload in parallel"
        icon="i-lucide-upload"
        class="min-h-44"
        :disabled="isUploading"
      >
        <template #actions="{ open }">
          <UButton
            label="Select Files"
            icon="i-lucide-file-plus"
            color="neutral"
            variant="outline"
            @click="open()"
          />
        </template>
      </UFileUpload>

      <!-- file list -->
      <div
        v-else
        class="rounded-lg border border-gray-200/70 dark:border-gray-700/70 overflow-hidden"
      >
        <!-- rows -->
        <div class="divide-y divide-gray-100/50 dark:divide-gray-800/50 max-h-72 overflow-y-auto">
          <div
            v-for="upload in uploads"
            :key="upload.id"
            class="flex items-start gap-3 px-3 py-2.5"
          >
            <!-- file icon -->
            <UIcon name="i-lucide-file" class="size-5 text-muted mt-0.5 shrink-0" />

            <!-- content -->
            <div class="flex-1 min-w-0 space-y-1.5">
              <div class="flex items-center justify-between gap-2">
                <p class="text-sm font-medium truncate text-gray-700 dark:text-gray-300">
                  {{ upload.file.name }}
                </p>

                <div class="flex items-center gap-2 shrink-0">
                  <!-- stage dots -->
                  <div
                    v-if="upload.stage !== UploadStage.IDLE"
                    class="flex items-center gap-0.5"
                    :title="upload.stage"
                  >
                    <template v-for="step in STAGE_STEPS" :key="step.stage">
                      <UIcon
                        v-if="getStageStatus(upload, step.stage) === 'complete'"
                        name="i-lucide-check-circle"
                        class="size-3.5 text-success"
                        :title="step.label"
                      />
                      <UIcon
                        v-else-if="getStageStatus(upload, step.stage) === 'active'"
                        name="i-lucide-loader-circle"
                        class="size-3.5 text-primary animate-spin"
                        :title="step.label"
                      />
                      <UIcon
                        v-else-if="getStageStatus(upload, step.stage) === 'error'"
                        name="i-lucide-x-circle"
                        class="size-3.5 text-error"
                        :title="step.label"
                      />
                      <UIcon
                        v-else
                        name="i-lucide-circle"
                        class="size-3.5 text-muted"
                        :title="step.label"
                      />
                    </template>
                  </div>

                  <!-- progress percentage -->
                  <span
                    v-if="isPipeline(upload.stage)"
                    class="text-xs tabular-nums text-muted w-8 text-right"
                  >
                    {{ Math.round(upload.overallProgress) }}%
                  </span>

                  <!-- cancel / remove -->
                  <UButton
                    icon="i-lucide-x"
                    size="xs"
                    variant="ghost"
                    color="neutral"
                    :aria-label="isPipeline(upload.stage) ? 'Cancel upload' : 'Remove file'"
                    @click="handleRemoveOrCancel(upload)"
                  />
                </div>
              </div>

              <!-- size + status label -->
              <div class="flex items-center gap-2">
                <span class="text-xs text-muted">{{ formatBytes(upload.file.size) }}</span>
                <span v-if="upload.stage === UploadStage.IDLE" class="text-xs text-muted">
                  Ready
                </span>
                <span
                  v-else-if="upload.stage === UploadStage.COMPLETE"
                  class="text-xs text-success"
                >
                  Uploaded
                </span>
                <span
                  v-else-if="upload.stage === UploadStage.ERROR"
                  class="text-xs text-error truncate max-w-48"
                >
                  {{ upload.errorMessage }}
                </span>
              </div>

              <!-- per-file progress bar -->
              <UProgress
                v-if="upload.stage !== UploadStage.IDLE"
                :value="upload.overallProgress"
                :color="
                  upload.stage === UploadStage.ERROR
                    ? 'error'
                    : upload.stage === UploadStage.COMPLETE
                      ? 'success'
                      : 'primary'
                "
                size="xs"
              />
            </div>
          </div>
        </div>

        <!-- add more -->
        <div
          v-if="!isUploading"
          class="px-3 py-2 border-t border-gray-200/70 dark:border-gray-700/70"
        >
          <UButton
            icon="i-lucide-plus"
            label="Add more files"
            variant="ghost"
            color="neutral"
            size="sm"
            @click="addMoreInputRef?.click()"
          />
          <input
            ref="addMoreInputRef"
            type="file"
            multiple
            class="hidden"
            @change="handleAddMoreChange"
          />
        </div>

        <!-- overall summary -->
        <Transition
          enter-active-class="transition-all duration-200 ease-out"
          leave-active-class="transition-all duration-150 ease-in"
          enter-from-class="opacity-0"
          leave-to-class="opacity-0"
        >
          <div
            v-if="showProgressSummary"
            class="px-3 py-2.5 border-t border-gray-200/70 dark:border-gray-700/70 bg-white/40 dark:bg-white/3"
          >
            <div class="flex items-center justify-between text-xs text-muted mb-1.5">
              <span>{{ completedCount }} of {{ uploads.length }} uploaded</span>
              <span class="tabular-nums">{{ overallSummaryProgress }}%</span>
            </div>
            <UProgress :value="overallSummaryProgress" :color="summaryBarColor" size="xs" />
          </div>
        </Transition>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-between w-full gap-2">
        <UButton
          color="neutral"
          variant="outline"
          label="Cancel"
          :disabled="isUploading"
          @click="emit('close', false)"
        />

        <div class="flex gap-2">
          <UButton
            v-if="hasFailures && !isUploading"
            label="Retry Failed"
            icon="i-lucide-rotate-ccw"
            color="neutral"
            variant="outline"
            @click="retryFailed"
          />
          <UButton
            v-if="canUpload"
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
