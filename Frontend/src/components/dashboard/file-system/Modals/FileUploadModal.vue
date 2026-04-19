<script setup lang="ts">
import { computed, ref } from "vue";
import { fileApi } from "@/api/file";
import { useDirectoryStore } from "@/stores/directory";
import type { SelectMenuItem } from "@nuxt/ui";
import { formatBytes } from "@/utils/size.utils";
import type { WorkerOutMessage } from "@/workers/blake3.worker";
import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { encryptFile } from "@/composables/useFileEncryption";
import { useAppToast } from "@/composables/useAppToast";

const directoryStore = useDirectoryStore();
const appToast = useAppToast();

// enums & constants

const PBKDF2_ITERATIONS = Number(import.meta.env.VITE_PBKDF2_ITERATIONS) || 800_000;

const ENCRYPTION_MAX_FILE_SIZE = 500 * 1024 * 1024; // 500 MB
const PASSWORD_MIN_LENGTH = 16;

const getPasswordRequirements = (password: string) => [
  { label: "16+ characters", met: password.length >= PASSWORD_MIN_LENGTH },
  { label: "Uppercase letter", met: /[A-Z]/.test(password) },
  { label: "Lowercase letter", met: /[a-z]/.test(password) },
  { label: "Number", met: /[0-9]/.test(password) },
  { label: "Special character", met: /[^a-zA-Z0-9]/.test(password) },
];

const getPasswordScore = (password: string): number =>
  getPasswordRequirements(password).filter((r) => r.met).length;

const PASSWORD_STRENGTH_LEVELS = [
  { barColor: "bg-red-400", label: "Very weak", textColor: "text-red-500" },
  { barColor: "bg-orange-400", label: "Weak", textColor: "text-orange-500" },
  { barColor: "bg-yellow-400", label: "Fair", textColor: "text-yellow-600 dark:text-yellow-400" },
  { barColor: "bg-lime-400", label: "Good", textColor: "text-lime-600 dark:text-lime-400" },
  { barColor: "bg-green-500", label: "Strong", textColor: "text-green-600 dark:text-green-400" },
] as const;

enum UploadStage {
  IDLE = "idle",
  ENCRYPTING = "encrypting",
  HASHING = "hashing",
  INITIALIZING = "initializing",
  UPLOADING = "uploading",
  FINALIZING = "finalizing",
  COMPLETE = "complete",
  ERROR = "error",
}

const PIPELINE_STAGES = [
  UploadStage.ENCRYPTING,
  UploadStage.HASHING,
  UploadStage.INITIALIZING,
  UploadStage.UPLOADING,
  UploadStage.FINALIZING,
] as const;

type PipelineStage = (typeof PIPELINE_STAGES)[number];

useModalBackGuard(() => emit("close", false));

const STAGE_STEPS: { stage: PipelineStage; label: string; icon: string }[] = [
  { icon: "i-lucide-lock", label: "Encrypt", stage: UploadStage.ENCRYPTING },
  { icon: "i-lucide-hash", label: "Hash", stage: UploadStage.HASHING },
  { icon: "i-lucide-rocket", label: "Prep", stage: UploadStage.INITIALIZING },
  { icon: "i-lucide-upload", label: "Upload", stage: UploadStage.UPLOADING },
  { icon: "i-lucide-shield-check", label: "Verify", stage: UploadStage.FINALIZING },
];

