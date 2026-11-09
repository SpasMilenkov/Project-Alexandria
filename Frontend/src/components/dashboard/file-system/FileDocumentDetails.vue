<template>
  <DetailsSection :title="isBook ? 'Book details' : 'Document details'">
    <div v-if="isPending" class="flex h-8 items-center text-gray-500">
      <BlockSpinner :size="16" />
    </div>

    <p v-else-if="error" class="text-sm text-gray-600 dark:text-gray-400">
      Couldn't load the document details.
    </p>

    <p v-else-if="!metadata?.isExtracted" class="text-sm text-gray-600 dark:text-gray-400">
      {{ notReadMessage }}
    </p>

    <p v-else-if="!hasAnything" class="text-sm text-gray-600 dark:text-gray-400">
      {{ emptyMessage }}
    </p>

    <template v-else>
      <ul
        v-if="facts.length > 0"
        class="flex flex-col divide-y divide-gray-200/70 rounded-xl border border-gray-200/70 dark:divide-gray-700/70 dark:border-gray-700/70"
      >
        <li v-for="fact in facts" :key="fact.key" class="flex items-start gap-3 p-3">
          <Icon
            :icon="fact.icon"
            class="mt-0.5 h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
            aria-hidden="true"
          />

          <div class="flex min-w-0 flex-1 flex-col gap-0.5">
            <span class="text-sm font-medium wrap-break-word text-gray-900 dark:text-gray-100">
              {{ fact.title }}
            </span>

            <span
              v-if="fact.detail"
              class="text-xs wrap-break-word text-gray-600 dark:text-gray-400"
            >
              {{ fact.detail }}
            </span>

            <span v-if="fact.note" class="text-xs text-gray-500 dark:text-gray-500">
              {{ fact.note }}
            </span>
          </div>
        </li>
      </ul>

      <div v-if="tiles.length > 0" class="grid grid-cols-2 gap-2">
        <DetailsTile
          v-for="tile in tiles"
          :key="tile.key"
          :label="tile.label"
          :class="{ 'col-span-2': tile.wide }"
        >
          <span class="block wrap-break-word tabular-nums">{{ tile.value }}</span>
        </DetailsTile>
      </div>
    </template>
  </DetailsSection>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { DocumentMetadataDto } from "@/api/file";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import DetailsSection from "@/components/common/details/DetailsSection.vue";
import DetailsTile from "@/components/common/details/DetailsTile.vue";
import {
  countTiles,
  describeDocumentMoment,
  formatEditingTime,
  formatLanguage,
  formatSeriesPosition,
} from "@/features/files/document-metadata";
import { getDocumentMetadata } from "@/queries/files";

interface DocumentFact {
  key: string;
  icon: string;
  title: string;
  detail?: string;
  note?: string;
}

interface DetailTile {
  key: string;
  label: string;
  value: string;
  wide?: boolean;
}

const {
  fileId,
  versionId,
  enabled,
  isBook = false,
} = defineProps<{
  fileId: string;
  versionId: string;
  enabled: boolean;
  isBook?: boolean;
}>();

// Books have no preview job, so the background sweep reads them on its own schedule.
const notReadMessage = computed(() =>
  isBook
    ? "Not read yet. Details appear within a few minutes of uploading."
    : "Not read yet. Details appear once the preview is generated.",
);

const emptyMessage = computed(() =>
  isBook
    ? "This book doesn't record who wrote it or when."
    : "This document doesn't record who wrote it or when.",
);

const {
  data: metadata,
  isPending,
  error,
} = useQuery(() => ({
  ...getDocumentMetadata({ fileId, versionId }),
  enabled,
}));

const protectedFact = (m: DocumentMetadataDto): DocumentFact | null => {
  if (!m.isPasswordProtected) return null;

  return {
    key: "protected",
    icon: "mdi:file-lock-outline",
    title: "Password protected",
    detail: "Details stay hidden until the file is opened with the password.",
  };
};

const drmFact = (m: DocumentMetadataDto): DocumentFact | null => {
  if (!m.isDrmProtected) return null;

  return {
    key: "drm",
    icon: "mdi:shield-lock-outline",
    title: "Copy protected",
    detail: "The store that sold it locked the pages, so only its reading apps can open them.",
  };
};

