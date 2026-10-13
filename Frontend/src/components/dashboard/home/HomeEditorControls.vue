<script setup lang="ts">
import type { HomeEditorTarget } from "@/types/home";

defineProps<{ target: HomeEditorTarget; separateMobile: boolean; disabled: boolean }>();

const emit = defineEmits<{
  target: [value: HomeEditorTarget];
  separate: [value: boolean];
  reset: [];
}>();
</script>

<template>
  <section aria-label="Dashboard layout settings" class="flex min-w-0 flex-col gap-4">
    <div class="flex flex-col gap-4">
      <div class="flex items-center gap-2" role="group" aria-label="Layout preview">
        <UButton
          :color="target === 'desktop' ? 'primary' : 'neutral'"
          :variant="target === 'desktop' ? 'solid' : 'outline'"
          :aria-pressed="target === 'desktop'"
          :disabled="disabled"
          @click="emit('target', 'desktop')"
          >Desktop</UButton
        >
        <UButton
          :color="target === 'mobile' ? 'primary' : 'neutral'"
          :variant="target === 'mobile' ? 'solid' : 'outline'"
          :aria-pressed="target === 'mobile'"
          :disabled="disabled"
          @click="emit('target', 'mobile')"
          >Mobile</UButton
        >
      </div>
    </div>

    <UFormField
      label="Use a separate mobile layout"
      description="Your saved mobile arrangement is kept when this is off."
    >
      <USwitch
        :model-value="separateMobile"
        :disabled="disabled"
        aria-label="Use a separate mobile layout"
        @update:model-value="emit('separate', $event)"
      />
    </UFormField>

    <p
      v-if="target === 'mobile' && !separateMobile"
      class="text-sm text-gray-600 dark:text-gray-400"
    >
      This previews your shared layout. Enable a separate mobile layout to edit it independently.
    </p>
    <p v-else id="home-placement-help" class="text-sm text-gray-600 dark:text-gray-400">
      Drag a widget to place it; drag its edges to resize. Arrow keys move it one cell.
    </p>

    <UButton
      color="error"
      variant="outline"
      icon="i-heroicons-arrow-path"
      :disabled="disabled || (target === 'mobile' && !separateMobile)"
      @click="emit('reset')"
      >Reset this layout</UButton
    >
  </section>
</template>
