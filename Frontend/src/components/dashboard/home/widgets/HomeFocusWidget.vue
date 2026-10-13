<script setup lang="ts">
import { computed } from "vue";

import type { HomeWidget } from "@/types/home";

import { useHomeFocusTimer } from "@/composables/useHomeFocusTimer";
import { HomeFocusStyle } from "@/enums/home-widget";
import { useHomeStore } from "@/stores/home";
import { homeFocusAction, homeFocusDuration, homeFocusLabel } from "@/utils/home-focus";

const { widget } = defineProps<{ widget: HomeWidget }>();
const home = useHomeStore();
const { state, remaining, toggle, reset, skipBreak } = useHomeFocusTimer(() => widget);
const time = computed(
  () =>
    `${String(Math.floor(remaining.value / 60)).padStart(2, "0")}:${String(remaining.value % 60).padStart(2, "0")}`,
);

const progress = computed(() =>
  Math.min(1, remaining.value / homeFocusDuration(state.value.phase, state.value.plan)),
);

const action = computed(() => homeFocusAction(state.value));

const label = computed(() => homeFocusLabel(state.value));

const currentBlock = computed(
  () => (state.value.session - 1) * 2 + Number(state.value.phase !== "focus"),
);

const blocks = computed(() =>
  Array.from({ length: state.value.plan.focusSessions * 2 }, (_, index) => {
    let minutes = state.value.plan.focusMinutes;
    if (index % 2) minutes = state.value.plan.shortBreakMinutes;
    if (index === state.value.plan.focusSessions * 2 - 1)
      minutes = state.value.plan.longBreakMinutes;

    return { index, minutes };
  }),
);
</script>

<template>
  <div
    class="focus-widget flex min-h-0 min-w-0 flex-1 flex-col gap-2 text-gray-900 dark:text-gray-100"
    :data-focus-style="widget.options.focusStyle"
    :data-focus-status="state.status"
    :data-focus-phase="state.phase"
  >
    <div class="focus-main flex min-h-0 flex-1 items-center justify-center gap-2">
      <svg
        v-if="widget.options.focusStyle === HomeFocusStyle.Ring"
        class="focus-dial"
        viewBox="0 0 200 200"
        role="img"
        :aria-label="`${label}, ${time}`"
      >
        <circle
          cx="100"
          cy="100"
          r="96"
          fill="var(--ui-bg)"
          stroke="var(--ui-border-accented)"
          stroke-width="1"
        />
        <circle
          cx="100"
          cy="100"
          r="86"
          fill="none"
          stroke="var(--ui-text-muted)"
          stroke-width="12"
        />
        <circle
          cx="100"
          cy="100"
          r="86"
          fill="none"
          class="focus-progress"
          stroke="currentColor"
          stroke-width="12"
          stroke-linecap="round"
          pathLength="100"
          :stroke-dasharray="`${progress * 100} 100`"
          transform="rotate(-90 100 100)"
        />
        <text
          x="100"
          y="100"
          text-anchor="middle"
          dominant-baseline="central"
          font-size="40"
          font-weight="700"
          fill="currentColor"
        >
          {{ time }}
        </text>
      </svg>

      <div
        class="focus-readout frosted-glass glass-surface-strong min-w-0 rounded-xl border border-gray-200/70 p-2 dark:border-gray-700/70"
      >
        <p
          class="focus-label text-xs font-medium text-gray-700 dark:text-gray-300"
          role="status"
          :title="label"
        >
          {{ label }}
        </p>
        <b
          v-if="widget.options.focusStyle !== HomeFocusStyle.Ring"
          class="focus-time block tabular-nums"
          >{{ time }}</b
        >
        <p class="focus-session mt-1 text-xs text-gray-600 dark:text-gray-400">
          Session {{ state.session }} of {{ state.plan.focusSessions }}
        </p>
      </div>
    </div>

    <template v-if="widget.options.focusStyle === HomeFocusStyle.Digital">
      <div
        class="h-1 shrink-0 overflow-hidden rounded-full bg-gray-300 ring-1 ring-gray-400 dark:bg-gray-600 dark:ring-gray-500"
        aria-hidden="true"
      >
        <div class="focus-progress h-full bg-current" :style="{ width: `${progress * 100}%` }" />
      </div>
      <div class="focus-session-dots flex justify-center gap-2" aria-hidden="true">
        <span
          v-for="index in state.plan.focusSessions"
          :key="index"
          class="size-1.5 rounded-full border border-gray-400 dark:border-gray-500"
          :class="{
            'bg-gray-500 dark:bg-gray-400': index < state.session,
            'ring-1 ring-primary': index === state.session,
          }"
        />
      </div>
    </template>

    <div
      v-else-if="widget.options.focusStyle === HomeFocusStyle.Cycle"
      class="focus-cycle flex h-6 shrink-0 gap-1"
      aria-hidden="true"
    >
      <div
        v-for="block in blocks"
        :key="block.index"
        class="relative min-w-0 overflow-hidden rounded-sm border border-gray-500 bg-gray-200 dark:border-gray-400 dark:bg-gray-700"
        :class="{
          'ring-2 ring-primary': block.index === currentBlock,
          'focus-cycle-complete': block.index < currentBlock,
        }"
        :style="{ flex: block.minutes }"
      >
        <span
          v-if="block.index === currentBlock"
          class="absolute inset-y-0 left-0 bg-gray-700 dark:bg-gray-200"
          :style="{ width: `${(1 - progress) * 100}%` }"
        />
      </div>
    </div>

    <div class="focus-controls flex shrink-0 items-center justify-center gap-2">
      <UButton
        color="primary"
        class="focus-toggle min-w-0 justify-center"
        :icon="state.status === 'running' ? 'i-mdi-pause' : 'i-mdi-play'"
        :aria-label="action"
        :title="action"
        :disabled="home.isEditing"
        @click="toggle"
      >
        <span class="focus-action-label truncate">{{ action }}</span>
      </UButton>
      <UButton
        v-if="state.phase !== 'focus'"
        color="neutral"
        variant="outline"
        size="sm"
        class="focus-skip shrink-0 justify-center"
        icon="i-mdi-skip-next"
        aria-label="Skip break"
        title="Skip break"
        :disabled="home.isEditing"
        @click="skipBreak"
      >
        <span class="focus-action-label">Skip break</span>
      </UButton>

      <UButton
        v-if="state.status !== 'idle'"
        class="focus-reset absolute right-0 top-0 shrink-0 justify-center"
        color="error"
        variant="outline"
        size="xs"
        icon="i-mdi-refresh"
        aria-label="Reset timer"
        title="Reset timer"
        :disabled="home.isEditing"
        @click="reset"
      />
    </div>
  </div>
