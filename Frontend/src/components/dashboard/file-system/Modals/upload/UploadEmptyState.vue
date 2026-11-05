<script setup lang="ts">
defineProps<{
  icon: string;
  title: string;
  buttonLabel: string;
  buttonIcon: string;
  subtitle?: string;
  hint?: string;
}>();

defineEmits<{ select: [] }>();
</script>

<template>
  <div
    class="flex min-h-56 flex-col items-center justify-center gap-4 rounded-xl border-2 border-dashed border-gray-200/70 px-6 py-8 text-center sm:min-h-72 dark:border-gray-700/70"
  >
    <UIcon :name="icon" class="h-12 w-12 text-gray-400 dark:text-gray-600" />

    <div class="space-y-1">
      <p class="text-sm font-medium text-gray-900 dark:text-gray-100">{{ title }}</p>
      <p v-if="subtitle" class="text-sm text-gray-600 dark:text-gray-400">{{ subtitle }}</p>
    </div>

    <UButton
      :label="buttonLabel"
      :icon="buttonIcon"
      variant="outline"
      color="neutral"
      @click="$emit('select')"
    />

    <p v-if="hint" class="text-xs text-gray-500 dark:text-gray-500">{{ hint }}</p>

    <slot />
  </div>
</template>
