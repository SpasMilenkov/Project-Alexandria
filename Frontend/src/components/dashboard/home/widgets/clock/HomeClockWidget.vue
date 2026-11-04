<script setup lang="ts">
import { computed, ref } from "vue";

import type { HomeWidget } from "@/types/home";

import {
  HomeClockStyle,
  HomeHorizontalAlignment,
  HomeVerticalAlignment,
} from "@/enums/home-widget";
import { homeClockDateLabels, homeClockReadout, useHomeClockTiming } from "@/utils/home-clock";
import { homeClockLayout } from "@/utils/home-clock-layout";
import { homeTimeZoneLabel, resolveHomeTimeZone } from "@/utils/home-time-zones";
import { isHomeClockFaceOnly } from "@/utils/home-widget-options";

import HomeClockFlipFace from "./HomeClockFlipFace.vue";
import HomeClockRingsFace from "./HomeClockRingsFace.vue";
import HomeClockWatchFace from "./HomeClockWatchFace.vue";
import HomeClockWordsFace from "./HomeClockWordsFace.vue";

const { widget } = defineProps<{ widget: HomeWidget }>();

const faceOnly = computed(() => isHomeClockFaceOnly(widget));

const element = ref<HTMLElement | null>(null);
const timing = useHomeClockTiming(
  element,
  () =>
    widget.options.clockStyle === HomeClockStyle.Analog ||
    widget.options.clockStyle === HomeClockStyle.Rings,
);
const { timestamp, motionActive } = timing;

const minuteTimestamp = computed(() => Math.floor(timestamp.value / 60_000) * 60_000);

const readoutTimestamp = computed(() =>
  widget.options.showSeconds ? timestamp.value : minuteTimestamp.value,
);

const zone = computed(() => resolveHomeTimeZone(widget.options.timeZone));

const clock = computed(() =>
  homeClockReadout(
    readoutTimestamp.value,
    zone.value.timeZone,
    widget.options.hourFormat,
    undefined,
    widget.options.showSeconds,
  ),
);

const captions = computed(() => {
  if (faceOnly.value) return null;

  return homeClockDateLabels(minuteTimestamp.value, zone.value.timeZone);
});

const zoneLabel = computed(() => {
  const label = homeTimeZoneLabel(widget.options.timeZone);

  if (zone.value.unavailable) return `${label} unavailable. Showing device time.`;

  return label;
});

const horizontalStyles = {
  [HomeHorizontalAlignment.Left]: {
    justifyItems: "start",
    "--clock-items": "flex-start",
  },
  [HomeHorizontalAlignment.Center]: {
    justifyItems: "center",
    "--clock-items": "center",
  },
  [HomeHorizontalAlignment.Right]: {
    justifyItems: "end",
    "--clock-items": "flex-end",
  },
};

const verticalStyles = {
  [HomeVerticalAlignment.Top]: "start",
  [HomeVerticalAlignment.Middle]: "center",
  [HomeVerticalAlignment.Bottom]: "end",
};

const textStyles = {
  [HomeHorizontalAlignment.Left]: "left",
  [HomeHorizontalAlignment.Center]: "center",
  [HomeHorizontalAlignment.Right]: "right",
};

const layout = computed(() => homeClockLayout(widget.options, faceOnly.value));

const alignment = computed(() => ({
  ...horizontalStyles[widget.options.horizontalAlignment],
  alignItems: verticalStyles[widget.options.verticalAlignment],
  "--clock-face-text": textStyles[widget.options.horizontalAlignment],
  "--clock-text": textStyles[widget.options.textAlignment],
  "--clock-block-items": verticalStyles[widget.options.verticalAlignment],
  "--clock-text-items": horizontalStyles[widget.options.textAlignment].justifyItems,
  "--clock-text-block-items": verticalStyles[widget.options.textVerticalAlignment],
  "--clock-face-area": layout.value.faceArea,
  "--clock-text-area": layout.value.textArea,
}));
</script>

