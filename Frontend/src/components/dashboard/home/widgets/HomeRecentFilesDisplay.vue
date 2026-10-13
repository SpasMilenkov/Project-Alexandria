<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useResizeObserver } from "@vueuse/core";
import { computed, ref } from "vue";

import type { FileResult } from "@/api/file";

import { useHomeWidgetRows } from "@/composables/useHomeWidgetRows";
import { HomeRecentFilesSource, HomeRecentFilesStyle } from "@/enums/home-widget";
import { formatRelativeShort } from "@/utils/date-formatters";
import { homeFileVisual, homeRecentFileDate } from "@/utils/home-file-widgets";
import { formatBytes } from "@/utils/size.utils";

import HomeRecentFileName from "./HomeRecentFileName.vue";
import HomeRecentFileThumbnail from "./HomeRecentFileThumbnail.vue";

const {
  files,
  source,
  recentStyle,
  disabled = false,
} = defineProps<{
  files: FileResult[];
  source: HomeRecentFilesSource;
  recentStyle: HomeRecentFilesStyle;
  disabled?: boolean;
}>();

const emit = defineEmits<{ open: [file: FileResult] }>();

const rows = ref<HTMLElement | null>(null);

const tiles = ref<HTMLElement | null>(null);

const tileColumns = ref(1);

const tileCapacity = ref(0);

const rowHeight = () => {
  if (recentStyle === HomeRecentFilesStyle.Table) return 32;
  if (recentStyle === HomeRecentFilesStyle.Timeline) return 56;

  return 48;
};

const capacity = useHomeWidgetRows(rows, rowHeight);

const age = (file: FileResult) => {
  const date = homeRecentFileDate(file, source);
  if (!date) return "";

  const value = formatRelativeShort(date);

  return value === "just now" ? value : `${value} ago`;
};

const absoluteDate = (file: FileResult) => {
  const date = homeRecentFileDate(file, source);

  return date ? new Date(date).toLocaleString() : "";
};

const rowModel = (file: FileResult) => ({
  file,
  ...homeFileVisual(file),
  size: formatBytes(Number(file.currentVersion.size)),
  age: age(file),
  date: absoluteDate(file),
});

const visibleRows = computed(() => {
  const sourceFiles = recentStyle === HomeRecentFilesStyle.Deck ? files.slice(1) : files;

  return sourceFiles.slice(0, capacity.value).map(rowModel);
});

const visibleTiles = computed(() =>
  files.slice(0, tileCapacity.value).map((file) => ({ file, age: age(file) })),
);

const newest = computed(() => {
  const file = files[0];

  return file ? rowModel(file) : null;
});

const action = computed(() => (source === HomeRecentFilesSource.Updated ? "Updated" : "Created"));

useResizeObserver(tiles, (entries) => {
  const bounds = entries[0]?.contentRect;
  if (!bounds || !tiles.value) return;

  const style = getComputedStyle(tiles.value);
  const width = Number.parseFloat(style.getPropertyValue("--tile-width"));
  const height = Number.parseFloat(style.getPropertyValue("--tile-height"));
  tileColumns.value = Math.max(1, Math.floor((bounds.width + 8) / (width + 8)));
  tileCapacity.value =
    tileColumns.value * Math.max(0, Math.floor((bounds.height + 8) / (height + 8)));
});
</script>

