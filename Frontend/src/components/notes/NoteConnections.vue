<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import FileTagList from "@/components/dashboard/tags/FileTagList.vue";
import FileTagPicker from "@/components/dashboard/tags/FileTagPicker.vue";
import { readingMinutes } from "@/features/notes/stats";
import { directoryPath } from "@/queries/directories";
import { formatCount } from "@/utils/count";
import { formatCalendarDate, formatTimeAgo } from "@/utils/date-formatters";
import { formatBytes } from "@/utils/size";

const { fileId, directoryId, versionNumber, sizeBytes, editedAt, wordCount } = defineProps<{
  fileId: string;
  directoryId: string | null;
  versionNumber: number;
  sizeBytes: number;
  editedAt: string;
  wordCount: number;
}>();

const emit = defineEmits<{ collapse: [] }>();

// Deep folders show their last two levels, which is usually enough to tell them apart.
const VISIBLE_FOLDER_LEVELS = 2;

const { data: path } = useQuery(() => directoryPath(directoryId ?? ""));

const folderLabel = computed(() => {
  if (!directoryId) return "Home";

  const names = path.value?.pathParts.map((part) => part.name) ?? [];

  if (names.length === 0) return "Folder";
  if (names.length <= VISIBLE_FOLDER_LEVELS) return names.join(" / ");

  return `… / ${names.slice(-VISIBLE_FOLDER_LEVELS).join(" / ")}`;
});

const folderTitle = computed(() => path.value?.pathParts.map((part) => part.name).join(" / "));

const folderRoute = computed(() => {
  if (directoryId) return { name: "dashboard", params: { dirId: directoryId } };

  return { name: "dashboard" };
});

const lengthLabel = computed(() => {
  if (wordCount === 0) return "No words yet";

  return `${formatCount(wordCount, "word")} · ${readingMinutes(wordCount)} min read`;
});
</script>

<template>
  <section
    aria-label="Connections"
    class="group frosted-glass glass-surface flex flex-col gap-4 rounded-xl border border-gray-200/70 p-3 dark:border-gray-700/70"
  >
    <div class="flex items-center justify-between gap-2 pl-2">
      <h2 class="text-sm font-semibold text-gray-900 dark:text-gray-100">Connections</h2>

      <!-- Below desktop width the panel sits under the sheet, so there is nothing to collapse. -->
      <UTooltip text="Hide connections">
        <UButton
          icon="i-lucide-minus"
          size="xs"
          color="neutral"
          variant="ghost"
          square
          class="hidden justify-center opacity-0 transition-opacity duration-150 group-hover:opacity-100 group-focus-within:opacity-100 motion-reduce:transition-none lg:inline-flex"
          aria-label="Hide connections"
          @click="emit('collapse')"
        />
      </UTooltip>
    </div>

    <section aria-label="Tags" class="flex flex-col gap-2">
      <div class="flex items-center justify-between gap-2 pl-2">
        <h3
          class="flex items-center gap-1.5 text-xs font-semibold tracking-wide text-gray-600 uppercase dark:text-gray-400"
        >
          <UIcon name="i-lucide-tags" class="h-3.5 w-3.5" aria-hidden="true" />
          Tags
        </h3>

        <FileTagPicker :file-id="fileId" file-label="this note" />
      </div>

      <FileTagList :file-id="fileId" class="px-2" />
    </section>

    <section aria-label="About this note" class="flex flex-col gap-2">
      <h3
        class="flex items-center gap-1.5 pl-2 text-xs font-semibold tracking-wide text-gray-600 uppercase dark:text-gray-400"
      >
        <UIcon name="i-lucide-info" class="h-3.5 w-3.5" aria-hidden="true" />
        Note
      </h3>

      <ul class="flex flex-col gap-1.5 px-2 text-sm">
        <li class="flex min-w-0 items-center gap-2">
          <UIcon
            name="i-lucide-folder"
            class="h-4 w-4 shrink-0 text-gray-500 dark:text-gray-500"
            aria-hidden="true"
          />
          <RouterLink
            :to="folderRoute"
            :title="folderTitle"
            class="min-w-0 truncate rounded-xs text-gray-900 hover:underline focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-100"
          >
            {{ folderLabel }}
          </RouterLink>
        </li>

        <li class="flex min-w-0 items-center gap-2">
          <UIcon
            name="i-lucide-text"
            class="h-4 w-4 shrink-0 text-gray-500 dark:text-gray-500"
            aria-hidden="true"
          />
          <span class="truncate tabular-nums text-gray-900 dark:text-gray-100">
            {{ lengthLabel }}
          </span>
        </li>

        <li class="flex min-w-0 items-center gap-2">
          <UIcon
            name="i-lucide-history"
            class="h-4 w-4 shrink-0 text-gray-500 dark:text-gray-500"
            aria-hidden="true"
          />
          <span class="truncate text-gray-900 dark:text-gray-100" :title="formatCalendarDate(editedAt)">
            <span class="tabular-nums">v{{ versionNumber }}</span> · edited
            {{ formatTimeAgo(editedAt) }}
          </span>
        </li>

        <li class="flex min-w-0 items-center gap-2">
          <UIcon
            name="i-lucide-hard-drive"
            class="h-4 w-4 shrink-0 text-gray-500 dark:text-gray-500"
            aria-hidden="true"
          />
          <span class="tabular-nums text-gray-900 dark:text-gray-100">
            {{ formatBytes(sizeBytes) }}
          </span>
        </li>
      </ul>
    </section>

    <p class="px-2 text-xs text-gray-500 dark:text-gray-500">
      Saving isn't available yet, so edits stay in this tab.
    </p>
  </section>
</template>
