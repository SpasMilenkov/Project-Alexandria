<template>
  <IntegrationPanel title="Top consumers" hint="Usage per account, split by artifact type">
    <ChartFrame
      height="lg"
      :loading="loading"
      :empty="users.length === 0"
      empty-title="No usage to rank yet"
      empty-text="Accounts appear here once they store files, previews, or transcoded media."
    >
      <Bar :data="chartData" :options="chartOptions" />
    </ChartFrame>
  </IntegrationPanel>
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

import ChartFrame from "@/components/dashboard/integrations/ChartFrame.vue";
import IntegrationPanel from "@/components/dashboard/integrations/IntegrationPanel.vue";
import { useTheme } from "@/composables/useTheme";
import { formatBytes } from "@/utils/size";

ChartJS.register(Title, Tooltip, Legend, BarElement, CategoryScale, LinearScale);

const { users, loading = false } = defineProps<{
  users: UserStorageRank[];
  loading?: boolean;
}>();

const { isDark } = useTheme();

const gridColor = computed(() =>
  isDark.value ? "rgba(255, 255, 255, 0.08)" : "rgba(0, 0, 0, 0.08)",
);
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
      data: users.map((u) => u.filesSize),
      label: "Files",
    },
    {
      backgroundColor: "#C9A84C",
      borderRadius: 3,
      data: users.map((u) => u.previewsSize),
      label: "Previews",
    },
    {
      backgroundColor: "#8E6B9E",
      borderRadius: 3,
      data: users.map((u) => u.transcodedSize),
      label: "Transcoded",
    },
  ],
  labels: users.map((u) => u.userName),
}));

const chartOptions = computed((): ChartOptions<"bar"> => ({
  indexAxis: "y",
  maintainAspectRatio: false,
  plugins: {
    legend: { display: true, labels: { color: tickColor.value } },
    tooltip: {
      callbacks: {
        footer: (items) => {
          const user = users[items[0]?.dataIndex ?? -1];
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
