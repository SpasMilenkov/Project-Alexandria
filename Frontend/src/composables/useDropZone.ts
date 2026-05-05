import { ref } from "vue";

import type { DroppedFile } from "@/types/files/dropped-file";

export type DropContents =
  | { dropType: "file"; entries: File[] }
  | { dropType: "zip"; entries: File[] }
  | { dropType: "dir"; entries: DroppedFile[] }
  | { dropType: "none" };

export const useDropZone = () => {
  const containerRef = ref<HTMLElement | null>(null);
  const dragHasDirectory = ref(false);

  const isOverDropZone = ref(false);
  const dragCounter = ref(0);

  const readEntryRecursive = async (entry: FileSystemEntry, path = ""): Promise<DroppedFile[]> => {
    if (entry.isFile) {
      return new Promise((resolve, reject) => {
        (entry as FileSystemFileEntry).file(
          (file) => resolve([{ file, relativePath: path + file.name }]),
          reject,
        );
      });
    }
    if (entry.isDirectory) {
      const dirEntry = entry as FileSystemDirectoryEntry;
      const children = await readAllEntries(dirEntry.createReader());
      const nested = await Promise.all(
        children.map((child) => readEntryRecursive(child, `${path}${entry.name}/`)),
      );
      return nested.flat();
    }
    return [];
  };

  const readAllEntries = (reader: FileSystemDirectoryReader): Promise<FileSystemEntry[]> =>
    new Promise((resolve, reject) => {
      const collected: FileSystemEntry[] = [];
      const readBatch = () => {
        reader.readEntries((batch) => {
          if (batch.length === 0) resolve(collected);
          else {
            collected.push(...batch);
            readBatch();
          }
        }, reject);
      };
      readBatch();
    });

  const onDragEnter = (event: DragEvent) => {
    event.preventDefault();
    dragCounter.value++;
    isOverDropZone.value = true;

    const items = Array.from(event.dataTransfer?.items ?? []);
    dragHasDirectory.value = items.some((item) => {
      const entry = item.webkitGetAsEntry?.();
      return entry?.isDirectory ?? false;
    });
  };

  const onDragLeave = () => {
    dragCounter.value--;
    if (dragCounter.value <= 0) {
      dragCounter.value = 0;
      isOverDropZone.value = false;
      dragHasDirectory.value = false;
    }
  };

  const onDragOver = (event: DragEvent) => {
    event.preventDefault();
  };

  const onDrop = async (event: DragEvent): Promise<DropContents> => {
    event.preventDefault();
    isOverDropZone.value = false;
    dragCounter.value = 0;
    dragHasDirectory.value = false;

    const items = Array.from(event.dataTransfer?.items ?? []);
    if (items.length === 0) return { dropType: "none" };

    const entries = items
      .map((item) => item.webkitGetAsEntry?.())
      .filter((e): e is FileSystemEntry => e !== null && e !== undefined);

    if (entries.length === 0) return { dropType: "none" };

    const droppedFiles = Array.from(event.dataTransfer?.files ?? []);

    if (isZipDrop(droppedFiles, entries)) {
      return { dropType: "zip", entries: droppedFiles };
    }

    const hasDirectory = entries.some((e) => e.isDirectory);

    if (hasDirectory) {
      return {
        dropType: "dir",
        entries: (await Promise.all(entries.map((e) => readEntryRecursive(e)))).flat(),
      };
    }
    return { dropType: "file", entries: droppedFiles };
  };

  const isZipDrop = (files: File[] | null, entries: FileSystemEntry[]): boolean => {
    if (entries.length !== 1 || !entries[0].isFile) return false;
    const name = files?.[0]?.name ?? entries[0].name;
    return name.toLowerCase().endsWith(".zip");
  };

  return {
    containerRef,
    dragHasDirectory,
    isOverDropZone,
    onDragEnter,
    onDragLeave,
    onDragOver,
    onDrop,
  };
};