<template>
  <div class="recent-body text-gray-900 dark:text-gray-100" :data-recent-style="recentStyle">
    <div
      v-if="recentStyle === HomeRecentFilesStyle.Thumbnails"
      ref="tiles"
      class="thumbnail-grid"
      :style="{ gridTemplateColumns: `repeat(${tileColumns}, minmax(0, 1fr))` }"
    >
      <button
        v-for="row in visibleTiles"
        :key="row.file.fileId"
        type="button"
        :disabled="disabled"
        :aria-label="`Open ${row.file.fileName}`"
        class="file-tile file-focus"
        @click="emit('open', row.file)"
      >
        <HomeRecentFileThumbnail :file="row.file" />

        <span class="file-name text-xs">{{ row.file.fileName }}</span>
        <span class="tile-age file-secondary">{{ row.age }}</span>
      </button>
    </div>

    <div
      v-else-if="recentStyle === HomeRecentFilesStyle.Deck && newest"
      class="recent-content deck"
    >
      <div class="deck-stack">
        <button
          type="button"
          :disabled="disabled"
          :aria-label="`Open ${newest.file.fileName}`"
          class="deck-hero file-focus"
          @click="emit('open', newest.file)"
        >
          <HomeRecentFileThumbnail :file="newest.file" cover />

          <span class="deck-caption">
            <span class="file-name">{{ newest.file.fileName }}</span>
            <span class="file-secondary">{{ newest.age }} · {{ newest.size }}</span>
          </span>
        </button>
      </div>

      <div ref="rows" class="recent-rows deck-next">
        <button
          v-for="row in visibleRows"
          :key="row.file.fileId"
          type="button"
          :disabled="disabled"
          class="file-row file-focus"
          :aria-label="`Open ${row.file.fileName}`"
          @click="emit('open', row.file)"
        >
          <Icon
            :icon="row.icon"
            class="size-5 shrink-0"
            :style="{ color: row.color }"
            aria-hidden="true"
          />
          <span class="file-main">
            <span class="file-name">{{ row.file.fileName }}</span>
            <span class="file-secondary">{{ row.age }}</span>
          </span>
        </button>
      </div>
    </div>

    <div
      v-else-if="recentStyle === HomeRecentFilesStyle.Table"
      class="recent-content file-table"
      role="table"
      :aria-label="`${action} files`"
    >
      <div class="table-heading file-secondary" role="row">
        <span role="columnheader">Name</span>
        <span class="size-column" role="columnheader">Size</span>
        <span class="date-column" role="columnheader">{{ action }}</span>
      </div>

      <div ref="rows" class="recent-rows" role="rowgroup">
        <div v-for="row in visibleRows" :key="row.file.fileId" class="table-row" role="row">
          <span class="name-cell" role="cell">
            <button
              type="button"
              :disabled="disabled"
              class="file-focus name-button"
              :aria-label="`Open ${row.file.fileName}`"
              @click="emit('open', row.file)"
            >
              <Icon
                :icon="row.icon"
                class="size-4 shrink-0"
                :style="{ color: row.color }"
                aria-hidden="true"
              />
              <span class="file-name">{{ row.file.fileName }}</span>
            </button>
          </span>
          <span class="size-column file-secondary" role="cell">{{ row.size }}</span>
          <span class="date-column file-secondary" :title="row.date" role="cell">{{
            row.age
          }}</span>
        </div>
      </div>
    </div>

    <div
      v-else
      ref="rows"
      class="recent-content recent-rows"
      :class="{
        'file-list': recentStyle === HomeRecentFilesStyle.List,
        timeline: recentStyle === HomeRecentFilesStyle.Timeline,
      }"
    >
      <button
        v-for="row in visibleRows"
        :key="row.file.fileId"
        type="button"
        :disabled="disabled"
        :aria-label="`Open ${row.file.fileName}`"
        class="file-row file-focus"
        @click="emit('open', row.file)"
      >
        <template v-if="recentStyle === HomeRecentFilesStyle.Timeline">
          <span class="timeline-age file-secondary" :title="row.date">{{ row.age }}</span>
          <span class="timeline-rail" aria-hidden="true"
            ><i :style="{ backgroundColor: row.color }"
          /></span>
        </template>

        <Icon
          v-else
          :icon="row.icon"
          class="size-5 shrink-0"
          :style="{ color: row.color }"
          aria-hidden="true"
        />

        <span class="file-main">
          <HomeRecentFileName
            v-if="recentStyle === HomeRecentFilesStyle.List"
            :file-name="row.file.fileName"
          />
          <span v-else class="file-name">{{ row.file.fileName }}</span>
          <span class="file-metadata file-secondary" :title="`${row.size} · ${row.date}`"
            >{{ row.size }}<span class="inline-age"> · {{ row.age }}</span></span
          >
        </span>

        <span
          v-if="recentStyle === HomeRecentFilesStyle.List"
          class="file-age file-secondary"
          :title="row.date"
          >{{ row.age }}</span
        >
        <span
          v-if="recentStyle === HomeRecentFilesStyle.List"
          class="action-badge"
          :class="{ updated: source === HomeRecentFilesSource.Updated }"
          >{{ action }}</span
        >
      </button>
    </div>
  </div>
</template>

<style scoped>
.recent-body {
  position: relative;
  flex: 1;
  min-height: 0;
  min-width: 0;
  container: recent-files-body / size;
}
.recent-content,
.thumbnail-grid {
  position: absolute;
  inset: 0;
  min-height: 0;
  overflow: hidden;
}
.recent-rows {
  display: flex;
  flex: 1;
  min-height: 0;
  flex-direction: column;
  overflow: hidden;
}
.file-row {
  display: flex;
  width: 100%;
  height: 48px;
  flex-shrink: 0;
  align-items: center;
  gap: 8px;
  text-align: left;
  padding: 0 8px;
  border-radius: 8px;
}
.file-row + .file-row,
.table-row + .table-row {
  border-top: 1px solid var(--ui-border);
}
.file-focus {
  cursor: pointer;
  transition: background-color 150ms;
}
.file-focus:enabled:hover {
  background-color: var(--ui-bg-elevated);
}
.file-focus:focus-visible {
  outline: 2px solid var(--ui-primary);
  outline-offset: -2px;
}
.file-main {
  display: flex;
  flex: 1;
  min-width: 0;
  flex-direction: column;
  gap: 2px;
  line-height: 16px;
}

