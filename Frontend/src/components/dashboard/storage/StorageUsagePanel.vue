<template>
  <section
    aria-labelledby="storage-usage-heading"
    class="rounded-[14px] border border-gray-200/70 p-4 frosted-glass glass-surface sm:p-6 dark:border-gray-700/70"
  >
    <div class="flex flex-wrap items-end justify-between gap-x-6 gap-y-2">
      <div class="min-w-0">
        <h2 id="storage-usage-heading" class="text-sm font-medium text-gray-600 dark:text-gray-400">
          Space used
        </h2>
        <p class="mt-1 text-3xl font-semibold tabular-nums text-gray-900 dark:text-gray-100">
          {{ formatBytes(data.usedBytes) }}
          <span class="text-base font-medium text-gray-600 dark:text-gray-400">
            of {{ quotaLabel }}
          </span>
        </p>
      </div>

      <p v-if="remainingLabel" class="text-sm font-medium" :class="remainingClass">
        {{ remainingLabel }}
      </p>
    </div>

    <UProgressGroup class="mt-4" :max="progressMax" :items="segments" size="md" />

    <ul class="mt-3 flex flex-wrap gap-x-6 gap-y-1" aria-label="Usage by kind">
      <li v-for="segment in legend" :key="segment.label" class="flex items-center gap-2 text-sm">
        <span class="h-2.5 w-2.5 shrink-0 rounded-full" :class="segment.dotClass" />
        <span class="text-gray-600 dark:text-gray-400">{{ segment.label }}</span>
        <span class="font-medium tabular-nums text-gray-900 dark:text-gray-100">
          {{ formatBytes(segment.value) }}
        </span>
      </li>
    </ul>

    <div class="mt-6 grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
      <div
        v-for="tile in tiles"
        :key="tile.label"
        class="flex items-start gap-3 rounded-xl border border-gray-200/70 p-4 dark:border-gray-700/70"
      >
        <UIcon :name="tile.icon" class="mt-0.5 h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400" />

        <div class="min-w-0 flex-1">
          <p class="text-sm text-gray-600 dark:text-gray-400">{{ tile.label }}</p>
          <p class="text-xl font-semibold tabular-nums text-gray-900 dark:text-gray-100">
            {{ tile.value }}
          </p>
          <p class="truncate text-xs text-gray-600 dark:text-gray-400">{{ tile.detail }}</p>
        </div>

        <UButton
          v-if="tile.to"
          :to="tile.to"
          variant="ghost"
          color="neutral"
          size="xs"
          trailing-icon="i-lucide-arrow-right"
          class="-mr-1 shrink-0"
        >
          Open
        </UButton>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import type { ProgressGroupItem } from "@nuxt/ui";

import { computed } from "vue";

import type { StorageBreakdown } from "@/api/status";

import { formatBytes } from "@/utils/size";

const { data } = defineProps<{
  data: StorageBreakdown;
}>();

const pluralize = (count: number, singular: string, plural: string) => {
  if (count === 1) return `1 ${singular}`;

  return `${count.toLocaleString()} ${plural}`;
};

const isUnlimited = computed(() => data.quotaBytes <= 0);

const remainingBytes = computed(() => data.quotaBytes - data.usedBytes);

const quotaLabel = computed(() => {
  if (isUnlimited.value) return "unlimited";

  return formatBytes(data.quotaBytes);
});

const remainingLabel = computed(() => {
  if (isUnlimited.value) return "";

  if (remainingBytes.value < 0) return `${formatBytes(-remainingBytes.value)} over quota`;

  return `${formatBytes(remainingBytes.value)} remaining`;
});

const remainingClass = computed(() => {
  if (remainingBytes.value < 0) return "text-error";

  return "text-gray-900 dark:text-gray-100";
});

const progressMax = computed(() => {
  if (data.quotaBytes > 0) return data.quotaBytes;

  return Math.max(1, data.usedBytes);
});

const legend = computed(() => [
  { color: "primary" as const, dotClass: "bg-primary", label: "Files", value: data.filesSize },
  { color: "info" as const, dotClass: "bg-info", label: "Previews", value: data.previewsSize },
  {
    color: "warning" as const,
    dotClass: "bg-warning",
    label: "Transcoded",
    value: data.transcodedSize,
  },
]);

const segments = computed<ProgressGroupItem[]>(() =>
  legend.value.map(({ color, value }) => ({ color, value })),
);

const tiles = computed(() => [
  {
    detail: "Your originals",
    icon: "i-lucide-files",
    label: "Files",
    to: null,
    value: formatBytes(data.filesSize),
  },
  {
    detail: `${pluralize(data.previewsCount, "artifact", "artifacts")}, regenerable`,
    icon: "i-lucide-image",
    label: "Previews",
    to: null,
    value: formatBytes(data.previewsSize),
  },
  {
    detail: `${pluralize(data.representationsCount, "representation", "representations")} for streaming`,
    icon: "i-lucide-layers",
    label: "Transcoded",
    to: null,
    value: formatBytes(data.transcodedSize),
  },
  {
    detail: "Restorable for 30 days after deletion",
    icon: "i-lucide-trash-2",
    label: "Trash",
    to: "/dashboard/trash",
    value: formatBytes(data.trashSize),
  },
]);
</script>
