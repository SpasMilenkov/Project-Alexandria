<script setup lang="ts">
import { useQuery, useQueryCache } from "@pinia/colada";
import { computed } from "vue";

import type { PreviewResultDto } from "@/api/file";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { atPdfPage } from "@/features/notes/deep-links";
import { FILES_QUERY_KEYS, getPreviewByVersion } from "@/queries/files";

// The same preview the file drawer shows, sized for a note: a picture, or a PDF at a page.
const { fileId, versionId, mimeType, name, page, kind } = defineProps<{
  fileId: string;
  versionId: string;
  mimeType: string;
  name: string;
  page: number | null;
  kind: "image" | "pdf";
}>();

const queryCache = useQueryCache();

// Reading the cached entry keeps an empty answer on the short stale window, as in the drawer.
const { data, isPending } = useQuery(() =>
  getPreviewByVersion({
    cached: queryCache.getQueryData<PreviewResultDto>(
      FILES_QUERY_KEYS.previewByVersion(fileId, versionId),
    ),
    fileId,
    mimeType,
    versionId,
  }),
);

const previewUrl = computed(() => data.value?.previewUrl ?? null);
const pdfSrc = computed(() => atPdfPage(previewUrl.value ?? "", page));
</script>

<template>
  <span v-if="isPending" class="flex h-32 items-center justify-center text-gray-500">
    <BlockSpinner :size="20" aria-label="Loading preview" />
  </span>

  <img
    v-else-if="previewUrl && kind === 'image'"
    :src="previewUrl"
    :alt="name"
    loading="lazy"
    class="mx-auto block max-h-[28rem] w-auto max-w-full rounded-md object-contain"
  />

  <!-- Keyed by page, since the PDF viewer reads the fragment only when it loads. -->
  <embed
    v-else-if="previewUrl"
    :key="pdfSrc"
    :src="pdfSrc"
    type="application/pdf"
    class="block h-[32rem] w-full rounded-md"
  />

  <span v-else class="block px-1 py-4 text-sm text-gray-600 dark:text-gray-400">
    No preview yet. It shows up once the file has been processed.
  </span>
</template>
