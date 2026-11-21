<template>
  <ExplorerTooltipCard
    :icon="getFileIcon(data.fileName)"
    :name="data.fileName"
    :subtitle="readableType"
  >
    <template v-if="$slots.lead" #lead>
      <slot name="lead" />
    </template>

    <ExplorerTooltipStat label="Size" emphasis>{{ sizeLabel }}</ExplorerTooltipStat>

    <ExplorerTooltipStat label="Version">
      v{{ data.currentVersion.versionNumber }}
    </ExplorerTooltipStat>

    <ExplorerTooltipStat label="Created">{{ createdLabel }}</ExplorerTooltipStat>

    <ExplorerTooltipStat label="Owner">{{ data.owner.name }}</ExplorerTooltipStat>

    <template v-if="hasNotes" #notes>
      <ExplorerTooltipNote v-if="data.currentVersion.isEncrypted" icon="i-lucide-lock">
        This file is encrypted
      </ExplorerTooltipNote>

      <ExplorerTooltipNote v-if="data.currentVersion.isPasswordProtected" icon="i-lucide-file-lock">
        Opening it needs the document's password
      </ExplorerTooltipNote>
    </template>

    <template v-if="tooltipTags.length > 0" #footer>
      <ul class="flex flex-wrap gap-1.5" aria-label="Tags">
        <li
          v-for="tag in visibleTags"
          :key="tag.id"
          class="inline-flex max-w-full items-center gap-1.5 rounded-full border border-gray-200/70 px-2 py-0.5 text-xs font-medium text-gray-900 dark:border-gray-700/70 dark:text-gray-100"
        >
          <span
            class="size-2 shrink-0 rounded-full"
            :style="{ backgroundColor: tag.color }"
            aria-hidden="true"
          />
          <span class="truncate">{{ tag.name }}</span>
        </li>

        <li
          v-if="hiddenTagCount > 0"
          class="inline-flex items-center rounded-full bg-gray-500/10 px-2 py-0.5 text-xs font-medium text-gray-600 dark:text-gray-400"
        >
          +{{ hiddenTagCount }}
        </li>
      </ul>
    </template>
  </ExplorerTooltipCard>
</template>

<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { FileResult } from "@/api/file";
import type { TagDto } from "@/api/tag";

import { getTagsForFile } from "@/queries/tags";
import { formatTimeAgo } from "@/utils/date-formatters";
import { getFileIcon } from "@/utils/files/icons";
import { getFileTypeReadable } from "@/utils/files/mimetype";
import { formatBytes } from "@/utils/size";

import ExplorerTooltipCard from "./tooltip/ExplorerTooltipCard.vue";
import ExplorerTooltipNote from "./tooltip/ExplorerTooltipNote.vue";
import ExplorerTooltipStat from "./tooltip/ExplorerTooltipStat.vue";

const MAX_VISIBLE_TAGS = 4;

const { data } = defineProps<{
  data: FileResult;
}>();

// Authoritative tag source with the listing snapshot as instant fallback.
// Row hover already warms this query, so tooltip reads are usually cached and
// tag edits converge here without refetching the whole file listing.
const { data: fileTags } = useQuery(() => getTagsForFile(data.fileId));

const tooltipTags = computed((): TagDto[] => fileTags.value?.tags ?? data.tags ?? []);

const visibleTags = computed(() => tooltipTags.value.slice(0, MAX_VISIBLE_TAGS));

const hiddenTagCount = computed(() => tooltipTags.value.length - visibleTags.value.length);

const hasNotes = computed(
  () => data.currentVersion.isEncrypted || data.currentVersion.isPasswordProtected,
);

const readableType = computed(() =>
  getFileTypeReadable(data.currentVersion.mimeType, data.fileName),
);

const sizeLabel = computed(() => formatBytes(Number(data.currentVersion.size)));

const createdLabel = computed(() => formatTimeAgo(data.createdAt));
</script>