<template>
  <div
    ref="element"
    class="clock-alignment min-h-0 min-w-0 flex-1"
    :style="alignment"
    :data-motion-active="motionActive"
    :data-face-only="faceOnly"
    :title="faceOnly ? zoneLabel : undefined"
  >
    <div
      class="clock-layout"
      :class="{
        'clock-layout--shared': layout.shared,
        'clock-layout--face-only': faceOnly,
        'clock-layout--rings': widget.options.clockStyle === HomeClockStyle.Rings,
      }"
      :style="{
        gridTemplateRows: layout.gridTemplateRows,
        gridTemplateColumns: layout.gridTemplateColumns,
      }"
    >
      <div class="clock-face-slot">
        <div
          class="clock-content flex min-w-0 flex-col gap-2"
          :class="{
            'clock-content--face-only': faceOnly,
            'clock-content--seconds': Boolean(clock.second),
          }"
        >
          <p class="clock-time sr-only">{{ clock.time }}</p>

          <p
            v-if="!faceOnly && widget.options.clockStyle === HomeClockStyle.Minimal"
            class="clock-greeting text-xs font-semibold text-gray-600 dark:text-gray-400"
          >
            {{ clock.greeting }}
          </p>

          <div
            v-if="widget.options.clockStyle === HomeClockStyle.Minimal"
            class="clock-quiet-time font-semibold tracking-tight text-gray-900 tabular-nums dark:text-gray-100"
            aria-hidden="true"
          >
            <span>{{ clock.hour }}</span
            ><span class="clock-colon">:</span><span>{{ clock.minute }}</span
            ><span v-if="clock.second" class="clock-colon">:</span
            ><span v-if="clock.second">{{ clock.second }}</span>
            <span v-if="clock.period" class="clock-period text-gray-600 dark:text-gray-400">{{
              clock.period
            }}</span>
          </div>

          <HomeClockWatchFace
            v-else-if="widget.options.clockStyle === HomeClockStyle.Analog"
            :timing="timing"
            :time-zone="zone.timeZone"
            :face-only="faceOnly"
          />

          <div
            v-else-if="widget.options.clockStyle === HomeClockStyle.Material"
            class="clock-material-time flex shrink-0 flex-col items-center rounded-3xl bg-gray-100 p-4 font-bold tracking-tight text-gray-900 tabular-nums dark:bg-gray-800 dark:text-gray-100"
            aria-hidden="true"
          >
            <span>{{ clock.hour }}</span>
            <span class="clock-material-dot bg-gray-500 dark:bg-gray-400"></span>
            <span class="clock-material-minute text-gray-600 dark:text-gray-400">{{
              clock.minute
            }}</span>
            <template v-if="clock.second">
              <span class="clock-material-dot bg-gray-500 dark:bg-gray-400"></span>
              <span class="clock-material-second text-gray-600 dark:text-gray-400">{{
                clock.second
              }}</span>
            </template>
            <span v-if="clock.period" class="clock-period text-gray-600 dark:text-gray-400">{{
              clock.period
            }}</span>
          </div>

          <HomeClockFlipFace
            v-else-if="widget.options.clockStyle === HomeClockStyle.Flip"
            :clock="clock"
            :motion-active="motionActive"
            :face-only="faceOnly"
          />

          <HomeClockRingsFace
            v-else-if="widget.options.clockStyle === HomeClockStyle.Rings"
            :clock="clock"
            :timing="timing"
            :time-zone="zone.timeZone"
            :face-only="faceOnly"
          />

          <HomeClockWordsFace
            v-else-if="widget.options.clockStyle === HomeClockStyle.Words"
            :clock="clock"
            :face-only="faceOnly"
          />
        </div>
      </div>

      <p v-if="faceOnly && zone.unavailable" class="sr-only" role="status">{{ zoneLabel }}</p>

      <div v-if="!faceOnly" class="clock-details min-w-0">
        <p
          v-if="widget.options.clockStyle === HomeClockStyle.Analog"
          class="clock-greeting mb-2 text-xs font-semibold text-gray-600 dark:text-gray-400"
        >
          {{ clock.greeting }}
        </p>

        <p class="clock-date text-sm text-gray-600 dark:text-gray-400">{{ captions?.date }}</p>
        <p class="clock-compact-date text-xs text-gray-600 dark:text-gray-400">
          {{ captions?.compactDate }}
        </p>

        <p
          class="clock-zone mt-2 text-xs text-gray-500 dark:text-gray-500"
          :class="{ 'clock-zone-fallback': zone.unavailable }"
          :role="zone.unavailable ? 'status' : undefined"
          :title="zone.unavailable ? zoneLabel : undefined"
        >
          <template v-if="zone.unavailable">
            <span class="clock-zone-name"
              >{{ homeTimeZoneLabel(widget.options.timeZone) }} unavailable. </span
            ><span>Showing device time.</span>
          </template>
          <template v-else>{{ zoneLabel }}</template>
        </p>
      </div>
    </div>
  </div>
</template>

<style scoped>
.clock-alignment {
  container: clock-body / size;
}

.clock-layout {
  display: grid;
  gap: 0.5rem 1rem;
  width: 100%;
  height: 100%;
}

.clock-layout--rings {
  font-size: clamp(0.75rem, min(3cqi, 6cqb), 1.25rem);
}

.clock-face-slot {
  grid-area: var(--clock-face-area);
  container: clock-body / size;
  min-width: 0;
  min-height: 0;
}

.clock-content {
  --clock-caption-reserve: 0rem;
  width: 100%;
  height: 100%;
  max-width: 100%;
  justify-content: var(--clock-block-items);
  align-items: var(--clock-items);
}

.clock-content--seconds {
  --clock-digital-scale: 0.65;
}

.clock-details {
  grid-area: var(--clock-text-area);
  justify-self: var(--clock-text-items);
  align-self: var(--clock-text-block-items);
  width: fit-content;
  max-width: min(100%, 16rem);
  text-align: var(--clock-text);
  overflow-wrap: anywhere;
}