const STAGE_RANGES: Partial<Record<UploadStage, { start: number; end: number }>> = {
  [UploadStage.ENCRYPTING]: { start: 0, end: 15 },
  [UploadStage.HASHING]: { start: 15, end: 30 },
  [UploadStage.INITIALIZING]: { start: 30, end: 35 },
  [UploadStage.UPLOADING]: { start: 35, end: 90 },
  [UploadStage.FINALIZING]: { start: 90, end: 100 },
  [UploadStage.COMPLETE]: { start: 100, end: 100 },
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
  isEncrypted: boolean;
  password: string;
  confirmPassword: string;
  encryptionHint: string;
  passwordVisible: boolean;
  confirmPasswordVisible: boolean;
  encryptionIv: string | null;
  encryptionSalt: string | null;
  encryptionAuthTag: string | null;
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
  confirmPassword: "",
  confirmPasswordVisible: false,
  encryptionAuthTag: null,
  encryptionHint: "",
  encryptionIv: null,
  encryptionSalt: null,
  errorAtStage: null,
  errorMessage: null,
  file,
  fileHash: null,
  id: crypto.randomUUID(),
  isEncrypted: false,
  overallProgress: 0,
  password: "",
  passwordVisible: false,
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

const initialInputRef = ref<HTMLInputElement | null>(null);
const addMoreInputRef = ref<HTMLInputElement | null>(null);

const dragCounter = ref(0);
const isDragging = computed(() => dragCounter.value > 0);

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

const passwordsValid = computed(() =>
  uploads.value
    .filter((u) => u.isEncrypted)
    .every(
      (u) =>
        getPasswordRequirements(u.password).every((r) => r.met) && u.password === u.confirmPassword,
    ),
);

const canUpload = computed(() => idleCount.value > 0 && !isUploading.value && passwordsValid.value);

const uploadButtonTooltip = computed(() => {
  if (isUploading.value || idleCount.value === 0) return undefined;
  if (!passwordsValid.value) return "All encrypted files need a strong password before uploading";
  return undefined;
});

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

const canEncrypt = (file: File): boolean => file.size <= ENCRYPTION_MAX_FILE_SIZE;

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

const getPasswordError = (state: FileUploadState): string | null => {
  if (!state.isEncrypted) return null;
  if (state.password.length === 0) return "Password required";
  const unmet = getPasswordRequirements(state.password).filter((r) => !r.met);
  if (unmet.length > 0) return "Password does not meet all requirements";
  if (state.password !== state.confirmPassword) return "Passwords do not match";
  return null;
};

const getLockColor = (state: FileUploadState) => {
  if (!canEncrypt(state.file)) return "neutral" as const;
  if (!state.isEncrypted) return "neutral" as const;
  if (getPasswordError(state)) return "error" as const;
  return "primary" as const;
};

const HTTP_ERROR_MAP: Record<number, string> = {
  400: "The request was invalid. Check your file and try again.",
  401: "Your session expired. Please reload and log back in.",
  403: "You don't have permission to upload here.",
  404: "The upload destination was not found.",
  409: "A file with this name already exists in this directory.",
  413: "The file is too large to upload to this server.",
  422: "The file could not be processed. It may be corrupted.",
  429: "Too many uploads at once. Wait a moment and retry.",
  500: "Something went wrong on the server. Try again in a moment.",
  502: "The server is temporarily unreachable. Try again shortly.",
  503: "The service is currently unavailable. Try again shortly.",
};

const extractErrorMessage = (err: any): string => {
  if (err instanceof DOMException && err.name === "AbortError") return "Cancelled";

  if (err instanceof DOMException || err?.name === "OperationError")
    return "Encryption failed. Check your password and try again.";

  if (err?.code === "ERR_NETWORK" || !err?.response)
    return "Could not reach the server. Check your connection and try again.";

  const status: number | undefined = err?.response?.status;
  if (status && HTTP_ERROR_MAP[status]) return HTTP_ERROR_MAP[status];
  if (status && status >= 500) return "Something went wrong on the server. Try again in a moment.";

  const data = err?.response?.data;
  if (data?.errors && typeof data.errors === "object") {
    const messages = Object.values(data.errors as Record<string, string[]>)
      .flat()
      .filter(Boolean);
    if (messages.length) return messages.join(" · ");
  }
  if (data?.message && data.message.length < 120) return data.message;

  return "An unexpected error occurred. Please try again.";
};

// worker-based hashing

const hashWithWorker = (
  file: File | Blob,
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
        results[i] = { reason: e, status: "rejected" };
      }
    }
  };

  await Promise.all(Array.from({ length: Math.min(limit, tasks.length) }, runSlot));
  return results;
};

