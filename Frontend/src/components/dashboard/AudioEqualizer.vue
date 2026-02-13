<template>
  <div class="flex items-center justify-center gap-1 h-16">
    <div
      v-for="i in bars"
      :key="i"
      class="w-1 bg-white rounded-full"
      :class="`animate-bar-${i}`"
      :style="`animation-delay: ${i * 0.1}s`"
    />
  </div>
</template>

<script setup lang="ts">
const bars = 5;
</script>

<style scoped>
@keyframes bar-animation {
  0%,
  100% {
    height: 8px;
  }
  50% {
    height: 48px;
  }
}

.animate-bar-1 {
  animation: bar-animation 0.8s ease-in-out infinite;
}

.animate-bar-2 {
  animation: bar-animation 0.7s ease-in-out infinite;
}

.animate-bar-3 {
  animation: bar-animation 0.9s ease-in-out infinite;
}

.animate-bar-4 {
  animation: bar-animation 0.75s ease-in-out infinite;
}

.animate-bar-5 {
  animation: bar-animation 0.85s ease-in-out infinite;
}
</style>
