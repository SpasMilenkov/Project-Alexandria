<script setup lang="ts">
import { useQueryCache } from "@pinia/colada";
import { computed, ref } from "vue";

import type { HomeWidget } from "@/types/home";

import { useAppToast } from "@/composables/useAppToast";
import { useDropZone } from "@/composables/useDropZone";
import { type UploadKind, useUploadLauncher } from "@/composables/useUploadLauncher";
import { DIRECTORY_QUERY_KEYS } from "@/queries/directories";
import { FILES_QUERY_KEYS } from "@/queries/files";
import { STATUS_QUERY_KEYS } from "@/queries/status";
import { useHomeStore } from "@/stores/home";

import HomeQuickUploadDisplay from "./HomeQuickUploadDisplay.vue";

const { widget } = defineProps<{ widget: HomeWidget }>();

const store = useHomeStore();
const cache = useQueryCache();
const toast = useAppToast();
const launcher = useUploadLauncher();
const drop = useDropZone();

const pending = ref(false);
const preparing = ref(false);
const disabled = computed(() => store.isEditing || pending.value);

const runUpload = async (operation: () => Promise<boolean>) => {
  if (disabled.value) return;

  const owner = store.ownerId;
  const epoch = store.ownerEpoch;
  pending.value = true;

  try {
    const completed = await operation();

    if (completed && owner === store.ownerId && epoch === store.ownerEpoch) {
      await Promise.allSettled([
        cache.invalidateQueries({ key: FILES_QUERY_KEYS.root }),
        cache.invalidateQueries({ key: DIRECTORY_QUERY_KEYS.root }),
        cache.invalidateQueries({ key: STATUS_QUERY_KEYS.myStorage() }),
      ]);
    }
  } catch {
    if (owner === store.ownerId && epoch === store.ownerEpoch) {
      toast.error("Could not open upload", "Please try again.");
    }
  } finally {
    pending.value = false;
    preparing.value = false;
  }
};

const upload = (kind: UploadKind) => runUpload(() => launcher.open(kind));

const externalFiles = (event: DragEvent) => event.dataTransfer?.types.includes("Files") ?? false;

const dragEnter = (event: DragEvent) => {
  if (!disabled.value && externalFiles(event)) drop.onDragEnter(event);
};

const dragOver = (event: DragEvent) => {
  if (!externalFiles(event)) return;

  event.preventDefault();

  if (!disabled.value) drop.onDragOver(event);
};

const dragLeave = () => {
  if (drop.isOverDropZone.value) drop.onDragLeave();
};

const dropped = (event: DragEvent) => {
  if (!externalFiles(event)) return;

  event.preventDefault();

  const owner = store.ownerId;
  const epoch = store.ownerEpoch;

  return runUpload(async () => {
    preparing.value = true;
    const contents = await drop.onDrop(event);
    preparing.value = false;

    if (store.isEditing || owner !== store.ownerId || epoch !== store.ownerEpoch) return false;

    return launcher.openDrop(contents);
  });
};
</script>

<template>
  <div
    class="flex min-h-0 min-w-0 flex-1 flex-col"
    :class="{ 'pointer-events-none': store.isEditing }"
    @dragenter="dragEnter"
    @dragover="dragOver"
    @dragleave="dragLeave"
    @drop="dropped"
  >
    <HomeQuickUploadDisplay
      :upload-style="widget.options.quickUploadStyle"
      :over="drop.isOverDropZone.value && !disabled"
      :preparing="preparing"
      :disabled="disabled"
      @upload="upload"
    />
  </div>
</template>
