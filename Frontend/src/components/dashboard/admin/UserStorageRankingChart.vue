<template>
  <UCard :ui="{ body: 'p-0 sm:p-0', header: 'px-6 pt-6 pb-4' }">
    <template #header>
      <div class="flex items-center gap-3">
        <div class="p-2 rounded-lg border border-dashed opacity-60">
          <UIcon name="mdi:account-multiple" class="w-4 h-4" />
        </div>
        <div>
          <h3 class="font-semibold tracking-tight">Top consumers</h3>
          <p class="text-xs text-muted mt-0.5">Live usage per account, split by artifact type</p>
        </div>
        <div class="ml-auto text-right">
          <p class="text-xs text-muted uppercase tracking-widest font-medium">Shown</p>
          <p class="text-sm font-semibold tabular-nums">{{ users.length }}</p>
        </div>
      </div>
    </template>

    <div
      v-if="users.length === 0"
      class="flex flex-col items-center justify-center gap-4 py-12 px-6 text-center"
    >
      <UIcon name="mdi:account-multiple" class="w-7 h-7 opacity-20" />
      <div class="space-y-1 max-w-xs">
        <p class="text-sm font-medium text-muted">No usage to rank yet</p>
        <p class="text-xs text-muted leading-relaxed">
          Accounts appear here once they store files, previews, or transcoded media.
        </p>
      </div>
    </div>

    <div v-else class="px-6 pb-6">
      <div class="relative h-72">
        <Bar :data="chartData" :options="chartOptions" />
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import {
  BarElement,
  type ChartData,
  Chart as ChartJS,
  type ChartOptions,
  CategoryScale,
  Legend,
  LinearScale,
  Title,
  Tooltip,
} from "chart.js";
import { computed } from "vue";
import { Bar } from "vue-chartjs";

import type { UserStorageRank } from "@/api/status";

import { useTheme } from "@/composables/useTheme";
import { formatBytes } from "@/utils/size.utils";

ChartJS.register(Title, Tooltip, Legend, BarElement, CategoryScale, LinearScale);

const props = defineProps<{
  users: UserStorageRank[];
}>();

const { isDark } = useTheme();

const gridColor = computed(() => (isDark.value ? "#374151" : "#e5e7eb"));
const tickColor = computed(() => (isDark.value ? "#9ca3af" : "#6b7280"));

const quotaLabel = (user: UserStorageRank): string => {
  if (user.quotaBytes <= 0) return "Unlimited quota";
  const percent = Math.round((user.usedBytes / user.quotaBytes) * 100);
  return `Quota ${formatBytes(user.quotaBytes)} (${percent}% used)`;
};

const chartData = computed((): ChartData<"bar"> => ({
  datasets: [
    {
      backgroundColor: "#5B7FA6",
      borderRadius: 3,
      data: props.users.map((u) => u.filesSize),
      label: "Files",
    },
    {
      backgroundColor: "#C9A84C",
      borderRadius: 3,
      data: props.users.map((u) => u.previewsSize),
      label: "Previews",
    },
    {
      backgroundColor: "#8E6B9E",
      borderRadius: 3,
      data: props.users.map((u) => u.transcodedSize),
      label: "Transcoded",
    },
  ],
  labels: props.users.map((u) => u.userName),
}));

const chartOptions = computed((): ChartOptions<"bar"> => ({
  indexAxis: "y",
  maintainAspectRatio: false,
  plugins: {
    legend: { display: true, labels: { color: tickColor.value } },
    tooltip: {
      callbacks: {
        footer: (items) => {
          const user = props.users[items[0]?.dataIndex ?? -1];
          if (!user) return "";
          return quotaLabel(user);
        },
        label: (context) => `  ${context.dataset.label}: ${formatBytes(context.parsed.x ?? 0)}`,
      },
      intersect: false,
      mode: "index" as const,
    },
  },
  responsive: true,
  scales: {
    x: {
      grid: { color: gridColor.value },
      stacked: true,
      ticks: {
        callback: (value) => formatBytes(Number(value)),
        color: tickColor.value,
        maxRotation: 0,
      },
    },
    y: {
      grid: { color: gridColor.value },
      stacked: true,
      ticks: { autoSkip: false, color: tickColor.value },
    },
  },
}));
</script>
