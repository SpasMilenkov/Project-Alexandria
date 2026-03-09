<template>
  <svg
    :width="size"
    :height="size"
    viewBox="0 0 24 24"
    xmlns="http://www.w3.org/2000/svg"
    fill="currentColor"
    aria-label="Loading"
    role="status"
    v-bind="$attrs"
  >
    <rect class="bss3" x="1" y="1" rx="1" width="10" height="10" />
    <rect class="bss3 bss3b" x="1" y="1" rx="1" width="10" height="10" />
    <rect class="bss3 bss3c" x="1" y="1" rx="1" width="10" height="10" />
  </svg>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    size?: number | string;
  }>(),
  {
    size: 16,
  },
);
</script>

<style scoped>
.bss3 {
  animation: bss3-anim 2.4s linear infinite;
  animation-delay: -2.4s;
}
.bss3b {
  animation-delay: -1.6s;
}
.bss3c {
  animation-delay: -0.8s;
}

@keyframes bss3-anim {
  8.33% {
    x: 13px;
    y: 1px;
  }
  25% {
    x: 13px;
    y: 1px;
  }
  33.3% {
    x: 13px;
    y: 13px;
  }
  50% {
    x: 13px;
    y: 13px;
  }
  58.33% {
    x: 1px;
    y: 13px;
  }
  75% {
    x: 1px;
    y: 13px;
  }
  83.33% {
    x: 1px;
    y: 1px;
  }
}
</style>
