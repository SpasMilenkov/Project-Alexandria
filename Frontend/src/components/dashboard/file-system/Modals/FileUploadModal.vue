<script setup lang="ts">
import { computed, ref } from "vue";

import type { WorkerOutMessage } from "@/workers/blake3.worker";

import { fileApi } from "@/api/file";
import UploadEmptyState from "@/components/dashboard/file-system/Modals/upload/UploadEmptyState.vue";
import UploadModalFooter from "@/components/dashboard/file-system/Modals/upload/UploadModalFooter.vue";
import UploadModalShell from "@/components/dashboard/file-system/Modals/upload/UploadModalShell.vue";
import UploadPanel from "@/components/dashboard/file-system/Modals/upload/UploadPanel.vue";
import UploadProgressSummary from "@/components/dashboard/file-system/Modals/upload/UploadProgressSummary.vue";
import { useAppToast } from "@/composables/useAppToast";
import { FALLBACK_CONTENT_TYPE } from "@/composables/useDirectoryUpload";
import { encryptFile } from "@/composables/useFileEncryption";
import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { countOf, quoted } from "@/features/toast/format";
import {
  STRENGTH_IDLE_TEXT,
  getPasswordRequirements,
  strengthBarClass,
  strengthLevel,
} from "@/features/upload/password-strength";
import { formatBytes } from "@/utils/size";

const appToast = useAppToast("upload");

// enums & constants

const ENCRYPTION_MAX_FILE_SIZE = 500 * 1024 * 1024; // 500 MB
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
  encryptionIterations: number | null;
}

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
  encryptionIterations: null,
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

const selectedDirectoryId = ref<string | undefined>(props.directoryId ?? undefined);

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

const passwordFieldColor = (state: FileUploadState) => {
  const hasUnmet = getPasswordRequirements(state.password).some((req) => !req.met);

  return state.password && hasUnmet ? ("error" as const) : ("neutral" as const);
};

const confirmFieldColor = (state: FileUploadState) => {
  const mismatch = state.confirmPassword && state.password !== state.confirmPassword;

  return mismatch ? ("error" as const) : ("neutral" as const);
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

const STAGE_LABELS: Record<UploadStage, string> = {
  [UploadStage.IDLE]: "Ready",
  [UploadStage.ENCRYPTING]: "Encrypting…",
  [UploadStage.HASHING]: "Checking the file…",
  [UploadStage.INITIALIZING]: "Preparing…",
  [UploadStage.UPLOADING]: "Uploading…",
  [UploadStage.FINALIZING]: "Verifying…",
  [UploadStage.COMPLETE]: "Uploaded",
  [UploadStage.ERROR]: "Failed",
};

const stageLabel = (state: FileUploadState) => {
  if (state.stage === UploadStage.IDLE && state.isEncrypted) return "Will be encrypted";
  if (state.stage === UploadStage.COMPLETE && state.isEncrypted) return "Uploaded, encrypted";

  return STAGE_LABELS[state.stage];
};

const stageTextClass = (state: FileUploadState) => {
  if (state.stage === UploadStage.COMPLETE) return "text-success";
  if (state.stage === UploadStage.ERROR) return "text-error";

  return "text-gray-600 dark:text-gray-400";
};

const progressColor = (state: FileUploadState) => {
  if (state.stage === UploadStage.ERROR) return "error" as const;
  if (state.stage === UploadStage.COMPLETE) return "success" as const;

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

const runConcurrent = async <T>(
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
  let contentType = state.file.type || FALLBACK_CONTENT_TYPE;

  if (state.isEncrypted) {
    const result = await encryptFile(state.file, state.password);
    fileToUpload = new Blob([result.ciphertext], { type: FALLBACK_CONTENT_TYPE });
    contentType = FALLBACK_CONTENT_TYPE;
    state.encryptionIv = result.iv;
    state.encryptionSalt = result.salt;
    state.encryptionAuthTag = result.authTag;
    state.encryptionIterations = result.iterations;
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
    fileToUpload,
    contentType,
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
    iterationCount: state.isEncrypted ? (state.encryptionIterations ?? undefined) : undefined,
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
    }
  });

  await runConcurrent(tasks, 4);

  reportBatchOutcome(pending);

  if (allComplete.value) {
    setTimeout(() => emit("close", true), 1000);
  }
};

