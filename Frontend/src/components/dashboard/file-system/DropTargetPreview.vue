<template>
  <div
    class="flex flex-col overflow-hidden"
    :class="surfaceClasses[variant]"
    :data-drop-dir-id="toDropId(folder.id)"
    :data-drop-dir-name="folder.name"
    data-drop-kind="panel"
    data-drop-panel
  >
    <div
      class="flex min-w-0 shrink-0 items-center gap-1 border-b border-gray-200/70 px-3 py-2 dark:border-gray-700/70"
    >
      <UIcon name="i-lucide-folder" class="h-4 w-4 shrink-0 text-gray-500 dark:text-gray-400" />

      <span v-if="hiddenTrailCount > 0" class="shrink-0 text-xs text-gray-500 dark:text-gray-500">
        …
      </span>

      <template v-for="(segment, index) in visibleTrail" :key="toDropId(segment.id)">
        <UIcon
          v-if="index > 0 || hiddenTrailCount > 0"
          name="i-lucide-chevron-right"
          class="h-3.5 w-3.5 shrink-0 text-gray-400 dark:text-gray-500"
        />
        <span
          class="min-w-0 truncate rounded px-1 text-sm data-[drop-active=valid]:bg-primary/10 data-[drop-active=valid]:ring-1 data-[drop-active=valid]:ring-primary"
          :class="
            segment === folder
              ? 'font-semibold text-gray-900 dark:text-gray-100'
              : 'text-gray-600 dark:text-gray-400'
          "
          :data-drop-dir-id="toDropId(segment.id)"
          :data-drop-dir-name="segment.name"
          data-drop-kind="preview-trail"
        >
          {{ segment.name }}
        </span>
      </template>

      <span
        v-if="totalCount !== null"
        class="ml-auto shrink-0 pl-2 text-xs tabular-nums text-gray-500 dark:text-gray-500"
      >
        {{ formatCount(totalCount, "item") }}
      </span>
    </div>

    <div class="relative min-h-0 flex-1 overflow-y-auto p-1" data-drop-scroll>
      <div v-if="isLoading" class="flex h-full items-center justify-center">
        <BlocksSpinner :size="18" aria-label="Loading folder" />
      </div>

      <div
        v-else-if="hasError"
        class="flex h-full flex-col items-center justify-center gap-3 px-6 text-center"
      >
        <span
          class="flex size-12 items-center justify-center rounded-2xl bg-default text-gray-900 shadow-sm ring ring-default dark:text-gray-100"
        >
          <UIcon name="i-lucide-folder-x" class="size-6" aria-hidden="true" />
        </span>
        <div class="flex flex-col gap-1">
          <p class="text-sm font-semibold text-gray-900 dark:text-gray-100">
            Couldn't load this folder
          </p>
          <p class="text-sm text-gray-600 dark:text-gray-400">Try again in a moment.</p>
        </div>
      </div>

      <div
        v-else-if="isEmpty"
        class="flex h-full flex-col items-center justify-center gap-3 px-6 text-center"
      >
        <span
          class="flex size-12 items-center justify-center rounded-2xl bg-default text-gray-900 shadow-sm ring ring-default dark:text-gray-100"
        >
          <UIcon name="i-lucide-folder-open" class="size-6" aria-hidden="true" />
        </span>
        <div class="flex flex-col gap-1">
          <p class="text-sm font-semibold text-gray-900 dark:text-gray-100">Empty folder</p>
          <p class="text-sm text-gray-600 dark:text-gray-400">Nothing has been added here yet.</p>
        </div>
      </div>

      <template v-else>
        <div
          v-for="directory in directories"
          :key="directory.id"
          class="flex h-8 min-w-0 items-center gap-2 rounded-md px-2 text-sm text-gray-900 data-[drop-active=valid]:bg-primary/10 data-[drop-active=valid]:ring-1 data-[drop-active=valid]:ring-inset data-[drop-active=valid]:ring-primary dark:text-gray-100"
          :class="{ 'opacity-40': excludedIds.includes(directory.id) }"
          :data-drop-dir-id="directory.id"
          :data-drop-dir-name="directory.name"
          data-drop-kind="preview-row"
        >
          <UIcon name="i-lucide-folder" class="h-4 w-4 shrink-0 text-gray-500 dark:text-gray-400" />
          <span class="min-w-0 flex-1 truncate">{{ directory.name }}</span>
          <UIcon
            name="i-lucide-chevron-right"
            class="h-3.5 w-3.5 shrink-0 text-gray-400 dark:text-gray-500"
          />
        </div>

        <div
          v-for="file in files"
          :key="file.fileId"
          class="flex h-8 min-w-0 items-center gap-2 px-2 text-sm text-gray-600 dark:text-gray-400"
        >
          <UIcon
            :name="getFileIcon(file.fileName)"
            class="h-4 w-4 shrink-0 text-gray-500 dark:text-gray-500"
          />
          <span class="min-w-0 truncate">{{ file.fileName }}</span>
        </div>

        <p
          v-if="hiddenCount > 0"
          class="px-2 py-1.5 text-xs tabular-nums text-gray-500 dark:text-gray-500"
        >
          +{{ hiddenCount }} more
        </p>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import BlocksSpinner from "@/components/common/BlockSpinner.vue";
