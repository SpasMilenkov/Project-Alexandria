<script setup lang="ts">
import { useDocumentVisibility, useTimeoutFn } from "@vueuse/core";
import { computed, ref, shallowRef, watch } from "vue";

import type { HomeWidget } from "@/types/home";

import { HomeCountdownStyle } from "@/enums/home-widget";
import { useHomeStore } from "@/stores/home";
import { homeCalendarDate, homeCountdown } from "@/utils/home-general";

import HomeFlipDigit from "./HomeFlipDigit.vue";

const { widget } = defineProps<{ widget: HomeWidget }>();
const home = useHomeStore();
const visibility = useDocumentVisibility();
const now = shallowRef(new Date());
const delay = ref(60_000);

const target = computed(() => homeCalendarDate(widget.options.countdownTarget));
const dateLabel = computed(() =>
  target.value?.toLocaleDateString(undefined, {
    weekday: "long",
    month: "long",
    day: "numeric",
    year: "numeric",
  }),
);

const countdown = computed(() => homeCountdown(target.value, now.value, dateLabel.value));

const { start, stop } = useTimeoutFn(() => updateClock(), delay, { immediate: false });

const updateClock = () => {
  stop();
  now.value = new Date();

  if (!target.value || visibility.value !== "visible") return;

  const midnight = new Date(now.value);
  midnight.setHours(24, 0, 0, 0);
  let nextUpdate = midnight.getTime();

  if (widget.options.countdownStyle === HomeCountdownStyle.Flip && countdown.value!.days > 0) {
    const minutes = Math.floor((target.value.getTime() - now.value.getTime()) / 60_000);

    nextUpdate = Math.min(nextUpdate, target.value.getTime() - minutes * 60_000 + 1);
  }

  delay.value = Math.max(1, nextUpdate - now.value.getTime());
  start();
};

watch([target, () => widget.options.countdownStyle, visibility], updateClock, {
  immediate: true,
  flush: "sync",
});

const groups = computed(() => {
  const value = countdown.value;
  if (!value) return [];

  return [
    {
      label: value.days < 0 ? "days ago" : "days",
      digits: String(Math.abs(value.days)).padStart(2, "0"),
      preciseDigits: value.days > 0 ? String(value.fullDays).padStart(2, "0") : null,
    },
    { label: "hours", digits: String(value.hours).padStart(2, "0") },
    { label: "min", digits: String(value.minutes).padStart(2, "0") },
  ];
});

const relation = computed(() => {
  if (!countdown.value || countdown.value.days === 0) return "";

  return countdown.value.days > 0 ? "until " : "since ";
});
</script>

<template>
  <div
    class="countdown-widget flex min-h-0 min-w-0 flex-1 flex-col justify-center gap-2 text-gray-900 dark:text-gray-100"
    :class="{
      'countdown-widget--dots': widget.options.countdownStyle === HomeCountdownStyle.DayDots,
    }"
    :data-countdown-style="widget.options.countdownStyle"
  >
    <template v-if="countdown">
      <div
        v-if="widget.options.countdownStyle === HomeCountdownStyle.Ring"
        class="countdown-ring flex min-h-0 flex-1 flex-col items-center justify-center gap-2"
      >
        <svg
          class="countdown-dial shrink-0"
          viewBox="0 0 200 200"
          role="img"
          :aria-label="`${countdown.value} ${countdown.unit}`"
        >
          <circle
            cx="100"
            cy="100"
            r="86"
            fill="none"
            stroke="var(--ui-bg-elevated)"
            stroke-width="12"
          />
          <circle
            class="countdown-stroke"
            :class="{ 'countdown-today': countdown.days === 0 }"
            cx="100"
            cy="100"
            r="86"
            fill="none"
            stroke="currentColor"
            stroke-width="12"
            stroke-linecap="round"
            pathLength="100"
            :stroke-dasharray="`${countdown.progress * 100} 100`"
            transform="rotate(-90 100 100)"
          />
          <text
            x="100"
            y="100"
            text-anchor="middle"
            dominant-baseline="central"
            :font-size="countdown.days === 0 ? 38 : Math.min(56, 220 / countdown.value.length)"
            font-weight="700"
            fill="currentColor"
          >
            {{ countdown.value }}
          </text>
          <text x="100" y="137" text-anchor="middle" font-size="16" fill="currentColor">
            {{ countdown.unit }}
          </text>
        </svg>
        <div class="countdown-details min-w-0">
          <p class="countdown-label text-sm font-medium" :title="widget.options.countdownLabel">
            {{ relation }}{{ widget.options.countdownLabel }}
          </p>
          <p class="countdown-date mt-1 text-xs text-gray-500 dark:text-gray-500">
            {{ countdown.date }}
          </p>
        </div>
      </div>

      <template v-else>
        <div
          v-if="widget.options.countdownStyle === HomeCountdownStyle.Flip && countdown.days !== 0"
          class="countdown-flip flex min-w-0 items-center gap-2"
          :aria-label="`${countdown.value} ${countdown.unit}`"
        >
          <div
            v-for="(group, index) in groups"
            v-show="index === 0 || countdown.days > 0"
            :key="group.label"
            class="countdown-flip-group flex min-w-0 flex-col items-center gap-1"
            :class="{ 'countdown-flip-extra': index > 0 }"
            :style="{
              '--countdown-digits': Math.max(group.digits.length, group.preciseDigits?.length ?? 0),
            }"
            aria-hidden="true"
          >
            <div
              class="countdown-digits flex gap-1"
              :class="{ 'countdown-calendar-days': Boolean(group.preciseDigits) }"
            >
              <HomeFlipDigit
                v-for="(digit, digitIndex) in group.digits"
                :key="digitIndex"
                :digit="digit"
                :motion-active="!home.isEditing"
              />
            </div>

            <div v-if="group.preciseDigits" class="countdown-digits countdown-precise-days gap-1">
              <HomeFlipDigit
                v-for="(digit, digitIndex) in group.preciseDigits"
                :key="digitIndex"
                :digit="digit"
                :motion-active="!home.isEditing"
              />
            </div>
            <span class="text-xs text-gray-500 dark:text-gray-500">{{ group.label }}</span>
          </div>
        </div>

        <div v-else class="countdown-number min-w-0">
          <b
            class="countdown-value tabular-nums"
            :class="{ 'countdown-today': countdown.days === 0 }"
            :style="{ '--countdown-length': Math.max(2, countdown.value.length) }"
            >{{ countdown.value }}</b
          >
          <span class="ml-1 text-sm text-gray-600 dark:text-gray-400">{{ countdown.unit }}</span>
        </div>

        <div class="countdown-details min-w-0">
          <p class="countdown-label text-sm font-medium" :title="widget.options.countdownLabel">
            {{ relation }}{{ widget.options.countdownLabel }}
          </p>
          <p class="countdown-date mt-1 text-xs text-gray-500 dark:text-gray-500">
            {{ countdown.date }}
          </p>
        </div>

        <div
          v-if="widget.options.countdownStyle === HomeCountdownStyle.DayDots"
          class="countdown-dots relative min-h-0 flex-1"
          aria-hidden="true"
        >
          <div class="countdown-dot-grid absolute inset-0 grid place-content-center">
            <span
              v-for="index in 30"
              :key="index"
              class="rounded-full bg-gray-200 dark:bg-gray-800"
              :class="{
                'countdown-elapsed-dot':
                  index < 30 && index <= Math.min(30, Math.max(0, 30 - countdown.days)),
                'countdown-target-dot': index === 30,
                'countdown-past-dot': index === 30 && countdown.days < 0,
              }"
            />
          </div>
        </div>
      </template>
    </template>

    <div v-else class="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 text-center">
      <UIcon name="i-heroicons-calendar" class="size-6 text-gray-500 dark:text-gray-500" />
      <p class="text-sm font-medium">Choose a date</p>
      <p class="countdown-date text-xs text-gray-600 dark:text-gray-400">
        Set your event in the widget options.
      </p>
    </div>
  </div>
