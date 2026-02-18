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
          <p class="text-xs opacity-90 uppercase tracking-widest font-medium">
            Total
          </p>
          <p class="text-sm font-semibold tabular-nums">{{ totalFormatted }}</p>
        </div>
      </div>
    </template>

    <div
      class="flex flex-col md:flex-row gap-0 divide-y md:divide-y-0 md:divide-x divide-dashed opacity-divide"
    >
      <!-- Donut chart -->
      <div class="flex items-center justify-center p-6 md:w-72 shrink-0">
        <div class="relative w-52 h-52">
          <Doughnut
            id="storage-breakdown"
            :options="chartOptions"
            :data="chartData"
            class="w-full h-full"
          />
          <!-- Center label -->
          <div
            class="absolute inset-0 flex flex-col items-center justify-center pointer-events-none"
          >
            <span class="text-2xl font-semibold tabular-nums leading-none">{{
              topCategory.size
            }}</span>
            <span
              class="text-xs opacity-90 mt-1 max-w-16 text-center leading-tight"
              >{{ topCategory.label }}</span
            >
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
          <span class="text-sm flex-1 truncate opacity-80">{{
            item.label
          }}</span>

          <!-- Bar -->
          <div class="hidden sm:flex items-center gap-2 w-28">
            <div
              class="flex-1 h-1 rounded-full bg-black/8 dark:bg-white/8 overflow-hidden"
            >
              <div
                class="h-full rounded-full transition-all duration-500"
                :style="{
                  width: item.pct + '%',
                  background: palette[i % palette.length],
                  opacity: 0.8,
                }"
              />
            </div>
            <span class="text-xs opacity-90 w-8 text-right tabular-nums"
              >{{ item.pct }}%</span
            >
          </div>

          <!-- Size -->
          <span
            class="text-xs font-medium tabular-nums opacity-60 w-16 text-right shrink-0"
          >
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
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  ArcElement,
  type ChartData,
  type ChartOptions,
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

// Cohesive muted palette — one entry per FileGroup (14 total), ordered to
// maximise contrast between visually adjacent slices.
// Groups: Documents, Spreadsheets, Presentations, Images, Videos, Audio,
//         Archives, Code, Fonts, Executables, Packages, Text, Binary, Uncategorized
const palette = [
  "#C17B5C", // terracotta      — Documents
  "#5B7FA6", // slate blue      — Spreadsheets
  "#7A9E87", // sage green      — Presentations
  "#C9A84C", // warm amber      — Images
  "#8E6B9E", // dusty purple    — Videos
  "#5F8A8B", // teal slate      — Audio
  "#B56B6B", // muted rose      — Archives
  "#6B8F71", // moss            — Code
  "#A07850", // warm brown      — Fonts
  "#607D8B", // blue grey       — Executables
  "#9C7B6E", // warm taupe      — Packages
  "#7B9EA6", // cool sky        — Text
  "#8A8A72", // warm olive grey — Binary
  "#A89880", // linen tan       — Uncategorized
];

const total = computed(() => props.data.reduce((s, v) => s + v, 0));

const breakdown = computed(() =>
  props.labels
    .map((label, i) => ({
      label,
      size: props.formattedSize[i],
      pct:
        total.value > 0 ? Math.round((props.data[i] / total.value) * 100) : 0,
    }))
    .sort((a, b) => b.pct - a.pct),
);

const topCategory = computed(
  () => breakdown.value[0] ?? { label: "—", size: "—" },
);

const totalFormatted = computed(() => {
  if (!props.formattedSize.length) return "—";
  // Re-use the last formattedSize entry logic — just sum bytes and reformat
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
    labels: props.labels,
    datasets: [
      {
        data: props.data,
        backgroundColor: palette.map((c, i) => (i < props.data.length ? c : c)),
        borderWidth: 2,
        borderColor: isDark.value ? "#1a1a1a" : "#f8f7f4",
        hoverBorderColor: isDark.value ? "#2a2a2a" : "#ffffff",
        hoverOffset: 4,
      },
    ],
  }),
);

const chartOptions = computed(
  (): ChartOptions<"doughnut"> => ({
    responsive: true,
    maintainAspectRatio: false,
    cutout: "72%",
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: isDark.value ? "#1e1e1e" : "#ffffff",
        borderColor: isDark.value ? "#333" : "#e5e5e5",
        borderWidth: 1,
        titleColor: isDark.value ? "#e0ddd8" : "#1a1a1a",
        bodyColor: isDark.value ? "#999" : "#666",
        padding: 10,
        cornerRadius: 6,
        callbacks: {
          label: (context) => {
            const index = context.dataIndex;
            const label = props.labels[index];
            const formatted = props.formattedSize[index];
            const pct =
              total.value > 0
                ? Math.round((props.data[index] / total.value) * 100)
                : 0;
            return `  ${formatted}  ·  ${pct}%`;
          },
          title: (items) => `  ${items[0].label}`,
        },
      },
    },
  }),
);
</script>
