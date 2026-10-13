<script setup lang="ts">
defineProps<{
  src: string | null;
  loaded: boolean;
  spinning: boolean;
  icon: string;
}>();

const emit = defineEmits<{ load: []; error: [] }>();
</script>

<template>
  <div class="player-record relative isolate" aria-hidden="true">
    <div class="record-disc absolute inset-0 rounded-full" :class="{ 'record-playing': spinning }">
      <div class="record-label absolute overflow-hidden rounded-full">
        <UIcon
          :name="icon"
          class="absolute inset-0 m-auto size-8 text-gray-200 dark:text-gray-200"
        />

        <img
          v-if="src"
          :src="src"
          alt=""
          decoding="async"
          class="absolute inset-0 h-full w-full object-cover"
          :class="{ invisible: !loaded }"
          @load="emit('load')"
          @error="emit('error')"
        />

        <span class="record-spindle absolute rounded-full" />
      </div>
    </div>

    <div class="record-reflection pointer-events-none absolute inset-0 rounded-full" />

    <svg viewBox="0 0 100 100" class="record-arm pointer-events-none absolute">
      <circle cx="98" cy="1" r="4" fill="#20232c" stroke="#9ca3af" stroke-width="1" />
      <path
        d="M98 1 L98 35 L90 58"
        fill="none"
        stroke="#d1d5db"
        stroke-width="2"
        stroke-linecap="round"
      />
      <rect
        x="87.5"
        y="54"
        width="5"
        height="10"
        rx="1"
        fill="#9ca3af"
        transform="rotate(20 90 59)"
      />
      <circle cx="98" cy="1" r="1.5" fill="#d1d5db" />
    </svg>
  </div>
</template>

<style scoped>
.player-record {
  aspect-ratio: 1;
}

.record-disc {
  background:
    repeating-radial-gradient(
      circle,
      transparent 0 2px,
      rgb(255 255 255 / 7%) 2px 3px,
      transparent 3px 5px
    ),
    #101116;
  border: 1px solid #353742;
  box-shadow:
    0 8px 20px rgb(0 0 0 / 35%),
    inset 0 0 0 5px #17181e;
  animation: record-turn 18s linear infinite;
  animation-play-state: paused;
}

.record-playing {
  animation-play-state: running;
}

.record-label {
  inset: 24%;
  border: 2px solid #353742;
  background: #34394b;
}

.record-spindle {
  inset: calc(50% - 4px);
  border: 2px solid #d1d5db;
  background: #111827;
}

.record-reflection {
  background: conic-gradient(
    from -40deg,
    transparent 0deg 55deg,
    rgb(255 255 255 / 12%) 70deg,
    transparent 100deg 230deg,
    rgb(255 255 255 / 8%) 250deg,
    transparent 280deg
  );
  mask-image: radial-gradient(circle, transparent 36%, black 38%);
}

.record-arm {
  inset: 0;
  width: 100%;
  height: 100%;
  overflow: visible;
}

@keyframes record-turn {
  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .record-disc {
    animation: none;
  }
}
</style>