// upload pipeline
//oxlint-disable-next-line max-statements
const uploadSingle = async (state: FileUploadState) => {
  const { signal } = state.abortController;

  state.stage = UploadStage.ENCRYPTING;
  updateProgress(state, 0);

  let fileToUpload: File | Blob = state.file;
  let contentType = state.file.type || "application/octet-stream";

  if (state.isEncrypted) {
    const result = await encryptFile(state.file, state.password);
    fileToUpload = new Blob([result.ciphertext], { type: "application/octet-stream" });
    contentType = "application/octet-stream";
    state.encryptionIv = result.iv;
    state.encryptionSalt = result.salt;
    state.encryptionAuthTag = result.authTag;
  }

  updateProgress(state, 100);

  state.stage = UploadStage.HASHING;
  updateProgress(state, 0);
  const hash = await hashWithWorker(
    fileToUpload,
    state.id,
    (pct) => updateProgress(state, pct),
    signal,
  );
  state.fileHash = hash;

  state.stage = UploadStage.INITIALIZING;
  updateProgress(state, 0);
  const response = await fileApi.initializeUpload({
    contentLength: fileToUpload.size,
    contentType,
    directoryId: selectedDirectoryId.value,
    hash,
  });
  state.uploadId = response.uploadId;
  updateProgress(state, 100);

  state.stage = UploadStage.UPLOADING;
  updateProgress(state, 0);
  await fileApi.uploadToS3(
    response.uploadUrl,
    fileToUpload as File,
    (pct) => updateProgress(state, pct),
    signal,
  );

  state.stage = UploadStage.FINALIZING;
  updateProgress(state, 0);
  await fileApi.finalizeUpload({
    directoryId: selectedDirectoryId.value || undefined,
    encryptionHint: state.encryptionHint || undefined,
    encryptionIv: state.encryptionIv ?? undefined,
    encryptionSalt: state.encryptionSalt ?? undefined,
    fileName: state.file.name,
    integrityTag: state.encryptionAuthTag ?? undefined,
    isEncrypted: state.isEncrypted,
    iterationCount: state.isEncrypted ? PBKDF2_ITERATIONS : undefined,
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
      state.errorMessage = isCancelled ? "Cancelled" : extractErrorMessage(err);
      if (!isCancelled) {
        appToast.error("Upload failed", state.errorMessage);
      }
    }
  });

  await runConcurrent(tasks, 4);

  if (allComplete.value) {
    appToast.success("All files uploaded successfully");
    setTimeout(() => emit("close", true), 1000);
  }
};

// file management

const addFiles = (files: File[]) => {
  const existing = new Set(uploads.value.map((u) => `${u.file.name}-${u.file.size}`));
  const deduped = files.filter((f) => !existing.has(`${f.name}-${f.size}`));
  uploads.value.push(...deduped.map(createUploadState));
};

const handleInitialChange = (e: Event) => {
  const input = e.target as HTMLInputElement;
  const files = Array.from(input.files ?? []);
  if (files.length) addFiles(files);
  input.value = "";
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
      u.encryptionIv = null;
      u.encryptionSalt = null;
      u.encryptionAuthTag = null;
    });
  await startUpload();
};

// drag and drop handlers

const onDragEnter = (e: DragEvent) => {
  e.preventDefault();
  dragCounter.value++;
};

const onDragLeave = (e: DragEvent) => {
  e.preventDefault();
  dragCounter.value--;
};

const onDragOver = (e: DragEvent) => {
  e.preventDefault();
};

