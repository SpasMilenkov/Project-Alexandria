<template>
  <div :id="id" role="listbox" aria-label="Search results" class="space-y-6 py-4">
    <div v-if="directories.length > 0" role="group" aria-label="Folders">
      <div class="mb-3 flex items-center gap-2 px-4">
        <UIcon name="i-lucide-folder" class="w-4 h-4 text-gray-500 dark:text-gray-500" />
        <h4 class="text-xs font-semibold uppercase tracking-wider text-gray-600 dark:text-gray-400">
          Folders ({{ directoryCountLabel }})
        </h4>
      </div>
      <div class="space-y-1.5">
        <!-- Mousemove rather than mouseenter, so rows scrolling under a resting pointer keep the keyboard's row. -->
        <div
          v-for="(dir, index) in directories"
          :id="optionId(index)"
          :key="dir.id"
          role="option"
          :aria-selected="index === activeIndex"
          @mousemove="activate(index)"
        >
          <DirectoryItem
            :data="dir"
            view-mode="list"
            :is-selected="index === activeIndex"
            @navigate="emit('open', folderTarget(dir))"
            @click="emit('open', folderTarget(dir))"
            @tooltip-enter="emit('preview', folderTarget(dir))"
          />
        </div>
      </div>
    </div>

    <div v-if="files.length > 0" role="group" aria-label="Files">
      <div class="mb-3 flex items-center gap-2 px-4">
        <UIcon name="i-lucide-file" class="w-4 h-4 text-gray-500 dark:text-gray-500" />
        <h4 class="text-xs font-semibold uppercase tracking-wider text-gray-600 dark:text-gray-400">
          Files ({{ fileCountLabel }})
        </h4>
      </div>
      <div class="space-y-1.5">
        <div
          v-for="(file, index) in files"
          :id="optionId(directories.length + index)"
          :key="file.fileId"
          role="option"
          :aria-selected="directories.length + index === activeIndex"
          @mousemove="activate(directories.length + index)"
        >
          <FileItem
            :data="file"
            :tags="file.tags"
            view-mode="list"
            :is-selected="directories.length + index === activeIndex"
            @click="emit('open', fileTarget(file))"
            @tooltip-enter="emit('preview', fileTarget(file))"
          >
            <template v-if="file.matchedInContent && query" #subtitle>
              <SearchRowExcerpt
                :file-id="file.fileId"
                :version-id="file.currentVersion.id"
                :query="query"
              />
            </template>
          </FileItem>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { DirectorySummaryDto } from "@/api/directory";
import type { FileResult } from "@/api/file";
import type { SearchPreviewTarget } from "@/types/search-preview-target";

import SearchRowExcerpt from "./SearchRowExcerpt.vue";

// Folders come first and files after them, and the active index runs across
// both in that order, matching how the keyboard moves through the list.
// The query is the searched text, for the excerpt under a file found by its text.
const {
  activeIndex,
  optionIdPrefix,
  query = "",
} = defineProps<{
  activeIndex: number;
  directories: DirectorySummaryDto[];
  directoryCountLabel: string;
  fileCountLabel: string;
  files: FileResult[];
  id: string;
  optionIdPrefix: string;
  query?: string;
}>();

const emit = defineEmits<{
  activate: [index: number];
  open: [result: SearchPreviewTarget];
  preview: [result: SearchPreviewTarget];
}>();

const optionId = (index: number) => `${optionIdPrefix}-${index}`;

const folderTarget = (directory: DirectorySummaryDto): SearchPreviewTarget => ({
  directory,
  kind: "directory",
});

const fileTarget = (file: FileResult): SearchPreviewTarget => ({ file, kind: "file" });

const activate = (index: number) => {
  if (index !== activeIndex) {
    emit("activate", index);
  }
};
</script>
