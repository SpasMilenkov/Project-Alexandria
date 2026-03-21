<template>
  <UCard :ui="{ body: 'p-0 sm:p-0', header: 'px-6 pt-6 pb-4' }">
    <template #header>
      <div class="flex items-center gap-3">
        <div class="p-2 rounded-lg border border-dashed opacity-60">
          <UIcon name="mdi:chart-donut" class="w-4 h-4" />
        </div>
        <div>
          <h3 class="font-semibold tracking-tight">Storage Breakdown</h3>
          <p class="text-xs opacity-90 mt-0.5">Usage by file type</p>
        </div>
        <div class="ml-auto text-right">
          <p class="text-xs opacity-90 uppercase tracking-widest font-medium">Total</p>
          <p class="text-sm font-semibold tabular-nums">
            {{ isEmpty ? "0 B" : totalFormatted }}
          </p>
        </div>
      </div>
    </template>

    <!-- Empty state -->
    <div
      v-if="isEmpty"
      class="flex flex-col items-center justify-center gap-4 py-12 px-6 text-center"
    >
      <div class="relative w-24 h-24 shrink-0">
        <svg viewBox="0 0 96 96" class="w-full h-full" aria-hidden="true">
          <circle
            cx="48"
            cy="48"
            r="36"
            fill="none"
            stroke="currentColor"
            stroke-width="10"
            stroke-dasharray="6 5"
            stroke-linecap="round"
            class="opacity-10"
          />
          <circle cx="48" cy="48" r="22" class="fill-current opacity-0" />
        </svg>
        <div class="absolute inset-0 flex items-center justify-center">
          <UIcon name="mdi:folder-open-outline" class="w-7 h-7 opacity-20" />
        </div>
      </div>

      <div class="space-y-1 max-w-xs">
        <p class="text-sm font-medium opacity-60">No files tracked yet</p>
        <p class="text-xs opacity-40 leading-relaxed">
          Once files are added, your storage breakdown by type will appear here.
        </p>
      </div>
    </div>

    <!-- Normal state -->
    <div
      v-else
      class="flex flex-col md:flex-row gap-0 divide-y md:divide-y-0 md:divide-x divide-dashed opacity-divide"
    >
      <div class="flex items-center justify-center p-6 md:w-72 shrink-0">
        <div class="relative w-52 h-52">
          <Doughnut
            id="storage-breakdown"
            :options="chartOptions"
            :data="chartData"
            class="w-full h-full"
          />
          <div
            class="absolute inset-0 flex flex-col items-center justify-center pointer-events-none"
          >
            <span class="text-2xl font-semibold tabular-nums leading-none">{{
              topCategory.size
            }}</span>
            <span class="text-xs opacity-90 mt-1 max-w-16 text-center leading-tight">{{
              topCategory.label
            }}</span>
          </div>
        </div>
      </div>

      <!-- Legend / breakdown list -->
      <div class="flex-1 py-4">
        <div
          v-for="(item, i) in breakdown"
          :key="item.label"
          class="flex items-center gap-3 px-5 py-2.5 group hover:bg-black/3 dark:hover:bg-white/3 transition-colors"
        >
          <!-- Swatch -->
          <span
            class="w-2.5 h-2.5 rounded-full shrink-0 ring-1 ring-inset ring-black/10 dark:ring-white/10"
            :style="{ background: palette[i % palette.length] }"
          />

          <!-- Label -->
          <span class="text-sm flex-1 truncate opacity-80">{{ item.label }}</span>

          <!-- Bar -->
          <div class="hidden sm:flex items-center gap-2 w-28">
            <div class="flex-1 h-1 rounded-full bg-black/8 dark:bg-white/8 overflow-hidden">
              <div
                class="h-full rounded-full transition-all duration-500"
                :style="{
                  width: item.pct + '%',
                  background: palette[i % palette.length],
                  opacity: 0.8,
                }"
              />
            </div>
            <span class="text-xs opacity-90 w-8 text-right tabular-nums">{{ item.pct }}%</span>
          </div>

          <!-- Size -->
          <span class="text-xs font-medium tabular-nums opacity-60 w-16 text-right shrink-0">
            {{ item.size }}
          </span>
        </div>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import { Doughnut } from "vue-chartjs";
import { useColorMode } from "@vueuse/core";
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

ChartJS.register(Title, Tooltip, Legend, ArcElement);

const props = defineProps<{
  labels: string[];
  data: number[];
  formattedSize: string[];
}>();

const colorMode = useColorMode();
const isDark = computed(() => colorMode.value === "dark");

const palette = [
  "#C17B5C", // Terracotta      — Documents
  "#5B7FA6", // Slate blue      — Spreadsheets
  "#7A9E87", // Sage green      — Presentations
  "#C9A84C", // Warm amber      — Images
  "#8E6B9E", // Dusty purple    — Videos
  "#5F8A8B", // Teal slate      — Audio
  "#B56B6B", // Muted rose      — Archives
  "#6B8F71", // Moss            — Code
  "#A07850", // Warm brown      — Fonts
  "#607D8B", // Blue grey       — Executables
  "#9C7B6E", // Warm taupe      — Packages
  "#7B9EA6", // Cool sky        — Text
  "#8A8A72", // Warm olive grey — Binary
  "#A89880", // Linen tan       — Uncategorized
];

const total = computed(() => props.data.reduce((s, v) => s + v, 0));

const isEmpty = computed(() => props.data.length === 0 || total.value === 0);

const sortedItems = computed(() =>
  props.labels
    .map((label, i) => ({
      bytes: props.data[i],
      label,
      size: props.formattedSize[i],
    }))
    .sort((a, b) => b.bytes - a.bytes),
);

const breakdown = computed(() =>
  sortedItems.value.map((item) => ({
    label: item.label,
    pct: total.value > 0 ? Math.round((item.bytes / total.value) * 100) : 0,
    size: item.size,
  })),
);

const topCategory = computed(() => breakdown.value[0] ?? { label: "—", size: "—" });

const totalFormatted = computed(() => {
  const bytes = total.value;
  const units = ["B", "KB", "MB", "GB", "TB"];
  let size = bytes;
  let i = 0;
  while (size >= 1024 && i < units.length - 1) {
    size /= 1024;
    i++;
  }
  return `${size.toFixed(1)} ${units[i]}`;
});

const chartData = computed(
  (): ChartData<"doughnut"> => ({
    datasets: [
      {
        backgroundColor: palette.map((c) => c),
        borderColor: isDark.value ? "#1a1a1a" : "#f8f7f4",
        borderWidth: 2,
        data: sortedItems.value.map((item) => item.bytes),
        hoverBorderColor: isDark.value ? "#2a2a2a" : "#ffffff",
        hoverOffset: 4,
      },
    ],
    labels: sortedItems.value.map((item) => item.label),
  }),
);

const chartOptions = computed(
  (): ChartOptions<"doughnut"> => ({
    cutout: "72%",
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: isDark.value ? "#1e1e1e" : "#ffffff",
        bodyColor: isDark.value ? "#999" : "#666",
        borderColor: isDark.value ? "#333" : "#e5e5e5",
        borderWidth: 1,
        callbacks: {
          label: (context) => {
            const item = sortedItems.value[context.dataIndex];
            const pct = total.value > 0 ? Math.round((item.bytes / total.value) * 100) : 0;
            return `  ${item.size}  ·  ${pct}%`;
          },
          title: (items) => `  ${items[0].label}`,
        },
        cornerRadius: 6,
        padding: 10,
        titleColor: isDark.value ? "#e0ddd8" : "#1a1a1a",
      },
    },
    responsive: true,
  }),
);
</script>