const onDrop = (e: DragEvent) => {
  e.preventDefault();
  dragCounter.value = 0;
  if (e.dataTransfer?.files) {
    addFiles(Array.from(e.dataTransfer.files));
  }
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
    :ui="{ content: 'sm:max-w-2xl' }"
    title="Upload Files"
  >
    <template #body>
      <div class="space-y-6">
        <!-- directory selector -->
        <div class="space-y-2">
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

        <!-- Drop Zone Wrapper -->
        <div
          class="relative"
          @dragenter="onDragEnter"
          @dragleave="onDragLeave"
          @dragover="onDragOver"
          @drop="onDrop"
        >
          <!-- empty state: drop zone -->
          <div
            v-if="uploads.length === 0"
            class="sm:min-h-80 rounded-lg border-2 border-dashed border-gray-200 dark:border-gray-700 flex flex-col items-center justify-center gap-4 p-6"
          >
            <UIcon name="i-lucide-upload" class="size-10 text-muted" />
            <div class="text-center">
              <p class="text-sm font-medium text-gray-700 dark:text-gray-300">
                Drop files here or select to upload
              </p>
              <p class="text-xs text-muted mt-1">Any file type — up to 4 uploads in parallel</p>
            </div>
            <UButton
              label="Select Files"
              icon="i-lucide-file-plus"
              color="neutral"
              variant="outline"
              @click="initialInputRef?.click()"
            />
            <input
              ref="initialInputRef"
              type="file"
              multiple
              class="hidden"
              @change="handleInitialChange"
            />
          </div>

          <!-- file list -->
          <div
            v-else
            class="rounded-lg border border-gray-200/70 dark:border-gray-700/70 overflow-hidden"
          >
            <!-- rows -->
            <div
              class="divide-y divide-gray-100/50 dark:divide-gray-800/50 max-h-[28rem] overflow-y-auto"
            >
              <div v-for="upload in uploads" :key="upload.id" class="px-4 py-3.5">
                <div class="flex items-start gap-3">
                  <!-- file icon -->
                  <UIcon name="i-lucide-file" class="size-5 text-muted mt-1 shrink-0" />

                  <!-- content container -->
                  <div class="flex-1 min-w-0">
                    <!-- top row: name & controls -->
                    <div class="flex items-center justify-between gap-3">
                      <p class="text-sm font-medium truncate text-gray-800 dark:text-gray-200">
                        {{ upload.file.name }}
                      </p>

                      <div class="flex items-center gap-1.5 shrink-0">
                        <!-- lock toggle — disabled for large files -->
                        <UTooltip
                          v-if="!canEncrypt(upload.file)"
                          text="Encryption is only supported for files under 500 MB. Encrypt locally before uploading."
                          :delay-duration="0"
                        >
                          <UButton
                            icon="i-lucide-lock-open"
                            size="xs"
                            variant="ghost"
                            color="neutral"
                            class="opacity-40 cursor-not-allowed"
                            aria-label="Encryption unavailable for large files"
                            disabled
                          />
                        </UTooltip>

                        <!-- lock toggle — enabled -->
                        <UTooltip
                          v-else
                          :text="
                            upload.isEncrypted ? 'Encrypted — click to remove' : 'Encrypt this file'
                          "
                        >
                          <UButton
                            :icon="upload.isEncrypted ? 'i-lucide-lock' : 'i-lucide-lock-open'"
                            size="xs"
                            variant="ghost"
                            :color="getLockColor(upload)"
                            :disabled="isPipeline(upload.stage)"
                            :aria-label="
                              upload.isEncrypted ? 'Remove encryption' : 'Encrypt this file'
                            "
                            @click="upload.isEncrypted = !upload.isEncrypted"
                          />
                        </UTooltip>

                        <!-- stage dots -->
                        <div
                          v-if="upload.stage !== UploadStage.IDLE"
                          class="flex items-center gap-0.5"
                          :title="upload.stage"
                        >
                          <template v-for="step in STAGE_STEPS" :key="step.stage">
                            <template
                              v-if="step.stage !== UploadStage.ENCRYPTING || upload.isEncrypted"
                            >
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
                          </template>
                        </div>

                        <!-- progress percentage -->
                        <span
                          v-if="isPipeline(upload.stage)"
                          class="text-xs tabular-nums text-muted w-9 text-right"
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

                    <!-- size + status badges row -->
                    <div class="flex items-center gap-2 mt-1.5">
                      <span class="text-xs text-muted">{{ formatBytes(upload.file.size) }}</span>

                      <span
                        v-if="upload.stage === UploadStage.IDLE && !upload.isEncrypted"
                        class="text-xs text-muted"
                      >
                        Ready
                      </span>
                      <UBadge
                        v-else-if="upload.stage === UploadStage.IDLE && upload.isEncrypted"
                        label="Encrypted"
                        color="warning"
                        variant="subtle"
                        size="xs"
                      />
                      <span
                        v-else-if="upload.stage === UploadStage.COMPLETE"
                        class="text-xs text-success flex items-center gap-1"
                      >
                        <UIcon
                          v-if="upload.isEncrypted"
                          name="i-lucide-lock"
                          class="size-3 text-warning"
                        />
                        Uploaded
                      </span>
                    </div>

                    <!-- progress bar -->
                    <div v-if="upload.stage !== UploadStage.IDLE" class="mt-3">
                      <UProgress
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

                    <!-- error message -->
                    <p
                      v-if="upload.stage === UploadStage.ERROR && upload.errorMessage"
                      class="text-xs text-error line-clamp-2 mt-2"
                      :title="upload.errorMessage"
                    >
                      {{ upload.errorMessage }}
                    </p>

                    <!-- encryption sub-panel -->
                    <Transition
                      enter-active-class="transition-[grid-template-rows,opacity] duration-200 ease-out"
                      leave-active-class="transition-[grid-template-rows,opacity] duration-150 ease-in"
                      enter-from-class="grid-template-rows-[0fr] opacity-0"
                      enter-to-class="grid-template-rows-[1fr] opacity-100"
                      leave-from-class="grid-template-rows-[1fr] opacity-100"
                      leave-to-class="grid-template-rows-[0fr] opacity-0"
                    >
                      <div
                        v-if="upload.isEncrypted && upload.stage === UploadStage.IDLE"
                        class="grid"
                      >
                        <div class="overflow-hidden">
                          <div
                            class="pt-4 mt-4 border-t border-gray-100 dark:border-gray-800 space-y-3"
                          >
                            <!-- password field -->
                            <UInput
                              v-model="upload.password"
                              :type="upload.passwordVisible ? 'text' : 'password'"
                              placeholder="Password"
                              size="sm"
                              class="w-full"
                              :ui="{ trailing: 'pe-1' }"
                              :color="
                                upload.password &&
                                getPasswordRequirements(upload.password).some((r) => !r.met)
                                  ? 'error'
                                  : 'neutral'
                              "
                            >
                              <template #trailing>
                                <UButton
                                  :icon="
                                    upload.passwordVisible ? 'i-lucide-eye-off' : 'i-lucide-eye'
                                  "
                                  size="xs"
                                  variant="ghost"
                                  color="neutral"
                                  :aria-label="
                                    upload.passwordVisible ? 'Hide password' : 'Show password'
                                  "
                                  @click="upload.passwordVisible = !upload.passwordVisible"
                                />
                              </template>
                            </UInput>

                            <!-- confirm password field -->
                            <UInput
                              v-model="upload.confirmPassword"
                              :type="upload.confirmPasswordVisible ? 'text' : 'password'"
                              placeholder="Confirm Password"
                              size="sm"
                              class="w-full"
                              :ui="{ trailing: 'pe-1' }"
                              :color="
                                upload.confirmPassword && upload.password !== upload.confirmPassword
                                  ? 'error'
                                  : 'neutral'
                              "
                            >
                              <template #trailing>
                                <UButton
                                  :icon="
                                    upload.confirmPasswordVisible
                                      ? 'i-lucide-eye-off'
                                      : 'i-lucide-eye'
                                  "
                                  size="xs"
                                  variant="ghost"
                                  color="neutral"
                                  :aria-label="
                                    upload.confirmPasswordVisible
                                      ? 'Hide confirm password'
                                      : 'Show confirm password'
                                  "
                                  @click="
                                    upload.confirmPasswordVisible = !upload.confirmPasswordVisible
                                  "
                                />
                              </template>
                            </UInput>

                            <!-- strength bar -->
                            <div class="space-y-1">
                              <div class="flex gap-1">
                                <div
                                  v-for="i in 5"
                                  :key="i"
                                  class="h-1.5 flex-1 rounded-full transition-all duration-300"
                                  :class="
                                    upload.password && i <= getPasswordScore(upload.password)
                                      ? PASSWORD_STRENGTH_LEVELS[
                                          getPasswordScore(upload.password) - 1
                                        ].barColor
                                      : 'bg-gray-200 dark:bg-gray-700'
                                  "
                                />
                              </div>
                              <p
                                class="text-xs font-medium transition-colors duration-200"
                                :class="
                                  upload.password
                                    ? PASSWORD_STRENGTH_LEVELS[
                                        getPasswordScore(upload.password) - 1
                                      ]?.textColor
                                    : 'text-muted'
                                "
                              >
                                {{
                                  upload.password
                                    ? PASSWORD_STRENGTH_LEVELS[
                                        getPasswordScore(upload.password) - 1
                                      ]?.label
                                    : "Enter a password"
                                }}
                              </p>
                            </div>

                            <!-- requirements grid -->
                            <div class="grid grid-cols-2 gap-x-6 gap-y-2">
                              <div
                                v-for="req in getPasswordRequirements(upload.password)"
                                :key="req.label"
                                class="flex items-center gap-2 text-xs transition-colors duration-150"
                                :class="
                                  req.met ? 'text-green-600 dark:text-green-400' : 'text-muted'
                                "
                              >
                                <div
                                  class="flex h-4 w-4 shrink-0 items-center justify-center rounded-full transition-colors duration-150"
                                  :class="
                                    req.met
                                      ? 'bg-green-100 dark:bg-green-900/30'
                                      : 'bg-gray-100 dark:bg-white/5'
                                  "
                                >
                                  <UIcon
                                    :name="req.met ? 'i-lucide-check' : 'i-lucide-minus'"
                                    class="size-2.5"
                                  />
                                </div>
                                {{ req.label }}
                              </div>
                            </div>

                            <!-- inline validation error -->
                            <p v-if="getPasswordError(upload)" class="text-xs text-error px-1">
                              {{ getPasswordError(upload) }}
                            </p>

                            <!-- hint field -->
                            <UInput
                              v-model="upload.encryptionHint"
                              placeholder="Hint (optional — stored unencrypted)"
                              size="sm"
                              class="w-full"
                              :ui="{ leading: 'ps-2' }"
                            >
                              <template #leading>
                                <UIcon name="i-lucide-info" class="size-3.5 text-muted" />
                              </template>
                            </UInput>
                          </div>
                        </div>
                      </div>
                    </Transition>
                  </div>
                </div>
              </div>
            </div>

            <!-- add more -->
            <div
              v-if="!isUploading"
              class="px-4 py-3 border-t border-gray-200/70 dark:border-gray-700/70"
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

            <!-- overall progress summary -->
            <Transition
              enter-active-class="transition-all duration-200 ease-out"
              leave-active-class="transition-all duration-150 ease-in"
              enter-from-class="opacity-0"
              leave-to-class="opacity-0"
            >
              <div
                v-if="showProgressSummary"
                class="px-4 py-3 border-t border-gray-200/70 dark:border-gray-700/70 bg-white/40 dark:bg-white/3"
              >
                <div class="flex items-center justify-between text-xs text-muted mb-1.5">
                  <span>{{ completedCount }} of {{ uploads.length }} uploaded</span>
                  <span class="tabular-nums">{{ overallSummaryProgress }}%</span>
                </div>
                <UProgress :value="overallSummaryProgress" :color="summaryBarColor" size="xs" />
              </div>
            </Transition>
          </div>

          <!-- Unified Drag Overlay -->
          <Transition
            enter-active-class="transition-opacity duration-200"
            leave-active-class="transition-opacity duration-150"
            enter-from-class="opacity-0"
            leave-from-class="opacity-100"
            leave-to-class="opacity-0"
          >
            <div
              v-if="isDragging"
              class="absolute inset-0 z-10 flex flex-col items-center justify-center bg-white/90 dark:bg-gray-900/90 backdrop-blur-sm rounded-lg border-2 border-dashed border-primary-500 shadow-sm"
            >
              <UIcon name="i-lucide-download" class="size-8 text-primary mb-2" />
              <p class="text-sm font-medium text-primary">Drop files to add them</p>
            </div>
          </Transition>
        </div>
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
          <UTooltip :text="uploadButtonTooltip">
            <UButton
              v-if="idleCount > 0 && !isUploading"
              label="Upload"
              icon="i-lucide-upload"
              color="primary"
              variant="solid"
              :disabled="!canUpload"
              @click="startUpload"
            />
          </UTooltip>
        </div>
      </div>
    </template>
  </UModal>
</template>

<style>
.grid-template-rows-\[0fr\] {
  grid-template-rows: 0fr;
}
.grid-template-rows-\[1fr\] {
  grid-template-rows: 1fr;
}
</style>
