<template>
  <figure class="p-6">
    <div class="max-w-[720px]" role="img" :aria-label="ariaLabel">
      <svg
        :viewBox="`0 0 ${geometry.width} ${geometry.height}`"
        class="return-svg block h-auto w-full"
        stroke-linecap="round"
        stroke-linejoin="round"
        :style="{ '--return-duration': `${REVEAL_SECONDS}s`, '--return-delay': rippleDelay }"
        aria-hidden="true"
      >
        <!-- Last play: solid line into a small beat -->
        <path
          :d="geometry.leadPath"
          fill="none"
          stroke="var(--wrapped-rose)"
          stroke-width="3"
          vector-effect="non-scaling-stroke"
        />

        <path :d="diamond(geometry.lastApex, 4)" fill="var(--wrapped-rose)" />

        <!-- The quiet stretch: no signal, only dashes -->
        <path
          :d="geometry.gapPath"
          fill="none"
          stroke="var(--wrapped-line)"
          stroke-width="3"
          stroke-dasharray="2 6"
          vector-effect="non-scaling-stroke"
        />

        <path
          :d="geometry.bracketPath"
          fill="none"
          stroke="var(--wrapped-line)"
          stroke-width="1.5"
          vector-effect="non-scaling-stroke"
        />

        <!-- The return: big beat, then one settling echo -->
        <path :d="diamond(geometry.apex, 14)" fill="var(--wrapped-pin)" opacity="0.16" />

        <rect
          v-for="n in 2"
          :key="n"
          :x="geometry.apex.x - 18"
          :y="geometry.apex.y - 18"
          width="36"
          height="36"
          fill="none"
          stroke="var(--wrapped-pin)"
          stroke-width="2"
          vector-effect="non-scaling-stroke"
          :class="['return-ripple', n === 2 && 'return-ripple--late']"
        />

        <path
          :d="geometry.returnPath"
          fill="none"
          stroke="var(--wrapped-rose)"
          stroke-width="3"
          vector-effect="non-scaling-stroke"
        />

        <path class="return-apex" :d="diamond(geometry.apex, 6)" fill="var(--wrapped-pin)" />
      </svg>

      <!-- HTML label so the number stays readable on narrow screens -->
      <div class="relative h-10" aria-hidden="true">
        <p
          class="absolute top-2 -translate-x-1/2 whitespace-nowrap"
          :style="{ left: `${geometry.labelLeft * 100}%` }"
        >
          <span
            class="text-2xl font-black tracking-tight text-gray-900 tabular-nums dark:text-gray-100"
            >{{ countLabel }}</span
          >
          <span class="ml-2 text-sm text-gray-600 dark:text-gray-400">{{ unit }} away</span>
        </p>
      </div>
    </div>

    <figcaption
      class="flex max-w-[720px] flex-wrap justify-between gap-x-4 gap-y-2 text-xs text-gray-600 dark:text-gray-400"
    >
      <span>Last played {{ dateLabel(from) }}</span>
      <span>Back on {{ dateLabel(to) }}</span>
    </figcaption>
  </figure>
</template>

<script setup lang="ts">
import { computed } from "vue";

import { dateLabel, returnGeometry } from "@/utils/wrapped-story.utils";

const props = defineProps<{ from?: string | null; to?: string | null; count: number }>();

// The line draws left to right; the ripple fires when the draw reaches the return beat.
const REVEAL_SECONDS = 1.6;

const geometry = computed(() => returnGeometry(props.from, props.to));

const rippleDelay = computed(
  () => `${((geometry.value.apex.x / geometry.value.width) * REVEAL_SECONDS).toFixed(2)}s`,
);

const unit = computed(() => (props.count === 1 ? "day" : "days"));

const countLabel = computed(() => new Intl.NumberFormat().format(props.count));

const ariaLabel = computed(() => `${countLabel.value} ${unit.value} between recorded plays.`);

const diamond = ({ x, y }: { x: number; y: number }, r: number) =>
  `M ${x} ${y - r} L ${x + r} ${y} L ${x} ${y + r} L ${x - r} ${y} Z`;
</script>

<style scoped>
/* Static fallback (reduced motion): faint diamonds, everything visible. */
.return-ripple {
  opacity: 0.25;
  transform: rotate(45deg);
  transform-box: fill-box;
  transform-origin: center;
}
.return-apex {
  transform-box: fill-box;
  transform-origin: center;
}

@media (prefers-reduced-motion: no-preference) {
  .return-svg {
    animation: return-reveal var(--return-duration) linear both;
  }
  .return-apex {
    animation: return-pop 0.45s cubic-bezier(0.3, 1.6, 0.5, 1) var(--return-delay) both;
  }
  .return-ripple {
    animation: return-ripple 1.5s ease-out var(--return-delay) both;
  }
  .return-ripple--late {
    animation-delay: calc(var(--return-delay) + 0.25s);
  }
}

@keyframes return-reveal {
  from {
    clip-path: inset(0 100% 0 0);
  }
  to {
    clip-path: inset(0 0 0 0);
  }
}
@keyframes return-pop {
  from {
    transform: scale(0);
  }
  to {
    transform: scale(1);
  }
}
@keyframes return-ripple {
  from {
    transform: rotate(45deg) scale(0.3);
    opacity: 0.7;
  }
  to {
    transform: rotate(45deg) scale(1);
    opacity: 0;
  }
}
</style>
