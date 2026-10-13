<script setup lang="ts">
defineProps<{ icon: string }>();
</script>

<template>
  <span class="spotlight-art pointer-events-none relative min-h-0 min-w-0" aria-hidden="true">
    <span class="spotlight-dots absolute inset-0" />

    <span class="spotlight-scene">
      <span
        class="spotlight-orbit absolute rounded-full border border-gray-200/70 dark:border-gray-700/70"
      />

      <span
        class="spotlight-sheet spotlight-sheet-back absolute inset-0 border border-gray-200/70 bg-gray-100/70 dark:border-gray-700/70 dark:bg-gray-800/70"
      />

      <span
        class="spotlight-sheet spotlight-sheet-middle absolute inset-0 border border-gray-200/70 bg-gray-100/70 dark:border-gray-700/70 dark:bg-gray-800/70"
      />

      <span
        class="spotlight-face glass-surface absolute inset-0 flex items-center justify-center border border-gray-200/70 shadow-sm dark:border-gray-700/70"
      >
        <UIcon :name="icon" class="spotlight-glyph text-gray-600 dark:text-gray-400" />
      </span>
    </span>
  </span>
</template>

<style scoped>
.spotlight-art {
  container: spotlight-art / size;
  overflow: hidden;
}

.spotlight-dots {
  background-image: radial-gradient(var(--ui-border-muted) 1px, transparent 1px);
  background-size: 1rem 1rem;
  mask-image: radial-gradient(ellipse at center, black, transparent 70%);
}

.spotlight-scene {
  position: absolute;
  top: 50%;
  left: 50%;
  width: clamp(2.5rem, min(48cqi, 64cqb), 13rem);
  aspect-ratio: 1;
  transform: translate(-50%, -50%);
}

.spotlight-orbit {
  inset: -24%;
}

.spotlight-sheet,
.spotlight-face {
  border-radius: clamp(0.75rem, 8cqb, 1.5rem);
}

.spotlight-sheet-back {
  transform: translate(-8%, 4%) rotate(-14deg);
}

.spotlight-sheet-middle {
  transform: translate(8%, -4%) rotate(10deg);
}

.spotlight-glyph {
  width: 44%;
  height: 44%;
}
</style>
