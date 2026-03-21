import type { TreeItem } from "@nuxt/ui";

import { computed, ref } from "vue";

import type { WorkerOutMessage } from "@/workers/blake3.worker";

import { fileApi } from "@/api/file";
import { formatBytes } from "@/utils/size.utils";

// types

export type UploadStatus = "pending" | "hashing" | "uploading" | "success" | "error";
export type FolderStatus = "pending" | "uploading" | "complete" | "error";

export interface FileUploadStatus {
  file: File;
  relativePath: string;
  status: UploadStatus;
  progress: number;
  error?: string;
  directoryId?: string;
  abortController: AbortController;
}

export interface FileEntry {
  file: File;
  relativePath: string;
}

export interface DirectoryTreeItem extends TreeItem {
  fullPath: string;
  isFolder: boolean;
  relativePath?: string;
  fileSize?: number;
  children?: DirectoryTreeItem[];
}

// constants

export const CONCURRENCY = 4;
export const VIRTUALIZE_THRESHOLD = 150;

// composable

export const useDirectoryUpload = () => {
  const fileStatuses = ref<FileUploadStatus[]>([]);
  const uploading = ref(false);
  const cancelled = ref(false);

  // computed — stats

  const totalFiles = computed(() => fileStatuses.value.length);

  const completedFiles = computed(
    () => fileStatuses.value.filter((f) => f.status === "success" || f.status === "error").length,
  );

  const successfulFiles = computed(
    () => fileStatuses.value.filter((f) => f.status === "success").length,
  );

  const failedFiles = computed(() => fileStatuses.value.filter((f) => f.status === "error"));

  const activeFiles = computed(
    () =>
      fileStatuses.value.filter((f) => f.status === "hashing" || f.status === "uploading").length,
  );

  const overallProgress = computed(() => {
    if (totalFiles.value === 0) return 0;
    const sum = fileStatuses.value.reduce((acc, f) => {
      if (f.status === "success") return acc + 100;
      if (f.status === "hashing" || f.status === "uploading") return acc + f.progress;
      return acc;
    }, 0);
    return Math.round(sum / totalFiles.value);
  });

  const summaryBarColor = computed(() => {
    if (uploading.value) return "primary" as const;
    if (
      failedFiles.value.length > 0 &&
      successfulFiles.value === totalFiles.value - failedFiles.value.length
    )
      return "warning" as const;
    if (failedFiles.value.length > 0) return "error" as const;
    return "success" as const;
  });

  const shouldVirtualize = computed(() => fileStatuses.value.length > VIRTUALIZE_THRESHOLD);

  // O(1) status lookup

  const statusMap = computed(() => new Map(fileStatuses.value.map((s) => [s.relativePath, s])));

  // folder status aggregation

  const folderStatusMap = computed((): Map<string, FolderStatus> => {
    const map = new Map<string, FolderStatus>();
    if (fileStatuses.value.length === 0) return map;

    const folderPaths = new Set<string>();
    for (const { relativePath } of fileStatuses.value) {
      const parts = relativePath.split("/");
      for (let i = 1; i < parts.length; i++) {
        folderPaths.add(parts.slice(0, i).join("/"));
      }
    }

    for (const folderPath of folderPaths) {
      const children = fileStatuses.value.filter((s) =>
        s.relativePath.startsWith(folderPath + "/"),
      );
      const hasActive = children.some((c) => c.status === "hashing" || c.status === "uploading");
      const hasError = children.some((c) => c.status === "error");
      const allDone = children.every((c) => c.status === "success");

      let status: FolderStatus = "pending";
      if (hasActive) status = "uploading";
      else if (allDone) status = "complete";
      else if (hasError) status = "error";

      map.set(folderPath, status);
    }

    return map;
  });

  const folderChildCountMap = computed((): Map<string, { total: number; done: number }> => {
    const map = new Map<string, { total: number; done: number }>();
    if (fileStatuses.value.length === 0) return map;

    const folderPaths = new Set<string>();
    for (const { relativePath } of fileStatuses.value) {
      const parts = relativePath.split("/");
      for (let i = 1; i < parts.length; i++) {
        folderPaths.add(parts.slice(0, i).join("/"));
      }
    }

    for (const folderPath of folderPaths) {
      const children = fileStatuses.value.filter((s) =>
        s.relativePath.startsWith(folderPath + "/"),
      );
      map.set(folderPath, {
        done: children.filter((c) => c.status === "success").length,
        total: children.length,
      });
    }

    return map;
  });

  // tree building

  const buildFileTree = (entries: FileEntry[]): DirectoryTreeItem[] => {
    const root = [] as DirectoryTreeItem[];
    const folderMap = new Map<string, DirectoryTreeItem>();

    const getOrCreateFolder = (
      parts: string[],
      depth: number,
      parent: DirectoryTreeItem[],
    ): DirectoryTreeItem[] => {
      if (depth >= parts.length - 1) return parent;
      const segmentPath = parts.slice(0, depth + 1).join("/");

      if (!folderMap.has(segmentPath)) {
        const node: DirectoryTreeItem = {
          label: parts[depth],
          fullPath: segmentPath,
          isFolder: true,
          defaultExpanded: depth < 2,
          children: [],
        };
        parent.push(node);
        folderMap.set(segmentPath, node);
      }

      return getOrCreateFolder(parts, depth + 1, folderMap.get(segmentPath)!.children!);
    };

    for (const entry of entries) {
      const parts = entry.relativePath.split("/");
      if (parts.length === 1) {
        root.push({
          label: entry.file.name,
          fullPath: entry.relativePath,
          isFolder: false,
          relativePath: entry.relativePath,
          fileSize: entry.file.size,
        });
      } else {
        const parentList = getOrCreateFolder(parts, 0, root);
        parentList.push({
          label: parts[parts.length - 1],
          fullPath: entry.relativePath,
          isFolder: false,
          relativePath: entry.relativePath,
          fileSize: entry.file.size,
        });
      }
    }

    return root;
  };

  // treeItems — uses fileStatuses when upload has started, raw entries before

  const buildTreeFromStatuses = (): DirectoryTreeItem[] =>
    buildFileTree(fileStatuses.value.map((s) => ({ file: s.file, relativePath: s.relativePath })));

  // worker-based hashing

  const hashWithWorker = (
    file: File,
    id: string,
    onProgress: (pct: number) => void,
    signal: AbortSignal,
  ): Promise<string> =>
    new Promise((resolve, reject) => {
      // signal may already be aborted before we even get here (queued pending files)
      if (signal.aborted) {
        reject(new DOMException("Cancelled", "AbortError"));
        return;
      }

      const worker = new Worker(new URL("@/workers/blake3.worker.ts", import.meta.url), {
        type: "module",
      });

      const onAbort = () => {
        worker.terminate();
        reject(new DOMException("Cancelled", "AbortError"));
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
  // shouldStop is checked before pulling each new task — slots drain immediately on cancel

  const runConcurrent = async <T>(
    tasks: Array<() => Promise<T>>,
    limit: number,
    shouldStop: () => boolean = () => false,
  ): Promise<PromiseSettledResult<T>[]> => {
    const results: PromiseSettledResult<T>[] = new Array(tasks.length);
    let cursor = 0;

    const runSlot = async () => {
      while (cursor < tasks.length && !shouldStop()) {
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

  // per-file upload pipeline

  const uploadSingleFile = async (fileStatus: FileUploadStatus): Promise<boolean> => {
    const { signal } = fileStatus.abortController;

    // guard for files that were pending when cancelAll() fired
    if (signal.aborted) {
      fileStatus.status = "error";
      fileStatus.error = "Cancelled";
      return false;
    }

    try {
      const { file } = fileStatus;

      fileStatus.status = "hashing";
      fileStatus.progress = 0;

      const hash = await hashWithWorker(
        file,
        fileStatus.relativePath,
        (pct) => {
          fileStatus.progress = Math.round(pct * 0.25);
        },
        signal,
      );

      fileStatus.progress = 25;
      const { uploadId, uploadUrl } = await fileApi.initializeUpload({
        contentLength: file.size,
        contentType: file.type || "application/octet-stream",
        directoryId: fileStatus.directoryId,
        hash,
      });
      fileStatus.progress = 30;

      fileStatus.status = "uploading";
      await fileApi.uploadToS3(
        uploadUrl,
        file,
        (pct) => {
          fileStatus.progress = 30 + Math.round(pct * 0.65);
        },
        signal,
      );

      fileStatus.progress = 95;
      await fileApi.finalizeUpload({
        directoryId: fileStatus.directoryId || undefined,
        fileName: file.name,
        uploadId,
      });

      fileStatus.status = "success";
      fileStatus.progress = 100;
      return true;
    } catch (err: any) {
      const isCancelled = err instanceof DOMException && err.name === "AbortError";
      fileStatus.status = "error";
      fileStatus.error = isCancelled
        ? "Cancelled"
        : (err.response?.data?.message ??
          err.response?.data?.error ??
          err.message ??
          "Unknown error");
      return false;
    }
  };

  // main pipeline entry point — called by both modals

  const startUploadPipeline = async (
    entries: FileEntry[],
    directoryMapping: Record<string, string>,
  ) => {
    fileStatuses.value = entries.map(({ file, relativePath }) => ({
      abortController: new AbortController(),
      directoryId: directoryMapping[relativePath] || undefined,
      error: undefined,
      file,
      progress: 0,
      relativePath,
      status: "pending" as UploadStatus,
    }));

    const tasks = fileStatuses.value.map((fs) => () => uploadSingleFile(fs));
    await runConcurrent(tasks, CONCURRENCY, () => cancelled.value);
  };

  const cancelAll = () => {
    cancelled.value = true;
    // abort every controller — pending files will hit the signal.aborted guard,
    // in-flight files will be interrupted mid-upload
    fileStatuses.value.forEach((fs) => {
      if (fs.status !== "success") fs.abortController.abort();
    });
  };

  const retryFailedUploads = async () => {
    const failed = fileStatuses.value.filter((f) => f.status === "error");
    if (failed.length === 0) return;

    // clear cancelled flag so the shouldStop guard doesn't immediately skip everything
    cancelled.value = false;

    failed.forEach((f) => {
      f.status = "pending";
      f.progress = 0;
      f.error = undefined;
      f.abortController = new AbortController();
    });

    const tasks = failed.map((fs) => () => uploadSingleFile(fs));
    await runConcurrent(tasks, CONCURRENCY, () => cancelled.value);
  };

  const reset = () => {
    fileStatuses.value = [];
    uploading.value = false;
    cancelled.value = false;
  };

  // slot helpers — shared between both modals

  const statusIcon = (status: UploadStatus) => {
    switch (status) {
      case "pending":
        return "i-lucide-circle";
      case "hashing":
        return "i-lucide-loader-circle";
      case "uploading":
        return "i-lucide-loader-circle";
      case "success":
        return "i-lucide-check-circle";
      case "error":
        return "i-lucide-x-circle";
    }
  };

  const statusIconClass = (status: UploadStatus) => {
    switch (status) {
      case "pending":
        return "text-muted";
      case "hashing":
        return "text-primary animate-spin";
      case "uploading":
        return "text-primary animate-spin";
      case "success":
        return "text-success";
      case "error":
        return "text-error";
    }
  };

  const folderIconClass = (folderPath: string) => {
    const status = folderStatusMap.value.get(folderPath);
    switch (status) {
      case "uploading":
        return "text-primary";
      case "complete":
        return "text-success";
      case "error":
        return "text-error";
      default:
        return "text-muted";
    }
  };

  return {
    // state
    fileStatuses,
    uploading,
    cancelled,
    // computed
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
    // methods
    buildFileTree,
    buildTreeFromStatuses,
    runConcurrent,
    uploadSingleFile,
    startUploadPipeline,
    cancelAll,
    retryFailedUploads,
    reset,
    // slot helpers
    statusIcon,
    statusIconClass,
    folderIconClass,
    // formatBytes re-exported for convenience in templates
    formatBytes,
  };
};
