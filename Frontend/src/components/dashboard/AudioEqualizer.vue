<template>
  <div class="flex items-center justify-center gap-1 h-16">
    <div
      v-for="i in bars"
      :key="i"
      class="bar"
      :style="`--delay: ${i * 0.1}s; --duration: ${durations[i - 1]}s`"
    />
  </div>
</template>

<script setup lang="ts">
const bars = 5;
const durations = [0.8, 0.7, 0.9, 0.75, 0.85];
</script>

<style scoped>
.bar {
  width: 4px;
  height: 48px;
  background: white;
  border-radius: 9999px;
  will-change: transform;
  transform-origin: center;
  animation: bar-animation var(--duration, 0.8s) ease-in-out var(--delay, 0s) infinite;
}

@keyframes bar-animation {
  0%, 100% {
    transform: scaleY(0.167);
  }
  50% {
    transform: scaleY(1);
  }
}
</style>