.clock-layout--rings .clock-details {
  max-width: min(100%, 21em);
}

.clock-layout--rings .clock-date {
  font-size: max(0.875rem, 1em);
}

.clock-layout--rings .clock-zone,
.clock-layout--rings .clock-compact-date {
  font-size: inherit;
  line-height: 1.5;
}

.clock-layout--shared {
  display: flex;
  flex-direction: column;
  justify-content: var(--clock-block-items);
  align-items: var(--clock-items);
  gap: 0.5rem;
}

.clock-layout--shared .clock-face-slot {
  container: none;
  width: 100%;
}

.clock-layout--shared .clock-content {
  --clock-caption-reserve: 5rem;
  height: auto;
}

.clock-layout--shared .clock-details {
  align-self: var(--clock-text-items);
}

.clock-quiet-time {
  font-size: calc(max(1rem, min(19cqi, 40cqb)) * var(--clock-digital-scale, 1));
  line-height: 1;
  white-space: nowrap;
}

.clock-period {
  font-size: max(0.3em, 0.625rem);
  font-weight: 600;
  letter-spacing: normal;
}

.clock-quiet-time .clock-period {
  margin-left: 0.25em;
}

.clock-dial {
  width: max(2rem, min(100cqi, calc(100cqb - var(--clock-caption-reserve))));
  max-width: 100%;
  aspect-ratio: 1;
}

.clock-material-time {
  width: min(100%, max(8rem, 70cqb));
  gap: 0.08em;
  font-size: calc(
    max(1rem, min(26cqi, calc((100cqb - var(--clock-caption-reserve) - 2rem) / 3))) *
      var(--clock-digital-scale, 1)
  );
  line-height: 1;
}

.clock-material-dot {
  width: 0.2em;
  height: 0.2em;
  border-radius: 50%;
}

.clock-material-time .clock-period {
  margin-top: 0.3em;
}

.clock-compact-date {
  display: none;
}

@container clock-body (max-width: 220px) {
  .clock-date,
  .clock-zone {
    display: none;
  }

  .clock-compact-date {
    display: block;
  }
}

@container clock-body (max-height: 150px) {
  .clock-greeting,
  .clock-zone {
    display: none;
  }

  .clock-material-time {
    flex-direction: row;
    gap: 0.12em;
    border-radius: 1rem;
    padding: 0.5rem;
    font-size: calc(max(1rem, min(17cqi, calc(100cqb - 1.5rem))) * var(--clock-digital-scale, 1));
  }

  .clock-material-time .clock-period {
    margin-top: 0;
    margin-left: 0.3em;
  }
}

@container clock-body (min-aspect-ratio: 2 / 1) {
  .clock-quiet-time {
    font-size: calc(max(1rem, min(16cqi, 50cqb)) * var(--clock-digital-scale, 1));
  }

  .clock-material-time {
    width: auto;
    flex-direction: row;
    gap: 0.12em;
    border-radius: 1rem;
    padding: 0.5rem 1rem;
    font-size: calc(max(1rem, min(13cqi, calc(100cqb - 2rem))) * var(--clock-digital-scale, 1));
  }

  .clock-material-time .clock-period {
    margin-top: 0;
    margin-left: 0.3em;
  }
}

.clock-zone.clock-zone-fallback {
  display: block;
}

@container clock-body (max-height: 150px) {
  .clock-details:has(.clock-zone-fallback)
    :is(.clock-date, .clock-compact-date, .clock-greeting, .clock-zone-name) {
    display: none;
  }
}

.clock-layout--face-only {
  gap: 0;
}

.clock-content.clock-content--face-only {
  --clock-caption-reserve: 0rem;
  gap: 0;
  min-height: 0;
}

.clock-content--face-only .clock-quiet-time {
  font-size: calc(max(1rem, min(28cqi, 90cqb)) * var(--clock-digital-scale, 1));
}

.clock-content--face-only .clock-dial {
  width: min(100cqi, 100cqb);
}

.clock-content--face-only .clock-material-time {
  justify-content: center;
  flex-direction: column;
  gap: 0.08em;
  width: 100%;
  height: 100%;
  padding: 0.5rem;
  font-size: calc(
    max(1rem, min(44cqi, calc((100cqb - 1rem) / 2.9))) * var(--clock-digital-scale, 1)
  );
}

.clock-content--face-only .clock-material-time .clock-period {
  margin-top: 0.3em;
  margin-left: 0;
}

@container clock-body (min-aspect-ratio: 9 / 5) {
  .clock-content--face-only .clock-material-time {
    flex-direction: row;
    gap: 0.12em;
    font-size: calc(max(1rem, min(27cqi, calc(100cqb - 1rem))) * var(--clock-digital-scale, 1));
  }

  .clock-content--face-only .clock-material-time .clock-period {
    margin-top: 0;
    margin-left: 0.3em;
  }
}
</style>
