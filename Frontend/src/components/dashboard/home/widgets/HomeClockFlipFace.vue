<script setup lang="ts">
import { computed } from "vue";

import type { HomeClockReadout } from "@/utils/home-clock";

import HomeFlipDigit from "./HomeFlipDigit.vue";

const {
  clock,
  motionActive,
  faceOnly = false,
} = defineProps<{
  clock: HomeClockReadout;
  motionActive: boolean;
  faceOnly?: boolean;
}>();

const groups = computed(() => {
  const result = [
    { name: "hour", digits: Array.from(clock.hour) },
    { name: "minute", digits: Array.from(clock.minute) },
  ];

  if (clock.second) result.push({ name: "second", digits: Array.from(clock.second) });
  if (clock.period) result.push({ name: "period", digits: Array.from(clock.period) });

  return result;
});
</script>

<template>
  <div
    class="clock-flip-face"
    :class="{
      'clock-flip-face--face-only': faceOnly,
      'clock-flip-face--seconds': Boolean(clock.second),
      'clock-flip-face--period': Boolean(clock.period),
    }"
    aria-hidden="true"
  >
    <div class="clock-flip-groups text-gray-900 dark:text-gray-100">
      <template v-for="(group, groupIndex) in groups" :key="group.name">
        <span v-if="groupIndex && group.name !== 'period'" class="clock-flip-colon">:</span>

        <div
          class="clock-flip-group"
          :class="{ 'clock-flip-group--period': group.name === 'period' }"
          :data-clock-part="group.name"
        >
          <HomeFlipDigit
            v-for="(digit, digitIndex) in group.digits"
            :key="digitIndex"
            :digit="digit"
            :motion-active="motionActive"
            :class="{ 'clock-flip-period': group.name === 'period' }"
          />
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.clock-flip-face {
  --clock-flip-period-rows: 0;
  --clock-flip-period-columns: 0;
  display: flex;
  flex-direction: column;
  align-items: var(--clock-items);
  gap: 0.5rem;
}

.clock-flip-face--period {
  --clock-flip-period-rows: 1.1;
  --clock-flip-period-columns: 1.9;
}

.clock-flip-groups {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  font-size: max(
    1rem,
    min(
      30cqi,
      calc((100cqb - var(--clock-caption-reserve) - 2rem) / (2.4 + var(--clock-flip-period-rows)))
    )
  );
  line-height: 1;
  font-variant-numeric: tabular-nums;
}

.clock-flip-group {
  display: flex;
  gap: 0.5rem;
}

.clock-flip-group--period {
  align-self: center;
  flex-shrink: 0;
}

.clock-flip-card.clock-flip-period {
  flex: none;
  width: 0.8em;
}

.clock-flip-period :deep(.clock-flip-digit),
.clock-flip-period :deep(.clock-flip-leaf::before) {
  font-size: 0.8em;
}

.clock-flip-colon {
  font-size: 0.5em;
  line-height: 0.3;
}

@container clock-body (min-aspect-ratio: 9 / 5) {
  .clock-flip-groups {
    flex-direction: row;
    font-size: max(
      1rem,
      min(
        calc((100cqi - 2rem) / (3.7 + var(--clock-flip-period-columns))),
        calc((100cqb - var(--clock-caption-reserve) - 2rem) / 1.1)
      )
    );
  }

  .clock-flip-colon {
    line-height: 1;
  }
}

@container clock-body (max-height: 150px) {
  .clock-flip-groups {
    gap: 0.25rem;
  }

  .clock-flip-face {
    gap: 0.25rem;
  }
}

@container clock-body (max-height: 100px) {
  .clock-flip-face {
    flex-direction: row;
    align-items: center;
    gap: 0.5rem;
  }

  .clock-flip-groups {
    flex-direction: row;
    font-size: clamp(1rem, min(13cqi, calc(100cqb - var(--clock-caption-reserve))), 2rem);
  }

  .clock-flip-colon {
    line-height: 1;
  }
}

.clock-flip-face--face-only {
  flex-direction: column;
  justify-content: var(--clock-block-items);
  gap: 0.25rem;
  width: 100%;
  height: 100%;
}

.clock-flip-face--face-only .clock-flip-groups {
  flex-direction: column;
  gap: 0.5rem;
  width: 100%;
  font-size: max(1rem, min(48cqi, calc((100cqb - 2rem) / (2.4 + var(--clock-flip-period-rows)))));
}

.clock-flip-face--face-only .clock-flip-group {
  width: 100%;
}

.clock-flip-face--face-only .clock-flip-group--period {
  width: auto;
}

.clock-flip-face--face-only .clock-flip-card {
  flex: 1;
  width: 0;
}

.clock-flip-face--face-only .clock-flip-card.clock-flip-period {
  flex: none;
  width: 0.8em;
}

@container clock-body (min-aspect-ratio: 9 / 5) {
  .clock-flip-face--face-only .clock-flip-groups {
    flex-direction: row;
    font-size: max(
      1rem,
      min(
        calc((100cqi - 2rem) / (3.7 + var(--clock-flip-period-columns))),
        calc((100cqb - 1rem) / 1.1)
      )
    );
  }
}

.clock-flip-face--seconds .clock-flip-groups {
  gap: 0.15em;
  font-size: max(
    0.875rem,
    min(
      24cqi,
      calc((100cqb - var(--clock-caption-reserve) - 2rem) / (3.7 + var(--clock-flip-period-rows)))
    )
  );
}

.clock-flip-face--seconds .clock-flip-group {
  gap: 0.15em;
}

.clock-flip-face--seconds.clock-flip-face--face-only .clock-flip-groups {
  font-size: max(
    0.875rem,
    min(30cqi, calc((100cqb - 1.5rem) / (3.7 + var(--clock-flip-period-rows))))
  );
}

@container clock-body (min-aspect-ratio: 9 / 5) {
  .clock-flip-face--seconds .clock-flip-groups {
    font-size: max(
      0.75rem,
      min(
        calc((100cqi - 1rem) / (6.5 + var(--clock-flip-period-columns))),
        calc((100cqb - var(--clock-caption-reserve) - 2rem) / 1.1)
      )
    );
  }

  .clock-flip-face--seconds.clock-flip-face--face-only .clock-flip-groups {
    font-size: max(
      0.875rem,
      min(
        calc((100cqi - 2.5rem) / (5.8 + var(--clock-flip-period-columns))),
        calc((100cqb - 1rem) / 1.1)
      )
    );
  }
}

@container clock-body (max-height: 100px) {
  .clock-flip-face--seconds:not(.clock-flip-face--face-only) .clock-flip-groups {
    font-size: max(
      0.625rem,
      min(
        calc((100cqi - 2rem) / (6.5 + var(--clock-flip-period-columns))),
        calc((100cqb - var(--clock-caption-reserve) - 0.5rem) / 1.1)
      )
    );
  }
}
</style>
