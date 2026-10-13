import { defineAsyncComponent } from "vue";

import type { DropContents } from "@/composables/useDropZone";
import type { DroppedFile } from "@/types/files/dropped-file";

import { useLazyModal } from "@/composables/useLazyOverlay";

const FileUploadModal = defineAsyncComponent(
  () => import("@/components/dashboard/file-system/Modals/FileUploadModal.vue"),
);
const DirectoryUploadModal = defineAsyncComponent(
  () => import("@/components/dashboard/file-system/Modals/DirectoryUploadModal.vue"),
);
const ArchiveUploadModal = defineAsyncComponent(
  () => import("@/components/dashboard/file-system/Modals/ArchiveUploadModal.vue"),
);
const ZipUploadChoiceModal = defineAsyncComponent(
  () => import("@/components/dashboard/file-system/Modals/ZipUploadChoiceModal.vue"),
);

export type UploadKind = "File" | "Directory" | "Archive";

interface UploadDestination {
  directoryId?: string;
  directoryName?: string;
}

export const useUploadLauncher = () => {
  const files = useLazyModal<UploadDestination & { droppedFiles?: File[] }, boolean>(
    FileUploadModal,
    { persistOnDispose: true },
  );
  const directories = useLazyModal<UploadDestination & { droppedFiles?: DroppedFile[] }, boolean>(
    DirectoryUploadModal,
    { persistOnDispose: true },
  );
  const archives = useLazyModal<UploadDestination & { droppedFiles?: File[] }, boolean>(
    ArchiveUploadModal,
    { persistOnDispose: true },
  );
  const zipChoice = useLazyModal<Record<string, never>, "archive" | "file" | null>(
    ZipUploadChoiceModal,
  );

  const open = async (kind: UploadKind, destination: UploadDestination = {}) => {
    if (files.isScopeDisposed()) return false;

    if (kind === "Directory") return (await directories.open(destination).result) ?? false;
    if (kind === "Archive") return (await archives.open(destination).result) ?? false;

    return (await files.open(destination).result) ?? false;
  };

  const openDrop = async (drop: DropContents, destination: UploadDestination = {}) => {
    if (files.isScopeDisposed() || drop.dropType === "none") return false;

    if (drop.dropType === "dir") {
      return (
        (await directories.open({ ...destination, droppedFiles: drop.entries }).result) ?? false
      );
    }

    if (drop.dropType === "zip") {
      const choice = await zipChoice.open().result;

      if (!choice || zipChoice.isScopeDisposed()) return false;

      if (choice === "archive") {
        return (
          (await archives.open({ ...destination, droppedFiles: drop.entries }).result) ?? false
        );
      }
    }

    return (await files.open({ ...destination, droppedFiles: drop.entries }).result) ?? false;
  };

  return { open, openDrop };
};
