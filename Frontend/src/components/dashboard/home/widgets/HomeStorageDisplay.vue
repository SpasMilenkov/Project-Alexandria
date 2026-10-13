<script setup lang="ts">
import { useResizeObserver } from "@vueuse/core";
import { computed, ref } from "vue";

import type { StorageBreakdown } from "@/api/status";

import { HomeStorageStyle } from "@/enums/home-widget";
import { homeStorageCells, homeStorageModel } from "@/utils/home-file-widgets";
import { formatBytes } from "@/utils/size.utils";

import HomeStorageLegend from "./HomeStorageLegend.vue";
import HomeStorageTank from "./HomeStorageTank.vue";

const {
  data,
  storageStyle,
  animated = true,
} = defineProps<{
  data: StorageBreakdown;
  storageStyle: HomeStorageStyle;
  animated?: boolean;
}>();

const model = computed(() => homeStorageModel(data));

const cells = computed(() => homeStorageCells(model.value.categories));

const legend = ref<InstanceType<typeof HomeStorageLegend> | null>(null);

const legendCapacity = ref(0);

useResizeObserver(
  () => legend.value?.element,
  (entries) => {
    const bounds = entries[0]?.contentRect;
    if (!bounds || !legend.value?.element) return;

    const style = getComputedStyle(legend.value.element);
    const columns = style.display === "grid" ? 2 : 1;
    const rowHeight = Number.parseFloat(style.fontSize) * 2;

    legendCapacity.value = Math.floor(bounds.height / rowHeight) * columns;
  },
);

const rows = ref<HTMLElement | null>(null);

const capacity = ref(0);

useResizeObserver(rows, (entries) => {
  const bounds = entries[0]?.contentRect;

  if (!bounds || !rows.value) return;

  const rowHeight = Number.parseFloat(getComputedStyle(rows.value).fontSize) * (10 / 3);

  capacity.value = Math.floor(bounds.height / rowHeight);
});

const ranked = computed(() =>
  [...model.value.categories].sort((first, second) => second.bytes - first.bytes),
);

const visibleLegend = computed(() => ranked.value.slice(0, legendCapacity.value));

const visibleRows = computed(() => ranked.value.slice(0, capacity.value));

const maximum = computed(() => Math.max(1, ...ranked.value.map((category) => category.bytes)));

const headline = computed(() =>
  model.value.unlimited ? model.value.used : `${model.value.percent}%`,
);

const circleStyle = computed(
  () => storageStyle === HomeStorageStyle.Ring || storageStyle === HomeStorageStyle.Waffle,
);

const cellSize = computed(() =>
  formatBytes((model.value.unlimited ? data.usedBytes : data.quotaBytes) / 100),
);
</script>

