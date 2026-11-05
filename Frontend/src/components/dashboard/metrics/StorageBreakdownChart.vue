<template>
  <UCard :ui="{ body: 'p-0 sm:p-0', header: 'px-4 pt-4 pb-3 sm:px-6 sm:pt-6 sm:pb-4' }">
    <template #header>
      <div class="flex items-start gap-3">
        <div class="min-w-0">
          <h3 class="text-base font-semibold text-gray-900 dark:text-gray-100">
            Storage breakdown
          </h3>
          <p class="mt-0.5 text-sm text-gray-600 dark:text-gray-400">
            {{ subtitle }}
          </p>
        </div>

        <div class="ml-auto shrink-0 text-right">
          <p class="text-xs font-medium text-gray-600 dark:text-gray-400">Total</p>
          <p class="text-sm font-semibold tabular-nums text-gray-900 dark:text-gray-100">
            {{ formatBytes(total) }}
          </p>
        </div>
      </div>
    </template>

    <div
      v-if="isEmpty"
      class="flex flex-col items-center justify-center gap-3 px-6 py-12 text-center"
    >
      <UIcon name="mdi:chart-donut" class="h-12 w-12 text-gray-400 dark:text-gray-600" />

      <div class="max-w-xs space-y-1">
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">No files tracked yet</p>
        <p class="text-xs text-gray-600 dark:text-gray-400">
          Once files are added, the breakdown by type appears here.
        </p>
      </div>
    </div>

    <!-- Sized against the card, not the viewport, so the admin page's narrower slot scales too. -->
    <div v-else class="@container">
      <div
        class="flex flex-col divide-y divide-gray-200/70 @2xl:flex-row @2xl:divide-x @2xl:divide-y-0 dark:divide-gray-700/70"
      >
        <div class="flex shrink-0 items-center justify-center p-6 @2xl:w-1/3 @5xl:p-8">
          <div class="relative size-52 @2xl:size-60 @4xl:size-72 @6xl:size-80">
            <Doughnut
              id="storage-breakdown"
              :options="chartOptions"
              :data="chartData"
              class="h-full w-full"
            />
            <div
              class="pointer-events-none absolute inset-0 flex flex-col items-center justify-center"
            >
              <span
                class="text-2xl font-semibold leading-none tabular-nums text-gray-900 @4xl:text-3xl dark:text-gray-100"
              >
                {{ topCategory.size }}
              </span>
              <span
                class="mt-1 max-w-24 text-center text-xs leading-tight text-gray-600 @4xl:text-sm dark:text-gray-400"
              >
                {{ topCategory.label }}
              </span>
            </div>
          </div>
        </div>

        <ul class="flex flex-1 flex-col justify-center py-3">
          <li
            v-for="(item, i) in breakdown"
            :key="item.label"
            class="flex items-center gap-3 px-4 py-2 sm:px-5"
          >
            <span class="h-2.5 w-2.5 shrink-0 rounded-full" :style="{ background: colorAt(i) }" />

            <span class="flex-1 truncate text-sm text-gray-900 dark:text-gray-100">
              {{ item.label }}
            </span>

            <div class="hidden w-28 items-center gap-2 sm:flex @4xl:w-40">
              <div class="h-1 flex-1 overflow-hidden rounded-full bg-gray-500/15">
                <div
                  class="h-full rounded-full"
                  :style="{ background: colorAt(i), width: `${item.pct}%` }"
                />
              </div>
              <span class="w-8 text-right text-xs tabular-nums text-gray-600 dark:text-gray-400">
                {{ item.pct }}%
              </span>
            </div>

            <span
              class="w-16 shrink-0 text-right text-xs font-medium tabular-nums text-gray-900 dark:text-gray-100"
            >
              {{ item.size }}
            </span>
          </li>
        </ul>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import {
  ArcElement,
  type ChartData,
  Chart as ChartJS,
  type ChartOptions,
  Legend,
  Title,
  Tooltip,
} from "chart.js";
import { computed } from "vue";
import { Doughnut } from "vue-chartjs";

import { useTheme } from "@/composables/useTheme";
import { formatBytes } from "@/utils/size";

ChartJS.register(Title, Tooltip, Legend, ArcElement);

const {
  labels,
  data,
  formattedSize,
  subtitle = "Usage by file type",
} = defineProps<{
  labels: string[];
  data: number[];
  formattedSize: string[];
  subtitle?: string;
}>();

const { isDark } = useTheme();

const PALETTE = [
  "#C17B5C",
  "#5B7FA6",
  "#7A9E87",
  "#C9A84C",
  "#8E6B9E",
  "#5F8A8B",
  "#B56B6B",
  "#6B8F71",
  "#A07850",
  "#607D8B",
  "#9C7B6E",
  "#7B9EA6",
  "#8A8A72",
  "#A89880",
];

const colorAt = (index: number) => PALETTE[index % PALETTE.length];

const total = computed(() => data.reduce((sum, value) => sum + value, 0));

const isEmpty = computed(() => data.length === 0 || total.value === 0);

const sortedItems = computed(() =>
  labels
    .map((label, i) => ({ bytes: data[i], label, size: formattedSize[i] }))
    .sort((a, b) => b.bytes - a.bytes),
);

const percentOf = (bytes: number) => {
  if (total.value <= 0) return 0;

  return Math.round((bytes / total.value) * 100);
};

const breakdown = computed(() =>
  sortedItems.value.map((item) => ({
    label: item.label,
    pct: percentOf(item.bytes),
    size: item.size,
  })),
);

const topCategory = computed(() => breakdown.value[0] ?? { label: "", size: "" });

const chartData = computed((): ChartData<"doughnut"> => ({
  datasets: [
    {
      backgroundColor: sortedItems.value.map((_, i) => colorAt(i)),
      borderColor: isDark.value ? "#1a1a1a" : "#f8f7f4",
      borderWidth: 2,
      data: sortedItems.value.map((item) => item.bytes),
      hoverBorderColor: isDark.value ? "#2a2a2a" : "#ffffff",
      hoverOffset: 4,
    },
  ],
  labels: sortedItems.value.map((item) => item.label),
}));

const chartOptions = computed((): ChartOptions<"doughnut"> => ({
  cutout: "72%",
  maintainAspectRatio: false,
  plugins: {
    legend: { display: false },
    tooltip: {
      backgroundColor: isDark.value ? "#1e1e1e" : "#ffffff",
      bodyColor: isDark.value ? "#d4d4d4" : "#404040",
      borderColor: isDark.value ? "#333" : "#e5e5e5",
      borderWidth: 1,
      callbacks: {
        label: (context) => {
          const item = sortedItems.value[context.dataIndex];

          return `  ${item.size}  ·  ${percentOf(item.bytes)}%`;
        },
        title: (items) => `  ${items[0].label}`,
      },
      cornerRadius: 6,
      padding: 10,
      titleColor: isDark.value ? "#e0ddd8" : "#1a1a1a",
    },
  },
  responsive: true,
}));
</script>
