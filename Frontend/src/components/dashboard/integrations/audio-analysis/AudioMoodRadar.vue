<script setup lang="ts">
import { computed, ref, useId } from "vue";

import { useSettingsStore } from "@/stores/settings";
import { type MoodEntry, formatConfidence } from "@/utils/audio-taxonomy.utils";

interface Point {
  x: number;
  y: number;
}

interface CornerStyle {
  smoothing: number;
  lineJoin: "miter" | "round";
  dotRoundness: number;
}

const { entries, color } = defineProps<{
  entries: MoodEntry[];
  color: string;
}>();

const WIDTH = 400;
const HEIGHT = 300;
const CENTER: Point = { x: WIDTH / 2, y: HEIGHT / 2 };
const RADIUS = 100;
const MIN_SHAPE_RATIO = 0.06;
const RING_STEPS = [0.25, 0.5, 0.75, 1];

const CORNER_STYLES: Record<"sharp" | "soft" | "round", CornerStyle> = {
  sharp: { smoothing: 0, lineJoin: "miter", dotRoundness: 0 },
  soft: { smoothing: 0.35, lineJoin: "round", dotRoundness: 0.45 },
  round: { smoothing: 0.8, lineJoin: "round", dotRoundness: 1 },
};

// Opposing moods sit across from each other, so the shape leans toward the dominant pole.
const AXIS_ORDER = [
  "mood_happy:happy",
  "mood_party:party",
  "mood_aggressive:aggressive",
  "mood_sad:sad",
  "mood_acoustic:acoustic",
  "mood_relaxed:relaxed",
];

const settingsStore = useSettingsStore();

const gradientId = `mood-radar-${useId()}`;
const activeKey = ref<string | null>(null);

// Same thresholds the settings page uses to label the corner roundness preference.
const cornerStyle = computed(() => {
  if (settingsStore.cornerRadius <= 0) return CORNER_STYLES.sharp;
  if (settingsStore.cornerRadius <= 0.25) return CORNER_STYLES.soft;

  return CORNER_STYLES.round;
});

const axes = computed(() =>
  AXIS_ORDER.map((key) => entries.find((entry) => entry.key === key)).filter(
    (entry): entry is MoodEntry => entry !== undefined,
  ),
);

const leadingKey = computed(() => {
  const leading = axes.value.reduce<MoodEntry | null>(
    (best, entry) => (best === null || entry.value > best.value ? entry : best),
    null,
  );

  return leading?.key ?? null;
});

const angleAt = (index: number) => (index * 2 * Math.PI) / axes.value.length - Math.PI / 2;

const pointAt = (index: number, ratio: number): Point => ({
  x: CENTER.x + Math.cos(angleAt(index)) * RADIUS * ratio,
  y: CENTER.y + Math.sin(angleAt(index)) * RADIUS * ratio,
});

const toPoints = (points: Point[]) => points.map(({ x, y }) => `${x},${y}`).join(" ");

const rings = computed(() =>
  RING_STEPS.map((step) => toPoints(axes.value.map((_, index) => pointAt(index, step)))),
);

const vertices = computed(() =>
  axes.value.map((entry, index) => ({
    ...entry,
    edge: pointAt(index, 1),
    point: pointAt(index, Math.max(entry.value, MIN_SHAPE_RATIO)),
  })),
);

// Closed Catmull-Rom spline through every vertex, so the dots stay on the outline.
// Zero smoothing collapses the control points onto the vertices, giving straight edges.
const shapePath = computed(() => {
  const points = vertices.value.map((vertex) => vertex.point);
  const count = points.length;

  if (count < 3) return "";

  const factor = cornerStyle.value.smoothing / 6;

  const segments = points.map((current, index) => {
    const previous = points[(index - 1 + count) % count]!;
    const next = points[(index + 1) % count]!;
    const afterNext = points[(index + 2) % count]!;

    const c1x = current.x + (next.x - previous.x) * factor;
    const c1y = current.y + (next.y - previous.y) * factor;
    const c2x = next.x - (afterNext.x - current.x) * factor;
    const c2y = next.y - (afterNext.y - current.y) * factor;

    return `C ${c1x} ${c1y} ${c2x} ${c2y} ${next.x} ${next.y}`;
  });

  return `M ${points[0]!.x} ${points[0]!.y} ${segments.join(" ")} Z`;
});

// Re-runs the bloom animation whenever the scores change.
const shapeKey = computed(() => axes.value.map((entry) => entry.value.toFixed(3)).join("|"));

const labelTransform = (index: number): string => {
  const cos = Math.cos(angleAt(index));
  const sin = Math.sin(angleAt(index));

  if (Math.abs(cos) < 0.1) return sin < 0 ? "translate(-50%, -100%)" : "translate(-50%, 0)";
  if (cos > 0) return "translate(0, -50%)";

  return "translate(-100%, -50%)";
};

const labels = computed(() =>
  axes.value.map((entry, index) => {
    const isVertical = Math.abs(Math.cos(angleAt(index))) < 0.1;
    const anchor = pointAt(index, isVertical ? 1.12 : 1.16);

    return {
      ...entry,
      isVertical,
      style: {
        "--i": index,
        left: `${(anchor.x / WIDTH) * 100}%`,
        top: `${(anchor.y / HEIGHT) * 100}%`,
        transform: labelTransform(index),
      },
    };
  }),
);