const destinationName = ref(props.directoryName ?? "Home");

const totalSize = computed(() => uploads.value.reduce((sum, state) => sum + state.file.size, 0));

const destinationSuffix = computed(() => ` to ${destinationName.value}`);

const nameUploads = (states: FileUploadState[]) => {
  const [first] = states;
  if (states.length === 1 && first) return quoted(first.file.name);

  return countOf(states.length, "file");
};

const listFailedNames = (states: FileUploadState[]) => {
  const names = states.slice(0, 2).map((state) => state.file.name);
  const remaining = states.length - names.length;

  if (remaining > 0) return `${names.join(", ")} and ${remaining} more`;

  return names.join(", ");
};

const retryFailedAction = {
  icon: "i-lucide-refresh-cw",
  label: "Retry failed",
  onClick: () => {
    void retryFailed();
  },
};

// One summary per batch instead of one toast per failing file; the rows already show each file's state.
const reportBatchOutcome = (batch: FileUploadState[]) => {
  const finished = batch.filter((state) => state.stage === UploadStage.COMPLETE);
  const failed = batch.filter(
    (state) => state.stage === UploadStage.ERROR && state.errorMessage !== "Cancelled",
  );
  const [firstFailure] = failed;

  if (!firstFailure) {
    if (finished.length > 0) {
      appToast.success(`Uploaded ${nameUploads(finished)}${destinationSuffix.value}`);
    }
    return;
  }

  const firstCause = firstFailure.errorMessage ?? "";

  if (finished.length === 0) {
    const cause =
      failed.length === 1 ? firstCause : `${listFailedNames(failed)} failed. ${firstCause}`;

    appToast.error(`Couldn't upload ${nameUploads(failed)}`, cause.trim(), { retry: retryFailed });
    return;
  }

  appToast.warning(
    `Uploaded ${finished.length} of ${batch.length} files${destinationSuffix.value}`,
    {
      actions: [retryFailedAction],
      description: `${listFailedNames(failed)} failed. ${firstCause}`.trim(),
    },
  );
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
      u.encryptionIterations = null;
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
</script>

<template>
  <UploadModalShell
    v-model="selectedDirectoryId"
    v-model:destination-name="destinationName"
    title="Upload files"
    description="Add files, optionally encrypt them, and choose where they go."
    :initial-id="directoryId"
    :initial-name="directoryName"
    :picker-disabled="isUploading"
    @close="emit('close', false)"
  >
    <div
      class="relative"
      @dragenter="onDragEnter"
      @dragleave="onDragLeave"
      @dragover="onDragOver"
      @drop="onDrop"
    >
      <UploadEmptyState
        v-if="uploads.length === 0"
        icon="i-lucide-upload"
        title="Drop files here"
        subtitle="Or choose them from your device. Any file type works."
        button-label="Choose files"
        button-icon="i-lucide-file-plus"
        @select="initialInputRef?.click()"
      >
        <input
          ref="initialInputRef"
          type="file"
          multiple
          class="hidden"
          @change="handleInitialChange"
        />
      </UploadEmptyState>

      <UploadPanel v-else>
        <template #header>
          <p class="min-w-0 flex-1 truncate text-sm text-gray-600 dark:text-gray-400">
            <span class="font-medium text-gray-900 dark:text-gray-100">
              {{ countOf(uploads.length, "file") }}
            </span>
            · {{ formatBytes(totalSize) }}
          </p>

          <UButton
            v-if="!isUploading"
            icon="i-lucide-plus"
            label="Add files"
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
        </template>

        <ul
          class="max-h-[28rem] divide-y divide-gray-200/70 overflow-y-auto dark:divide-gray-700/70"
        >
          <li v-for="upload in uploads" :key="upload.id" class="px-4 py-3">
            <div class="flex items-start gap-3">
              <UIcon
                name="i-lucide-file"
                class="mt-0.5 size-5 shrink-0 text-gray-500 dark:text-gray-500"
              />

              <div class="min-w-0 flex-1">
                <div class="flex items-center justify-between gap-3">
                  <p
                    class="truncate text-sm font-medium text-gray-900 dark:text-gray-100"
                    :title="upload.file.name"
                  >
                    {{ upload.file.name }}
                  </p>

                  <div class="flex shrink-0 items-center gap-1">
                    <span
                      v-if="isPipeline(upload.stage)"
                      class="w-9 text-right text-xs tabular-nums text-gray-600 dark:text-gray-400"
                    >
                      {{ Math.round(upload.overallProgress) }}%
                    </span>

                    <UTooltip
                      v-if="!canEncrypt(upload.file)"
                      text="Encryption works for files under 500 MB. Encrypt larger files before uploading."
                      :delay-duration="0"
                    >
                      <UButton
                        icon="i-lucide-lock-open"
                        size="xs"
                        variant="ghost"
                        color="neutral"
                        square
                        class="justify-center"
                        aria-label="Encryption unavailable for large files"
                        disabled
                      />
                    </UTooltip>

                    <UTooltip
                      v-else-if="upload.stage === UploadStage.IDLE"
                      :text="upload.isEncrypted ? 'Remove encryption' : 'Encrypt this file'"
                    >
                      <UButton
                        :icon="upload.isEncrypted ? 'i-lucide-lock' : 'i-lucide-lock-open'"
                        size="xs"
                        variant="ghost"
                        :color="getLockColor(upload)"
                        square
                        class="justify-center"
                        :aria-pressed="upload.isEncrypted"
                        :aria-label="upload.isEncrypted ? 'Remove encryption' : 'Encrypt this file'"
                        @click="upload.isEncrypted = !upload.isEncrypted"
                      />
                    </UTooltip>

                    <UButton
                      icon="i-lucide-x"
                      size="xs"
                      variant="ghost"
                      color="neutral"
                      square
                      class="justify-center"
                      :aria-label="isPipeline(upload.stage) ? 'Cancel upload' : 'Remove file'"
                      @click="handleRemoveOrCancel(upload)"
                    />
                  </div>
                </div>

                <p class="mt-0.5 flex items-center gap-1.5 text-xs">
                  <span class="text-gray-600 dark:text-gray-400">
                    {{ formatBytes(upload.file.size) }}
                  </span>
                  <span aria-hidden="true" class="text-gray-500 dark:text-gray-500">·</span>
                  <UIcon
                    v-if="upload.isEncrypted"
                    name="i-lucide-lock"
                    class="size-3 shrink-0 text-gray-500 dark:text-gray-500"
                  />
                  <span :class="stageTextClass(upload)">{{ stageLabel(upload) }}</span>
                </p>

                <UProgress
                  v-if="upload.stage !== UploadStage.IDLE"
                  :model-value="upload.overallProgress"
                  :color="progressColor(upload)"
                  size="xs"
                  class="mt-2.5"
                />

                <p
                  v-if="upload.stage === UploadStage.ERROR && upload.errorMessage"
                  class="mt-2 line-clamp-2 text-xs text-error"
                  :title="upload.errorMessage"
                >
                  {{ upload.errorMessage }}
                </p>

                <Transition
                  enter-active-class="transition-[grid-template-rows,opacity] duration-200 ease-out"
                  leave-active-class="transition-[grid-template-rows,opacity] duration-150 ease-in"
                  enter-from-class="grid-template-rows-[0fr] opacity-0"
                  enter-to-class="grid-template-rows-[1fr] opacity-100"
                  leave-from-class="grid-template-rows-[1fr] opacity-100"
                  leave-to-class="grid-template-rows-[0fr] opacity-0"
                >
                  <div v-if="upload.isEncrypted && upload.stage === UploadStage.IDLE" class="grid">
                    <div class="overflow-hidden">
                      <div
                        class="mt-3 space-y-3 border-t border-gray-200/70 pt-3 dark:border-gray-700/70"
                      >
                        <UInput
                          v-model="upload.password"
                          :type="upload.passwordVisible ? 'text' : 'password'"
                          placeholder="Password"
                          size="sm"
                          class="w-full"
                          :ui="{ trailing: 'pe-1' }"
                          :color="passwordFieldColor(upload)"
                        >
                          <template #trailing>
                            <UButton
                              :icon="upload.passwordVisible ? 'i-lucide-eye-off' : 'i-lucide-eye'"
                              size="xs"
                              variant="ghost"
                              color="neutral"
                              square
                              class="justify-center"
                              :aria-label="
                                upload.passwordVisible ? 'Hide password' : 'Show password'
                              "
                              @click="upload.passwordVisible = !upload.passwordVisible"
                            />
                          </template>
                        </UInput>

                        <UInput
                          v-model="upload.confirmPassword"
                          :type="upload.confirmPasswordVisible ? 'text' : 'password'"
                          placeholder="Confirm password"
                          size="sm"
                          class="w-full"
                          :ui="{ trailing: 'pe-1' }"
                          :color="confirmFieldColor(upload)"
                        >
                          <template #trailing>
                            <UButton
                              :icon="
                                upload.confirmPasswordVisible ? 'i-lucide-eye-off' : 'i-lucide-eye'
                              "
                              size="xs"
                              variant="ghost"
                              color="neutral"
                              square
                              class="justify-center"
                              :aria-label="
                                upload.confirmPasswordVisible
                                  ? 'Hide confirmation'
                                  : 'Show confirmation'
                              "
                              @click="
                                upload.confirmPasswordVisible = !upload.confirmPasswordVisible
                              "
                            />
                          </template>
                        </UInput>

                        <div class="space-y-1">
                          <div class="flex gap-1">
                            <div
                              v-for="i in 5"
                              :key="i"
                              class="h-1.5 flex-1 rounded-full transition-colors duration-300"
                              :class="strengthBarClass(upload.password, i)"
                            />
                          </div>
                          <p
                            class="text-xs font-medium transition-colors duration-200"
                            :class="strengthLevel(upload.password)?.textColor ?? STRENGTH_IDLE_TEXT"
                          >
                            {{ strengthLevel(upload.password)?.label ?? "Enter a password" }}
                          </p>
                        </div>

                        <ul class="grid grid-cols-2 gap-x-6 gap-y-2">
                          <li
                            v-for="req in getPasswordRequirements(upload.password)"
                            :key="req.label"
                            class="flex items-center gap-2 text-xs transition-colors duration-150"
                            :class="
                              req.met
                                ? 'text-green-600 dark:text-green-400'
                                : 'text-gray-600 dark:text-gray-400'
                            "
                          >
                            <span
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
                            </span>
                            {{ req.label }}
                          </li>
                        </ul>

                        <p v-if="getPasswordError(upload)" class="px-1 text-xs text-error">
                          {{ getPasswordError(upload) }}
                        </p>

                        <UInput
                          v-model="upload.encryptionHint"
                          placeholder="Hint (optional, stored unencrypted)"
                          size="sm"
                          class="w-full"
                          :ui="{ leading: 'ps-2' }"
                        >
                          <template #leading>
                            <UIcon
                              name="i-lucide-info"
                              class="size-3.5 text-gray-500 dark:text-gray-500"
                            />
                          </template>
                        </UInput>
                      </div>
                    </div>
                  </div>
                </Transition>
              </div>
            </div>
          </li>
        </ul>

        <template #footer>
          <Transition
            enter-active-class="transition-all duration-200 ease-out"
            leave-active-class="transition-all duration-150 ease-in"
            enter-from-class="opacity-0"
            leave-to-class="opacity-0"
          >
            <UploadProgressSummary
              v-if="showProgressSummary"
              :percent="overallSummaryProgress"
              :color="summaryBarColor"
            >
              <template #label>{{ completedCount }} of {{ uploads.length }} uploaded</template>
            </UploadProgressSummary>
          </Transition>
        </template>
      </UploadPanel>

      <Transition
        enter-active-class="transition-opacity duration-200"
        leave-active-class="transition-opacity duration-150"
        enter-from-class="opacity-0"
        leave-to-class="opacity-0"
      >
        <div
          v-if="isDragging"
          class="absolute inset-0 z-10 flex flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed border-primary bg-default"
        >
          <UIcon name="i-lucide-download" class="size-8 text-primary" />
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100">Drop to add files</p>
        </div>
      </Transition>
    </div>

    <template #footer>
      <UploadModalFooter
        :working="false"
        :has-session="false"
        :has-failures="hasFailures && !isUploading"
        :destination-name="destinationName"
        :cancel-disabled="isUploading"
        @close="emit('close', false)"
        @retry="retryFailed"
      >
        <template #primary>
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
        </template>
      </UploadModalFooter>
    </template>
  </UploadModalShell>
</template>

<style>
.grid-template-rows-\[0fr\] {
  grid-template-rows: 0fr;
}
.grid-template-rows-\[1fr\] {
  grid-template-rows: 1fr;
}
</style>
