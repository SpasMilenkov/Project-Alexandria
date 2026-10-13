<script setup lang="ts">
import { useTimestamp } from "@vueuse/core";
import { computed } from "vue";
import type { HomeWidget } from "@/types/home";
import { homeTimeZoneLabel, resolveHomeTimeZone } from "@/utils/home-time-zones";

const { widget } = defineProps<{ widget: HomeWidget }>();

const timestamp = useTimestamp({ interval: 1000 });

const zone = computed(() => resolveHomeTimeZone(widget.options.timeZone));

const time = computed(() =>
  new Date(timestamp.value).toLocaleTimeString(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: zone.value.timeZone,
  }),
);

const date = computed(() =>
  new Date(timestamp.value).toLocaleDateString(undefined, {
    weekday: "long",
    month: "long",
    day: "numeric",
    timeZone: zone.value.timeZone,
  }),
);

const compactDate = computed(() =>
  new Date(timestamp.value).toLocaleDateString(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    timeZone: zone.value.timeZone,
  }),
);

const zoneLabel = computed(() => {
  const label = homeTimeZoneLabel(widget.options.timeZone);

  if (zone.value.unavailable) return `${label} unavailable. Showing device time.`;

  return label;
});
</script>

<template>
  <div class="clock-content flex min-w-0 flex-1 flex-col justify-center gap-2">
    <p
      class="clock-time font-semibold tracking-tight whitespace-nowrap text-gray-900 tabular-nums dark:text-gray-100"
    >
      {{ time }}
    </p>

    <div class="min-w-0">
      <p class="clock-date text-sm text-gray-600 dark:text-gray-400">{{ date }}</p>
      <p class="clock-compact-date text-xs text-gray-600 dark:text-gray-400">{{ compactDate }}</p>

      <p
        class="clock-zone mt-2 text-xs text-gray-500 dark:text-gray-500"
        :class="{ 'clock-zone-fallback': zone.unavailable }"
        :role="zone.unavailable ? 'status' : undefined"
      >
        {{ zoneLabel }}
      </p>
    </div>
  </div>
</template>

<style scoped>
.clock-time {
  font-size: clamp(1rem, 10cqi, 3rem);
  line-height: 1.15;
}

.clock-compact-date {
  display: none;
}

@container home-widget (max-width: 240px) {
  .clock-time {
    white-space: normal;
  }

  .clock-date,
  .clock-zone {
    display: none;
  }

  .clock-compact-date {
    display: block;
  }
}

@container home-widget (max-height: 224px) {
  .clock-zone {
    display: none;
  }
}

@container home-widget (min-width: 400px) and (max-height: 224px) {
  .clock-content {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
  }

  .clock-time {
    flex-shrink: 0;
  }
}

@container home-widget (min-width: 400px) and (min-height: 400px) {
  .clock-time {
    font-size: clamp(2rem, 12cqi, 4.5rem);
  }
}

.clock-zone.clock-zone-fallback {
  display: block;
}
</style>