<template>
  <div class="storage-body text-gray-900 dark:text-gray-100" :data-storage-style="storageStyle">
    <div v-if="storageStyle === HomeStorageStyle.Meter" class="storage-content meter">
      <div class="storage-summary">
        <p class="storage-value">{{ model.used }}</p>
        <p class="storage-secondary">
          {{ model.unlimited ? "used · Unlimited" : `of ${model.quota} used` }}
        </p>
      </div>

      <div class="meter-details">
        <div class="usage-bar" aria-hidden="true">
          <span
            v-for="category in model.categories"
            :key="category.label"
            :style="{ width: `${category.share}%`, backgroundColor: category.color }"
          />
        </div>

        <p class="storage-secondary">{{ model.remaining }}</p>
      </div>

      <HomeStorageLegend ref="legend" class="storage-legend" :categories="visibleLegend" />
    </div>

    <div v-else-if="circleStyle" class="storage-content circle-layout">
      <div class="storage-dial">
        <svg v-if="storageStyle === HomeStorageStyle.Ring" viewBox="0 0 200 200" aria-hidden="true">
          <circle cx="100" cy="100" r="80" fill="none" class="ring-track" stroke-width="18" />
          <circle
            v-for="category in model.categories"
            :key="category.label"
            cx="100"
            cy="100"
            r="80"
            fill="none"
            :stroke="category.color"
            stroke-width="18"
            pathLength="100"
            :stroke-dasharray="`${Math.max(0, category.share - 0.5)} ${100 - Math.max(0, category.share - 0.5)}`"
            :stroke-dashoffset="-category.offset"
            transform="rotate(-90 100 100)"
          />
          <text
            x="100"
            y="96"
            text-anchor="middle"
            dominant-baseline="central"
            :font-size="model.unlimited ? 24 : 36"
            fill="currentColor"
            font-weight="700"
          >
            {{ headline }}
          </text>
          <text x="100" y="125" text-anchor="middle" font-size="14" class="ring-caption">used</text>
        </svg>

        <div v-else class="waffle" aria-hidden="true">
          <i
            v-for="(color, index) in cells"
            :key="index"
            :style="{ backgroundColor: color ?? undefined }"
          />
        </div>
      </div>

      <p class="dial-caption storage-secondary">{{ model.used }} used</p>

      <div class="circle-details">
        <div class="storage-summary">
          <p class="storage-value">{{ model.used }}</p>
          <p class="storage-secondary">{{ model.remaining }}</p>
        </div>

        <HomeStorageLegend ref="legend" class="storage-legend" :categories="visibleLegend" />

        <p v-if="storageStyle === HomeStorageStyle.Waffle" class="cell-caption storage-secondary">
          1 square = {{ cellSize }}
        </p>
      </div>

      <span class="sr-only">{{ model.used }} used. {{ model.remaining }}.</span>
    </div>

    <div v-else-if="storageStyle === HomeStorageStyle.Breakdown" class="storage-content breakdown">
      <div class="storage-summary">
        <p class="storage-value">{{ model.used }}</p>
        <p class="storage-secondary">{{ model.remaining }}</p>
      </div>

      <div ref="rows" class="breakdown-rows">
        <div v-for="category in visibleRows" :key="category.label" class="breakdown-row">
          <div class="legend-row">
            <span>{{ category.label }}</span>
            <b>{{ formatBytes(category.bytes) }}</b>
          </div>

          <div class="usage-bar thin" aria-hidden="true">
            <span
              :style="{
                width: `${(category.bytes / maximum) * 100}%`,
                backgroundColor: category.color,
              }"
            />
          </div>
        </div>

        <p v-if="!model.categories.length" class="storage-secondary">No storage used yet</p>
      </div>
    </div>

    <div
      v-else
      class="storage-content tank-layout"
      :class="{
        'near-limit': (model.percent ?? 0) >= 85,
        'almost-limit': (model.percent ?? 0) >= 95,
      }"
    >
      <HomeStorageTank :fill="model.fill" :animated="animated" />

      <div class="tank-details">
        <p class="storage-value">{{ model.unlimited ? model.used : headline }}</p>
        <p class="storage-secondary">
          {{ model.unlimited ? "Unlimited storage" : `${model.used} of ${model.quota}` }}
        </p>
        <p class="storage-secondary">{{ model.remaining }}</p>
        <span v-if="(model.percent ?? 0) >= 85" class="limit-badge">Nearly full</span>

        <HomeStorageLegend ref="legend" class="storage-legend" :categories="visibleLegend" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.storage-body {
  position: relative;
  flex: 1;
  min-width: 0;
  min-height: 0;
  container: storage-body / size;
}

.storage-content {
  --storage-label-size: 12px;
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 8px;
  overflow: hidden;
}

