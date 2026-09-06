<script setup lang="ts">
defineProps<{
  icon: string;
  buttonLabel: string;
  buttonIcon: string;
  title?: string;
  subtitle?: string;
  hint?: string;
  tall?: boolean;
}>();

defineEmits<{ select: [] }>();
</script>

<template>
  <div
    :class="
      tall
        ? 'sm:min-h-80 rounded-lg border-2 border-dashed border-gray-200 dark:border-gray-700 flex flex-col items-center justify-center gap-4 p-6'
        : 'rounded-lg border border-dashed border-gray-200/70 dark:border-gray-700/70 p-8 flex flex-col items-center gap-4'
    "
  >
    <UIcon :name="icon" class="size-10 text-muted" />

    <div v-if="title" class="text-center">
      <p class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ title }}</p>
      <p v-if="subtitle" class="text-xs text-muted mt-1">{{ subtitle }}</p>
    </div>

    <UButton
      :label="buttonLabel"
      :icon="buttonIcon"
      variant="outline"
      color="neutral"
      @click="$emit('select')"
    />

    <p v-if="hint" class="text-xs text-muted">{{ hint }}</p>

    <slot />
    <slot name="below" />
  </div>
</template>
