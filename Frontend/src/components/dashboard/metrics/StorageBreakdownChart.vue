<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="p-2 rounded-lg border">
            <UIcon name="mdi:chart-donut" class="w-5 h-5" />
          </div>
          <div>
            <h3 class="font-semibold">Storage Breakdown</h3>
            <p class="text-sm opacity-70">Storage usage by file type</p>
          </div>
        </div>
      </div>
    </template>

      <!-- Chart -->
      <div class="flex items-center justify-center p-4 max-h-120">
        <Doughnut
          id="storage-breakdown"
          :options="chartOptions"
          :data="chartData"
          class=" max-w-xl"
        />
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
  colors: string[];
  formattedSize: string[];
}>();

const colorMode = useColorMode();

const isDark = computed(() => colorMode.value === "dark");

const chartData = computed(
  (): ChartData<"doughnut"> => ({
    labels: props.labels,
    datasets: [
      {
        data: props.data,
        tooltip: {},
        backgroundColor: props.colors,
      },
    ],
  }),
);

const chartOptions = computed(
  (): ChartOptions<"doughnut"> => ({
    responsive: true,
    plugins: {
      legend: { labels: { color: isDark.value ? "white" : "black" } },
      tooltip: {
        callbacks: {
          label: (context) => {
            const index = context.dataIndex;

            const label = props.labels[index];
            const formatted = props.formattedSize[index];

            return `${label}: ${formatted}`;
          },
        },
      },
    },
  }),
);
</script>
<style lang="css" scoped></style>