.file-metadata {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.file-name {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12px;
  font-weight: 500;
}
.file-secondary {
  font-size: 12px;
  color: var(--ui-text-muted);
}
.file-age {
  display: none;
  white-space: nowrap;
}
.action-badge {
  display: none;
  flex-shrink: 0;
  border-radius: 8px;
  padding: 0 8px;
  font-size: 11px;
  line-height: 20px;
  background: #dcfce7;
  color: #166534;
}
.action-badge.updated {
  background: #dbeafe;
  color: #1e40af;
}

.action-badge:where(.dark *) {
  background: #163428;
  color: #86efac;
}

.action-badge.updated:where(.dark *) {
  background: #1c304f;
  color: #93c5fd;
}

.thumbnail-grid {
  --tile-width: 104px;
  --tile-height: 112px;
  display: grid;
  gap: 8px;
  grid-auto-rows: var(--tile-height);
  align-content: start;
}
.file-tile {
  display: flex;
  min-width: 0;
  min-height: 0;
  flex-direction: column;
  gap: 4px;
  text-align: left;
  border-radius: 8px;
}
.file-tile .file-name,
.file-tile .tile-age {
  padding: 0 4px;
}
.timeline .file-row {
  display: grid;
  grid-template-columns: 72px 16px minmax(0, 1fr);
  height: 56px;
  border: 0;
}
.timeline-age {
  text-align: right;
  white-space: nowrap;
}
.timeline-rail {
  position: relative;
  align-self: stretch;
}
.timeline-rail::before {
  content: "";
  position: absolute;
  left: 50%;
  top: 0;
  bottom: 0;
  width: 2px;
  background: var(--ui-border);
  transform: translateX(-50%);
}
.timeline-rail i {
  position: absolute;
  left: 50%;
  top: 50%;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  transform: translate(-50%, -50%);
}
.timeline .inline-age {
  display: none;
}

.deck {
  display: flex;
  gap: 16px;
}
.deck-stack {
  position: relative;
  flex: 1;
  min-height: 0;
  min-width: 0;
  margin-bottom: 16px;
}
.deck-stack::before,
.deck-stack::after {
  content: "";
  position: absolute;
  left: 8px;
  right: 8px;
  bottom: -8px;
  height: 16px;
  border: 1px solid var(--ui-border);
  border-radius: 8px;
  background: var(--ui-bg-elevated);
}
.deck-stack::after {
  left: 16px;
  right: 16px;
  bottom: -16px;
  z-index: -1;
}
.deck-hero {
  position: absolute;
  inset: 0;
  z-index: 1;
  display: flex;
  flex-direction: column;
  width: 100%;
  overflow: hidden;
  border-radius: 8px;
  transition:
    transform 150ms ease-out,
    box-shadow 150ms ease-out;
}

@media (hover: hover) {
  .deck-hero:enabled:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgb(0 0 0 / 12%);
  }
}

.deck-hero:enabled:active {
  transform: translateY(1px);
}
.deck-caption {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 8px;
  text-align: left;
  background: var(--ui-bg-elevated);
}
.deck-next {
  display: none;
}

.file-table {
  --table-columns: minmax(0, 1fr) 72px;
  display: flex;
  flex-direction: column;
}
.table-heading,
.table-row {
  display: grid;
  grid-template-columns: var(--table-columns);
  gap: 8px;
  align-items: center;
}
.table-heading {
  height: 24px;
  flex-shrink: 0;
  padding: 0 8px;
  border-bottom: 1px solid var(--ui-border);
  font-weight: 600;
}
.table-row {
  height: 32px;
  flex-shrink: 0;
  padding: 0 8px;
}
.name-cell {
  min-width: 0;
}
.name-button {
  display: flex;
  min-width: 0;
  width: 100%;
  height: 32px;
  align-items: center;
  gap: 8px;
  border-radius: 8px;
  text-align: left;
}
.size-column {
  display: none;
  text-align: right;
  white-space: nowrap;
}
.date-column {
  text-align: right;
  white-space: nowrap;
}

@container recent-files-body (min-width: 300px) {
  .file-age,
  .action-badge {
    display: block;
  }
  .inline-age {
    display: none;
  }
}

@container recent-files-body (min-width: 420px) {
  .file-table {
    --table-columns: minmax(0, 1fr) 80px 88px;
  }
  .size-column {
    display: block;
  }
}

@container recent-files-body (max-width: 220px) {
  .file-row {
    padding-inline: 0;
  }

  .file-list .file-row > svg {
    width: 16px;
    height: 16px;
  }

  .file-list .inline-age {
    display: none;
  }

  .thumbnail-grid {
    --tile-width: 60px;
    --tile-height: 76px;
  }
  .tile-age {
    display: none;
  }
  .timeline .file-row {
    grid-template-columns: 16px minmax(0, 1fr);
  }
  .timeline-age {
    display: none;
  }
  .timeline .inline-age {
    display: inline;
  }
  .file-table {
    --table-columns: minmax(0, 1fr);
  }
  .date-column {
    display: none;
  }
}

@container recent-files-body (min-aspect-ratio: 2 / 1) {
  .thumbnail-grid {
    --tile-width: 68px;
    --tile-height: 76px;
  }
  .tile-age {
    display: none;
  }
}

@container recent-files-body (min-aspect-ratio: 3 / 2) {
  .deck-next {
    display: flex;
  }
}

@media (prefers-reduced-motion: reduce) {
  .file-focus,
  .deck-hero {
    transition: none;
  }

  .deck-hero:enabled:hover,
  .deck-hero:enabled:active {
    transform: none;
  }
}
</style>
