<template>
  <div
    class="inline-flex shrink-0 rounded-lg border border-gray-200 p-0.5 dark:border-gray-700"
    role="group"
    :aria-label="label"
  >
    <UButton
      v-for="option in options"
      :key="String(option.value)"
      :label="option.label"
      size="xs"
      :color="model === option.value ? 'primary' : 'neutral'"
      :variant="model === option.value ? 'solid' : 'ghost'"
      :aria-pressed="model === option.value"
      @click="model = option.value"
    />
  </div>
</template>

<script setup lang="ts" generic="T extends string | number">
const { options, label } = defineProps<{
  options: readonly { label: string; value: T }[];
  // Accessible name for the button group.
  label: string;
}>();

const model = defineModel<T>({ required: true });
</script>
