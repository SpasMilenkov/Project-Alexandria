<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";
import { type RouteLocationRaw, RouterLink } from "vue-router";

import { isAuthError } from "@/api/shuffle";
import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { myStorage } from "@/queries/status";
import { getProfileStats } from "@/queries/user";
import { formatCount, formatNumber } from "@/utils/count";
import { formatDuration } from "@/utils/date-formatters";
import { formatBytes } from "@/utils/size";

interface StatTile {
  icon: string;
  label: string;
  title?: string;
  to?: RouteLocationRaw;
  value: string;
}

const {
  data: stats,
  error: statsError,
  isPending: isStatsPending,
  refetch: refetchStats,
} = useQuery(getProfileStats());
const { data: storage } = useQuery(myStorage());

const visibleError = computed(() => {
  if (!statsError.value || isAuthError(statsError.value)) return null;

  return statsError.value;
});

// Storage

const isUnlimited = computed(() => !storage.value || storage.value.quotaBytes <= 0);

const storageLabel = computed(() => {
  if (!storage.value) return "";

  const used = formatBytes(storage.value.usedBytes);
  if (isUnlimited.value) return `${used} used`;

  return `${used} of ${formatBytes(storage.value.quotaBytes)}`;
});

const storagePercent = computed(() => {
  if (!storage.value || isUnlimited.value) return 0;

  return Math.min(100, (storage.value.usedBytes / storage.value.quotaBytes) * 100);
});

const storageColor = computed(() => {
  if (storagePercent.value >= 95) return "error";
  if (storagePercent.value >= 80) return "warning";

  return "primary";
});

// Library

// A new account has nothing to total yet, so it gets a pointer to start instead of a wall of zeros.
const isEmpty = computed(() => {
  if (!stats.value) return false;

  const { files, folders, listenedSeconds, playlists, tags } = stats.value;

  return files + folders + playlists + tags === 0 && listenedSeconds === 0;
});

const listenedLabel = computed(() => {
  if (!stats.value || stats.value.listenedSeconds < 60) return "0h";

  return formatDuration(stats.value.listenedSeconds, { largestUnit: "hour", maxUnits: 1 });
});

const tiles = computed<StatTile[]>(() => {
  if (!stats.value) return [];

  const s = stats.value;

  return [
    {
      icon: "i-lucide-files",
      label: "Files",
      to: { name: "dashboard" },
      value: formatNumber(s.files),
    },
    {
      icon: "i-lucide-folder",
      label: "Folders",
      to: { name: "dashboard" },
      value: formatNumber(s.folders),
    },
    {
      icon: "i-lucide-image",
      label: "Photos",
      to: { name: "gallery" },
      value: formatNumber(s.photos),
    },
    {
      icon: "i-lucide-music",
      label: "Tracks",
      to: { name: "music" },
      value: formatNumber(s.tracks),
    },
    {
      icon: "i-lucide-video",
      label: "Videos",
      to: { name: "videos" },
      value: formatNumber(s.videos),
    },
    {
      icon: "i-lucide-list-music",
      label: "Playlists",
      to: { name: "playlists" },
      value: formatNumber(s.playlists),
    },
    { icon: "i-lucide-tag", label: "Tags", to: { name: "tags" }, value: formatNumber(s.tags) },
    {
      icon: "i-lucide-link",
      label: "Shared links",
      title: "Links that can still be opened",
      value: formatNumber(s.activeShareLinks),
    },
    {
      icon: "i-lucide-headphones",
      label: "Listened",
      title: formatCount(s.plays, "play"),
      to: { name: "history" },
      value: listenedLabel.value,
    },
  ];
});
</script>

<template>
  <div class="flex flex-col gap-4 py-4">
    <RouterLink
      v-if="storage"
      :to="{ name: 'my-storage' }"
      class="-mx-2 flex flex-col gap-2 rounded-lg px-2 py-1.5 transition-colors duration-150 hover:bg-gray-500/10 focus-visible:outline-2 focus-visible:outline-primary motion-reduce:transition-none"
      aria-label="Storage, open My storage"
    >
      <span class="flex items-center justify-between gap-3">
        <span
          class="flex items-center gap-1.5 text-xs font-medium text-gray-600 dark:text-gray-400"
        >
          <UIcon name="i-lucide-hard-drive" class="h-3.5 w-3.5 shrink-0" />
          Storage
        </span>
        <span class="text-xs font-medium tabular-nums text-gray-900 dark:text-gray-100">
          {{ storageLabel }}
        </span>
      </span>

      <UProgress
        v-if="!isUnlimited"
        :model-value="storagePercent"
        :color="storageColor"
        size="xs"
        aria-label="Share of your quota in use"
      />
    </RouterLink>

    <UAlert
      v-if="visibleError"
      color="error"
      variant="subtle"
      icon="i-lucide-alert-circle"
      title="Couldn't load your library totals"
      :description="visibleError.message"
      :actions="[{ color: 'neutral', label: 'Try again', onClick: () => refetchStats() }]"
    />

    <div v-else-if="isStatsPending" class="flex h-40 items-center justify-center">
      <BlockSpinner :size="16" aria-label="Loading library totals" class="text-gray-500" />
    </div>

    <div
      v-else-if="isEmpty"
      class="flex flex-col items-center gap-2 rounded-xl border border-dashed border-gray-300/80 px-4 py-5 text-center dark:border-gray-600/70"
    >
      <UIcon name="i-lucide-files" class="h-5 w-5 text-gray-600 dark:text-gray-400" />
      <p class="m-0 text-sm font-medium text-gray-900 dark:text-gray-100">Nothing here yet</p>
      <p class="m-0 text-xs text-gray-600 dark:text-gray-400">
        Upload files and your library totals show up here.
      </p>
      <UButton
        :to="{ name: 'dashboard' }"
        label="Go to files"
        color="neutral"
        variant="outline"
        size="xs"
        class="mt-1"
      />
    </div>

    <ul
      v-else-if="stats"
      class="m-0 grid list-none grid-cols-3 gap-1.5 p-0"
      aria-label="Your library"
    >
      <li v-for="tile in tiles" :key="tile.label" class="min-w-0">
        <component
          :is="tile.to ? RouterLink : 'div'"
          :to="tile.to"
          :title="tile.title"
          class="flex h-full min-w-0 flex-col gap-1 rounded-lg border border-gray-200/70 px-2.5 py-2 dark:border-gray-700/70"
          :class="{
            'transition-colors duration-150 hover:bg-gray-500/10 focus-visible:outline-2 focus-visible:outline-primary motion-reduce:transition-none':
              tile.to,
          }"
        >
          <span
            class="truncate text-base font-semibold tabular-nums text-gray-900 dark:text-gray-100"
          >
            {{ tile.value }}
          </span>
          <span class="flex min-w-0 items-center gap-1 text-xs text-gray-600 dark:text-gray-400">
            <UIcon :name="tile.icon" class="h-3 w-3 shrink-0" />
            <span class="truncate">{{ tile.label }}</span>
          </span>
        </component>
      </li>
    </ul>
  </div>
</template>