</template>

<style scoped>
.countdown-widget {
  container: countdown / size;
}

.countdown-label {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  overflow-wrap: anywhere;
}

.countdown-value {
  font-size: clamp(1.125rem, min(calc((150cqi - 3rem) / var(--countdown-length)), 40cqb), 4.5rem);
  line-height: 1;
}

.countdown-widget--dots .countdown-value {
  font-size: clamp(1rem, min(calc(150cqi / var(--countdown-length)), 18cqb), 4.5rem);
}

.countdown-today {
  color: var(--ui-primary);
}

.countdown-stroke {
  color: var(--ui-text-muted);
}

.countdown-dial {
  width: min(100cqi, calc(100cqb - 3rem));
  max-height: 100%;
  aspect-ratio: 1;
}

.countdown-ring .countdown-details {
  text-align: center;
}

.countdown-dots {
  container: countdown-dots / size;
}

.countdown-dot-grid {
  --dot-gap: clamp(2px, min(1cqi, 3cqb), 16px);
  --dot-size: max(
    0px,
    min(calc((100cqi - 9 * var(--dot-gap)) / 10), calc((100cqb - 2 * var(--dot-gap)) / 3))
  );
  grid-template-columns: repeat(10, var(--dot-size));
  grid-template-rows: repeat(3, var(--dot-size));
  gap: var(--dot-gap);
}

.countdown-target-dot {
  background: var(--ui-primary);
}

.countdown-elapsed-dot {
  background: var(--ui-text-muted);
}

.countdown-past-dot {
  background: var(--ui-text-dimmed);
}

.countdown-flip-extra {
  display: none;
}

.countdown-precise-days {
  display: none;
}

.countdown-digits {
  font-size: clamp(0.75rem, min(calc(90cqi / var(--countdown-digits)), 28cqb), 3.5rem);
}

@container countdown (max-height: 180px) {
  .countdown-date {
    display: none;
  }

  .countdown-widget--dots .countdown-label {
    -webkit-line-clamp: 1;
  }
}

@container countdown (min-width: 300px) and (min-height: 130px) {
  .countdown-calendar-days {
    display: none;
  }

  .countdown-precise-days {
    display: flex;
  }
  .countdown-flip-extra {
    display: flex;
  }
  .countdown-digits {
    font-size: clamp(0.75rem, min(calc(36cqi / var(--countdown-digits)), 24cqb), 3rem);
  }
}

@container countdown (min-width: 420px) and (min-height: 260px) {
  .countdown-widget--dots .countdown-label {
    font-size: clamp(14px, min(3cqi, 5cqb), 24px);
  }

  .countdown-widget--dots .countdown-date {
    font-size: clamp(12px, min(2cqi, 4cqb), 18px);
  }
}

@container countdown (min-aspect-ratio: 2 / 1) {
  .countdown-ring {
    flex-direction: row;
  }
  .countdown-ring .countdown-details {
    flex: 1;
    text-align: left;
  }
  .countdown-dial {
    width: min(40cqi, calc(100cqb - 0.5rem));
  }
}
</style>
