<script setup lang="ts">
// Sized in em so the tile matches whatever display size the surrounding numerals use.
const { icon } = defineProps<{ icon: string }>();
</script>

<template>
  <span
    class="inline-flex size-[0.78em] shrink-0 items-center justify-center rounded-[0.22em] bg-gray-500/15 text-gray-900 dark:text-gray-100"
  >
    <UIcon :name="icon" class="size-[0.42em]" />
  </span>
</template>