.storage-value {
  font-size: clamp(16px, min(14cqi, 30cqb), 36px);
  font-weight: 700;
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.storage-secondary,
.legend-row {
  font-size: var(--storage-label-size);
  color: var(--ui-text-muted);
}

.storage-summary,
.meter-details {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0;
  flex-shrink: 0;
}

.usage-bar {
  display: flex;
  flex-shrink: 0;
  height: 12px;
  overflow: hidden;
  border-radius: 8px;
  background: var(--ui-bg-elevated);
}

.usage-bar > span {
  flex-shrink: 0;
}
.usage-bar.thin {
  height: 6px;
}

.storage-legend {
  font-size: var(--storage-label-size);
  display: none;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.legend-row {
  display: flex;
  align-items: center;
  gap: 8px;
  height: 24px;
  flex-shrink: 0;
}

.legend-row span {
  flex: 1;
  min-width: 0;
}
.legend-row b {
  color: var(--ui-text);
  font-weight: 500;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.meter,
.breakdown {
  justify-content: flex-start;
  gap: 16px;
}
.meter-details {
  gap: 8px;
}
.circle-layout {
  --reserved: 0px;
  align-items: center;
}
.storage-dial {
  flex-shrink: 0;
  width: min(100cqi, 100cqb - var(--reserved));
  aspect-ratio: 1;
}
.storage-dial svg {
  display: block;
  width: 100%;
  height: 100%;
}
.ring-track {
  stroke: var(--ui-bg-elevated);
}
.ring-caption {
  fill: var(--ui-text-muted);
}
.waffle {
  display: grid;
  width: 100%;
  height: 100%;
  grid-template-columns: repeat(10, 1fr);
  grid-template-rows: repeat(10, 1fr);
  gap: 2px;
}
.waffle i {
  border-radius: 2px;
  background: var(--ui-bg-elevated);
}
.dial-caption,
.circle-details {
  display: none;
}
.circle-details {
  flex-direction: column;
  gap: 16px;
  min-width: 0;
  min-height: 0;
  flex: 1;
}
.breakdown-rows {
  font-size: var(--storage-label-size);
  flex: 1;
  min-height: 0;
  overflow: hidden;
}
.breakdown-row {
  height: calc(10em / 3);
}

.breakdown-row .legend-row {
  height: 2em;
}

.tank-layout {
  --liquid: #59979c;
  flex-direction: row;
  align-items: center;
  justify-content: flex-start;
  gap: 16px;
}
.tank-layout.near-limit {
  --liquid: #b89442;
}

.tank-layout.almost-limit {
  --liquid: #b66f70;
}
.tank {
  position: relative;
  flex-shrink: 0;
  height: min(100cqb, 90cqi);
  aspect-ratio: 1 / 2;
  overflow: hidden;
  border: 2px solid var(--ui-border);
  border-radius: 16px;
  background: var(--ui-bg-elevated);
}
.tank-details {
  display: flex;
  flex: 1;
  min-width: 0;
  min-height: 0;
  flex-direction: column;
  gap: 8px;
}
.limit-badge {
  align-self: flex-start;
  border-radius: 8px;
  padding: 0 8px;
  font-size: 12px;
  background: var(--ui-bg-elevated);
  color: var(--ui-text);
}

@container storage-body (min-height: 200px) {
  .meter .storage-legend {
    display: flex;
    flex-direction: column;
  }
}

@container storage-body (min-width: 420px) and (min-height: 200px) {
  .meter .storage-legend {
    display: grid;
    grid-template-columns: 1fr 1fr;
    column-gap: 24px;
    grid-auto-rows: 2em;
    align-content: start;
  }
}

@container storage-body (min-aspect-ratio: 2 / 1) {
  .meter,
  .breakdown {
    flex-direction: row;
    align-items: center;
    gap: 24px;
  }
  .meter-details,
  .breakdown-rows {
    flex: 1;
  }
  .meter .storage-legend {
    display: none;
  }
  .breakdown-rows {
    align-self: stretch;
  }
}

@container storage-body (min-height: 200px) {
  .circle-layout {
    --reserved: 28px;
  }
  .dial-caption {
    display: block;
  }
}

@container storage-body (min-height: 300px) {
  .circle-layout {
    --reserved: 200px;
  }
  .circle-details {
    display: flex;
    width: 100%;
  }
  .circle-details .storage-summary,
  .cell-caption {
    display: none;
  }
  .circle-details .storage-legend {
    display: flex;
    flex-direction: column;
  }
}

@container storage-body (min-aspect-ratio: 3 / 2) {
  .circle-layout {
    flex-direction: row;
    gap: 24px;
    --reserved: 0px;
  }
  .storage-dial {
    width: min(100cqb, 45cqi);
  }
  .circle-details {
    display: flex;
    flex: 1;
    align-self: stretch;
    justify-content: center;
  }
  .circle-details .storage-summary,
  .cell-caption {
    display: block;
  }
  .circle-details .storage-legend {
    display: flex;
    flex-direction: column;
  }
  .dial-caption {
    display: none;
  }
}

@container storage-body (max-aspect-ratio: 3 / 4) {
  .tank-layout {
    flex-direction: column;
    justify-content: center;
    gap: 8px;
  }
  .tank {
    height: min(100cqb - 100px, 120cqi);
  }
  .tank-details {
    flex: 0;
    align-items: center;
    text-align: center;
  }
}

@container storage-body (min-width: 420px) and (min-height: 260px) {
  .storage-content {
    --storage-label-size: clamp(12px, min(2cqi, 4cqb), 20px);
  }

  .storage-value {
    font-size: clamp(36px, min(9cqi, 14cqb), 64px);
  }

  .tank-details {
    align-self: stretch;
    justify-content: center;
  }

  .tank-details .storage-legend {
    display: flex;
    flex-direction: column;
  }
}
</style>
