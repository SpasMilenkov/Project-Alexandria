<template>
  <!-- Decorative only: sits behind the page, never takes clicks or focus. -->
  <div
    class="pointer-events-none absolute inset-0 -z-10 text-gray-900 dark:text-gray-100"
    aria-hidden="true"
  >
    <!-- Two half-hidden records, one high on the right, one low on the left -->
    <svg
      v-for="record in records"
      :key="record.id"
      viewBox="0 0 600 600"
      fill="none"
      :class="['absolute hidden max-w-none opacity-60 lg:block 2xl:opacity-100', record.position]"
    >
      <circle
        v-for="r in grooves"
        :key="r"
        cx="300"
        cy="300"
        :r="r"
        stroke="currentColor"
        stroke-opacity="0.16"
        vector-effect="non-scaling-stroke"
      />

      <g :class="record.spin">
        <circle
          v-for="arc in record.arcs"
          :key="arc.r"
          cx="300"
          cy="300"
          :r="arc.r"
          pathLength="100"
          :stroke="arc.color"
          stroke-opacity="0.55"
          stroke-width="2"
          :stroke-dasharray="`${arc.length} ${100 - arc.length}`"
          :transform="`rotate(${arc.turn} 300 300)`"
          vector-effect="non-scaling-stroke"
        />
      </g>

      <path d="M 300 284 L 316 300 L 300 316 L 284 300 Z" fill="currentColor" fill-opacity="0.3" />
    </svg>

    <!-- A ruler down the left edge, fading out at both ends -->
    <div class="ruler absolute inset-y-0 left-4 hidden w-6 opacity-20 xl:block" />
  </div>
</template>

<script setup lang="ts">
// Grooves every 12 units from 48 to 288; arcs sit on grooves so they read as part of the record.
const grooves = Array.from({ length: 21 }, (_, i) => 48 + i * 12);

const records = [
  {
    id: "top-right",
    position: "top-16 -right-[340px] w-[600px]",
    spin: "record-spin",

    arcs: [
      { r: 108, color: "var(--wrapped-blue, #3b82f6)", length: 22, turn: 20 },
      { r: 168, color: "var(--wrapped-rose, #f43f5e)", length: 14, turn: 140 },
      { r: 228, color: "var(--wrapped-gold, #f59e0b)", length: 18, turn: 250 },
      { r: 264, color: "var(--wrapped-purple, #8b5cf6)", length: 8, turn: 60 },
    ],
  },

  {
    id: "bottom-left",
    position: "bottom-24 -left-[300px] w-[440px]",
    spin: "record-spin record-spin--reverse",

    arcs: [
      { r: 108, color: "var(--wrapped-teal, #14b8a6)", length: 20, turn: 80 },
      { r: 180, color: "var(--wrapped-lilac, #a78bfa)", length: 12, turn: 200 },
      { r: 252, color: "var(--wrapped-sage, #10b981)", length: 16, turn: 300 },
    ],
  },
];
</script>

<style scoped>
.ruler {
  /* Minor tick every 10px, major tick every 80px. */
  background-image:
    repeating-linear-gradient(to bottom, currentColor 0 1px, transparent 1px 10px),
    repeating-linear-gradient(to bottom, currentColor 0 2px, transparent 2px 80px);
  background-size:
    10px 100%,
    24px 100%;
  background-repeat: no-repeat;
  mask-image: linear-gradient(to bottom, transparent, black 20%, black 80%, transparent);
}

@media (prefers-reduced-motion: no-preference) {
  /* About one turn every three minutes: felt more than seen. */
  .record-spin {
    transform-origin: 300px 300px;
    animation: record-turn 180s linear infinite;
  }
  .record-spin--reverse {
    animation-direction: reverse;
  }
}
@keyframes record-turn {
  to {
    transform: rotate(360deg);
  }
}
</style>
