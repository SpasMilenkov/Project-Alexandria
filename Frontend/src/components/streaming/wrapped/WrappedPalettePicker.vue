<script setup lang="ts">
import { computed } from "vue";

import { type WrappedPalette, wrappedPalettePresets } from "@/utils/wrapped-palette.utils";

const {
  modelValue,
  defaultColors,
  label = "Wrapped",
  description = "Pick three colors. Try them on your year.",
} = defineProps<{
  modelValue: WrappedPalette | null;
  defaultColors: WrappedPalette;
  label?: string;
  description?: string;
}>();

const emit = defineEmits<{ "update:modelValue": [palette: WrappedPalette | null] }>();
const colorLabels = ["Lead", "Harmony", "Highlight"];
const swatches = computed(() => modelValue ?? defaultColors);

const isSelected = (colors: WrappedPalette) =>
  colors.every((color, index) => color.toLowerCase() === modelValue?.[index]?.toLowerCase());

const editColor = (index: number, event: Event) => {
  if (!(event.target instanceof HTMLInputElement)) return;

  const colors: WrappedPalette = [...swatches.value];

  colors[index] = event.target.value;
  emit("update:modelValue", colors);
};
</script>

<template>
  <div>
    <div class="mb-4 flex items-center justify-between gap-2">
      <p class="text-sm font-semibold text-gray-900 dark:text-gray-100">Make it yours</p>

      <UButton
        color="error"
        variant="outline"
        size="xs"
        :disabled="!modelValue"
        @click="emit('update:modelValue', null)"
        >Reset</UButton
      >
    </div>

    <div class="grid grid-cols-2 gap-2" role="group" :aria-label="`${label} color palettes`">
      <button
        v-for="preset in wrappedPalettePresets"
        :key="preset.name"
        type="button"
        class="flex flex-col items-start gap-2 rounded-xl border border-gray-200/70 p-2 text-xs text-gray-700 transition-transform focus-visible:outline-2 focus-visible:outline-primary dark:border-gray-700/70 dark:text-gray-300 motion-reduce:transition-none"
        :class="{ 'ring-1 ring-primary scale-[1.03]': isSelected(preset.colors) }"
        :aria-pressed="isSelected(preset.colors)"
        @click="emit('update:modelValue', [...preset.colors])"
      >
        <span class="flex -space-x-2" aria-hidden="true">
          <span
            v-for="(color, index) in preset.colors"
            :key="index"
            class="h-6 w-6 rounded-full ring-1 ring-black/10"
            :style="{ backgroundColor: color }"
          />
        </span>
        {{ preset.name }}
      </button>
    </div>

    <div class="mt-4 border-t border-gray-200/70 pt-4 dark:border-gray-700/70">
      <UFormField label="Your own mix" :description="description">
        <div class="mt-2 flex gap-4">
          <label
            v-for="(color, index) in swatches"
            :key="index"
            class="flex cursor-pointer flex-col items-center gap-2 text-xs text-gray-600 dark:text-gray-400"
          >
            <input
              type="color"
              :value="color"
              :aria-label="`${label} ${colorLabels[index]} color`"
              class="palette-color h-10 w-10 cursor-pointer rounded-full focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
              @input="editColor(index, $event)"
            />
            {{ colorLabels[index] }}
          </label>
        </div>
      </UFormField>
    </div>
  </div>
</template>

<style scoped>
.palette-color {
  padding: 0;
  overflow: hidden;
  border: 0;
  background: transparent;
}
.palette-color::-webkit-color-swatch-wrapper {
  padding: 0;
}
.palette-color::-webkit-color-swatch,
.palette-color::-moz-color-swatch {
  border: 0;
  border-radius: 50%;
}
</style>