const ariaLabel = computed(
  () =>
    `Mood profile: ${axes.value.map((entry) => `${entry.name} ${formatConfidence(entry.value)}`).join(", ")}`,
);

const isLeading = (key: string) => key === leadingKey.value;
const isActive = (key: string) => key === activeKey.value;

const dotRadius = (key: string) => {
  if (isLeading(key) || isActive(key)) return 4.5;

  return 3.5;
};

const dotRect = (vertex: { key: string; point: Point }) => {
  const radius = dotRadius(vertex.key);

  return {
    x: vertex.point.x - radius,
    y: vertex.point.y - radius,
    width: radius * 2,
    height: radius * 2,
    rx: radius * cornerStyle.value.dotRoundness,
  };
};

const labelNameClass = (key: string) => {
  if (isLeading(key)) return "font-semibold text-gray-900 dark:text-gray-100";
  if (isActive(key)) return "font-medium text-gray-900 dark:text-gray-100";

  return "text-gray-600 dark:text-gray-400";
};

const setActive = (key: string | null) => {
  activeKey.value = key;
};
</script>

<template>
  <div
    class="@container/radar relative mx-auto aspect-4/3 w-full max-w-3xl"
    role="img"
    :aria-label="ariaLabel"
    :style="{ color }"
  >
    <svg
      :viewBox="`0 0 ${WIDTH} ${HEIGHT}`"
      class="absolute inset-0 size-full overflow-visible"
      aria-hidden="true"
    >
      <defs>
        <radialGradient :id="gradientId">
          <stop offset="0%" stop-color="currentColor" stop-opacity="0.2" />
          <stop offset="100%" stop-color="currentColor" stop-opacity="0.55" />
        </radialGradient>
      </defs>

      <polygon
        v-for="ring in rings"
        :key="ring"
        :points="ring"
        class="fill-none stroke-black/10 dark:stroke-white/10"
        stroke-width="1"
        :stroke-linejoin="cornerStyle.lineJoin"
        vector-effect="non-scaling-stroke"
      />

      <line
        v-for="vertex in vertices"
        :key="`spoke-${vertex.key}`"
        :x1="CENTER.x"
        :y1="CENTER.y"
        :x2="vertex.edge.x"
        :y2="vertex.edge.y"
        class="transition-[stroke-opacity] duration-200 motion-reduce:transition-none"
        :class="isActive(vertex.key) ? '' : 'stroke-black/10 dark:stroke-white/10'"
        :stroke="isActive(vertex.key) ? 'currentColor' : undefined"
        :stroke-opacity="isActive(vertex.key) ? 0.6 : undefined"
        stroke-width="1"
        vector-effect="non-scaling-stroke"
      />

      <g :key="shapeKey" class="radar-bloom">
        <g>
          <path
            :d="shapePath"
            :fill="`url(#${gradientId})`"
            stroke="currentColor"
            stroke-width="2.5"
            :stroke-linejoin="cornerStyle.lineJoin"
            vector-effect="non-scaling-stroke"
          />

          <rect
            v-for="vertex in vertices"
            :key="`dot-${vertex.key}`"
            v-bind="dotRect(vertex)"
            fill="currentColor"
            class="stroke-white dark:stroke-gray-900"
            stroke-width="2"
            vector-effect="non-scaling-stroke"
          />
        </g>
      </g>

      <circle
        v-for="vertex in vertices"
        :key="`hit-${vertex.key}`"
        :cx="vertex.point.x"
        :cy="vertex.point.y"
        r="14"
        fill="transparent"
        @mouseenter="setActive(vertex.key)"
        @mouseleave="setActive(null)"
      />
    </svg>

    <div
      v-for="label in labels"
      :key="label.key"
      class="radar-label absolute flex gap-x-1.5 whitespace-nowrap text-xs leading-tight @lg/radar:text-sm"
      :class="label.isVertical ? 'items-baseline' : 'flex-col'"
      :style="label.style"
      @mouseenter="setActive(label.key)"
      @mouseleave="setActive(null)"
    >
      <span :class="labelNameClass(label.key)">{{ label.name }}</span>
      <span class="tabular-nums text-gray-600 dark:text-gray-400">
        {{ formatConfidence(label.value) }}
      </span>
    </div>
  </div>
</template>

<style scoped>
.radar-bloom {
  transform-box: view-box;
  transform-origin: 200px 150px;
  animation: radar-bloom 900ms cubic-bezier(0.22, 1, 0.36, 1) both;
}

.radar-label {
  animation: radar-fade 500ms ease-out both;
  animation-delay: calc(var(--i) * 60ms + 300ms);
}

@keyframes radar-bloom {
  from {
    opacity: 0;
    transform: scale(0.2);
  }

  to {
    opacity: 1;
    transform: scale(1);
  }
}

@keyframes radar-fade {
  from {
    opacity: 0;
  }

  to {
    opacity: 1;
  }
}

@media (prefers-reduced-motion: reduce) {
  .radar-bloom,
  .radar-label {
    animation: none;
  }
}
</style>
