<script setup lang="ts">
const { playing = true } = defineProps<{ playing?: boolean }>();
</script>

<template>
  <span class="equalizer" :class="{ paused: !playing }" aria-hidden="true">
    <i />
    <i />
    <i />
  </span>
</template>

<style scoped>
.equalizer {
  display: inline-flex;
  align-items: flex-end;
  gap: 2px;
  width: 15px;
  height: 14px;
}

.equalizer i {
  display: block;
  width: 3px;
  height: 40%;
  border-radius: 1px;
  background: currentColor;
  animation: equalize 0.9s ease-in-out infinite;
}

.equalizer i:nth-child(2) {
  animation-delay: -0.3s;
}

.equalizer i:nth-child(3) {
  animation-delay: -0.6s;
}

.equalizer.paused i {
  animation-play-state: paused;
}

@keyframes equalize {
  0%,
  100% {
    height: 30%;
  }

  50% {
    height: 100%;
  }
}

@media (prefers-reduced-motion: reduce) {
  .equalizer i {
    height: 60%;
    animation: none;
  }
}
</style>