const seriesFact = (m: DocumentMetadataDto): DocumentFact | null => {
  if (!m.series) return null;

  const fact: DocumentFact = { key: "series", icon: "mdi:book-multiple-outline", title: m.series };
  if (m.seriesIndex !== null) fact.detail = formatSeriesPosition(m.seriesIndex);

  return fact;
};

const titleFact = (m: DocumentMetadataDto): DocumentFact | null => {
  if (!m.title) return null;

  const fact: DocumentFact = { key: "title", icon: "mdi:text-box-outline", title: m.title };
  if (m.subject) fact.detail = m.subject;

  return fact;
};

const authorFact = (m: DocumentMetadataDto): DocumentFact | null => {
  let fact: DocumentFact | null = null;

  if (m.author) {
    fact = { key: "author", icon: "mdi:account-outline", title: m.author };
    if (m.lastModifiedBy && m.lastModifiedBy !== m.author) {
      fact.detail = `Last edited by ${m.lastModifiedBy}`;
    }
  } else if (m.lastModifiedBy) {
    fact = { key: "author", icon: "mdi:account-outline", title: m.lastModifiedBy };
    fact.detail = "Last to edit it";
  }

  if (fact && m.company) fact.note = m.company;

  return fact;
};

const momentFact = (m: DocumentMetadataDto): DocumentFact | null => {
  const icon = "mdi:calendar-clock-outline";
  const written = m.authoredAt
    ? describeDocumentMoment(m.authoredAt, m.authoredOffsetMinutes)
    : null;
  const saved = m.modifiedAt ? describeDocumentMoment(m.modifiedAt, m.modifiedOffsetMinutes) : null;

  // A book's date is when this edition came out, and later saves are revisions to the file.
  const firstLabel = isBook ? "Published" : "Written";
  const savedLabel = isBook ? "Last updated" : "Last saved";

  if (written) {
    const fact: DocumentFact = { key: "moment", icon, title: `${firstLabel} ${written.date}` };
    if (saved && saved.date !== written.date) fact.detail = `${savedLabel} ${saved.date}`;
    if (written.zoneNote) fact.note = written.zoneNote;

    return fact;
  }

  if (saved) {
    const fact: DocumentFact = { key: "moment", icon, title: `${savedLabel} ${saved.date}` };
    if (saved.zoneNote) fact.note = saved.zoneNote;

    return fact;
  }

  return null;
};

const appFact = (m: DocumentMetadataDto): DocumentFact | null => {
  const title = m.application ?? m.producer;
  if (!title) return null;

  const fact: DocumentFact = { key: "app", icon: "mdi:pencil-outline", title };
  if (m.application && m.producer && m.producer !== m.application) {
    fact.detail = `Saved as PDF with ${m.producer}`;
  }

  return fact;
};

const facts = computed((): DocumentFact[] => {
  const m = metadata.value;
  if (!m?.isExtracted) return [];

  return [
    protectedFact(m),
    drmFact(m),
    titleFact(m),
    seriesFact(m),
    authorFact(m),
    momentFact(m),
    appFact(m),
  ].filter((fact): fact is DocumentFact => fact !== null);
});

const tiles = computed((): DetailTile[] => {
  const m = metadata.value;
  if (!m?.isExtracted) return [];

  const result: DetailTile[] = countTiles(m);

  if (m.editingMinutes !== null) {
    result.push({
      key: "editing",
      label: "Editing time",
      value: formatEditingTime(m.editingMinutes),
    });
  }

  if (m.paperSize) result.push({ key: "paper", label: "Paper size", value: m.paperSize });
  if (m.publisher) result.push({ key: "publisher", label: "Publisher", value: m.publisher });
  if (m.isbn) result.push({ key: "isbn", label: "ISBN", value: m.isbn });
  if (m.language)
    result.push({ key: "language", label: "Language", value: formatLanguage(m.language) });

  // An odd half-width tile would leave a hole in the two-column grid, so it spans instead.
  const lastTile = result.at(-1);
  if (lastTile && result.length % 2 === 1) lastTile.wide = true;

  if (m.keywords) {
    const label = isBook ? "Subjects" : "Keywords";
    result.push({ key: "keywords", label, value: m.keywords, wide: true });
  }

  return result;
});

const hasAnything = computed(() => facts.value.length > 0 || tiles.value.length > 0);
</script>