import { toDropId } from "@/composables/useExplorerDragSession";
import { useLoadHold } from "@/composables/useLoadHold";
import { previewDirectoriesQuery, previewFilesQuery } from "@/features/explorer/drag/drop-preview";
import { formatCount } from "@/utils/count";
import { getFileIcon } from "@/utils/files/icons";

export interface PreviewTrailSegment {
  id: string | null;
  name: string;
}

const MAX_VISIBLE_TRAIL = 3;

const {
  trail,
  excludedIds,
  variant = "floating",
} = defineProps<{
  trail: PreviewTrailSegment[];
  excludedIds: string[];
  // Floating is the card that follows a drag; inline fills a pane inside a
  // solid overlay, where glass and a shadow would only add noise.
  variant?: "floating" | "inline";
}>();

const surfaceClasses: Record<"floating" | "inline", string> = {
  floating:
    "h-80 w-72 rounded-xl border border-gray-200/70 shadow-lg frosted-glass glass-surface-strong transition-shadow data-[drop-active=valid]:ring-2 data-[drop-active=valid]:ring-primary dark:border-gray-700/70",
  inline: "h-full w-full",
};

const folder = computed(() => trail[trail.length - 1]);

const visibleTrail = computed(() => trail.slice(-MAX_VISIBLE_TRAIL));
const hiddenTrailCount = computed(() => Math.max(0, trail.length - MAX_VISIBLE_TRAIL));

const directoriesQuery = useQuery(() => previewDirectoriesQuery(folder.value.id));
const filesQuery = useQuery(() => previewFilesQuery(folder.value.id));

const directories = computed(() => directoriesQuery.data.value?.items ?? []);
const files = computed(() => filesQuery.data.value?.items ?? []);

// Inline previews keep the previous listing up briefly while the next one loads.
// A drag never does: dropping onto a listing that belongs to another folder
// would be misleading.
const { isHolding } = useLoadHold(
  () => folder.value.id ?? "root",
  () => variant === "inline",
);

const isLoading = computed(
  () =>
    !isHolding.value &&
    !directoriesQuery.error.value &&
    !filesQuery.error.value &&
    (directoriesQuery.data.value === undefined ||
      filesQuery.data.value === undefined ||
      directoriesQuery.isPlaceholderData.value ||
      filesQuery.isPlaceholderData.value),
);

const hasError = computed(
  () => Boolean(directoriesQuery.error.value) || Boolean(filesQuery.error.value),
);

const totalCount = computed(() => {
  if (isLoading.value) return null;

  const directoryData = directoriesQuery.data.value;
  const fileData = filesQuery.data.value;

  if (!directoryData || !fileData) return null;

  return directoryData.totalCount + fileData.totalCount;
});

const isEmpty = computed(() => totalCount.value === 0);

const hiddenCount = computed(() => {
  if (totalCount.value === null) return 0;

  return Math.max(0, totalCount.value - directories.value.length - files.value.length);
});
</script>