</template>

<style scoped>
.focus-widget {
  position: relative;
  container: focus-body / size;
}

.focus-main {
  flex-direction: column;
}

.focus-dial {
  width: min(70cqi, calc(100cqb - 6.5rem));
  max-height: 100%;
  flex-shrink: 0;
}

.focus-progress {
  color: var(--ui-text);
}

.focus-readout {
  text-align: center;
}

.focus-time {
  font-size: clamp(1.25rem, min(24cqi, 28cqb), 4rem);
  line-height: 1.1;
}

.focus-reset {
  z-index: 1;
}

.focus-cycle-complete {
  background: var(--ui-text);
}

@container focus-body (max-height: 180px) {
  .focus-main {
    flex-direction: row;
  }
  .focus-dial {
    width: min(48cqi, calc(100cqb - 2.5rem));
  }
  .focus-session,
  .focus-session-dots {
    display: none;
  }
  .focus-label {
    font-size: 0.75rem;
  }
  .focus-readout {
    padding-block: 0.25rem;
  }
  .focus-widget:not([data-focus-style="0"]) .focus-readout {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    max-width: 100%;
  }
  .focus-widget:not([data-focus-style="0"]) .focus-label {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .focus-widget[data-focus-style="0"] .focus-readout {
    flex: 1;
    text-align: left;
  }
  .focus-cycle {
    height: 0.5rem;
  }
  .focus-time {
    font-size: clamp(1rem, min(22cqi, 28cqb), 2.25rem);
    flex-shrink: 0;
  }
  .focus-controls {
    align-self: center;
    width: 100%;
    max-width: 20rem;
  }
  .focus-toggle {
    flex: 1;
  }
  .focus-skip .focus-action-label {
    display: none;
  }
  .focus-reset {
    position: static;
  }
}

@container focus-body (max-width: 150px) and (max-height: 180px) {
  .focus-widget:not([data-focus-status="idle"]) .focus-action-label {
    display: none;
  }
}

@container focus-body (max-width: 240px) and (max-height: 180px) {
  .focus-widget:not([data-focus-phase="focus"]) .focus-action-label {
    display: none;
  }
}

@container focus-body (min-aspect-ratio: 2 / 1) and (min-height: 181px) {
  .focus-main {
    flex-direction: row;
  }
  .focus-dial {
    width: min(45cqi, calc(100cqb - 3rem));
  }
  .focus-readout {
    text-align: left;
  }
  .focus-time {
    font-size: clamp(1.5rem, min(22cqi, 45cqb), 5rem);
  }
}
</style>
