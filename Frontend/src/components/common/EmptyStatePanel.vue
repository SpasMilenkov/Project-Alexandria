<script setup lang="ts">
// Skeleton blocks must stay free of backdrop-filter: blurred layers escape the fade mask.
const { panel = true } = defineProps<{ panel?: boolean }>();
</script>

<template>
  <div
    class="relative min-h-[28rem] flex-1 overflow-hidden"
    :class="{
      'rounded-xl border border-gray-200/70 frosted-glass glass-surface dark:border-gray-700/70':
        panel,
    }"
  >
    <div
      class="pointer-events-none absolute inset-0 select-none [mask-image:linear-gradient(to_bottom,black_20%,transparent_95%)]"
      aria-hidden="true"
    >
      <slot name="ghost" />
    </div>

    <div class="absolute inset-0 flex overflow-y-auto p-4">
      <slot />
    </div>
  </div>
</template>
