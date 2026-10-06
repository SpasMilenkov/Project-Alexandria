<template>
  <UPopover
    v-model:open="open"
    :content="{
      align: 'end',
      sideOffset: 8,
      onOpenAutoFocus: keepHoverFocus,
      onCloseAutoFocus: restoreFocus,
    }"
    :ui="{
      content:
        'frosted-glass glass-surface-strong rounded-2xl ring-1 ring-gray-200/70 dark:ring-gray-700/70',
    }"
  >
    <button
      ref="trigger"
      type="button"
      class="palette-trigger group flex h-10 items-center rounded-full px-2 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
      :class="{ 'palette-trigger-open': open }"
      aria-label="Change Wrapped colors"
      @pointerenter="enterTrigger"
      @pointerleave="scheduleClose"
      @focus="openOnFocus"
      @click.prevent="togglePinned"
    >
      <span
        v-for="(color, index) in swatches"
        :key="index"
        class="palette-dot h-7 w-7 rounded-full ring-1 ring-black/10 dark:ring-white/20"
        :style="{ backgroundColor: color }"
        aria-hidden="true"
      />
    </button>

    <template #content>
      <div
        ref="panel"
        class="w-72 max-w-[calc(100vw-2rem)] p-4"
        @pointerenter="cancelClose"
        @pointerleave="scheduleClose"
        @focusin="pinned = true"
      >
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

        <div class="grid grid-cols-2 gap-2" role="group" aria-label="Wrapped color palettes">
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
          <UFormField label="Your own mix" description="Pick three colors. Try them on your year.">
            <div class="mt-2 flex gap-4">
              <label
                v-for="(color, index) in swatches"
                :key="index"
                class="flex cursor-pointer flex-col items-center gap-2 text-xs text-gray-600 dark:text-gray-400"
              >
                <input
                  type="color"
                  :value="color"
                  :aria-label="`Wrapped ${colorLabels[index]} color`"
                  class="palette-color h-10 w-10 cursor-pointer rounded-full focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
                  @input="editColor(index, $event)"
                />
                {{ colorLabels[index] }}
              </label>
            </div>
          </UFormField>
        </div>

        <p class="mt-4 text-xs leading-relaxed text-gray-500 dark:text-gray-400">
          Saved on this browser. Your export wears the same colors.
        </p>
      </div>
    </template>
  </UPopover>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from "vue";

import { useTheme } from "@/composables/useTheme";
import {
  type WrappedPalette,
  wrappedExportColors,
  wrappedPalettePresets,
} from "@/utils/wrapped-palette.utils";

const { modelValue } = defineProps<{ modelValue: WrappedPalette | null }>();
const emit = defineEmits<{ "update:modelValue": [palette: WrappedPalette | null] }>();
const open = ref(false);
const pinned = ref(false);
const panel = ref<HTMLElement | null>(null);
const trigger = ref<HTMLButtonElement | null>(null);
const { isDark } = useTheme();
const colorLabels = ["Lead", "Harmony", "Highlight"];

const swatches = computed<WrappedPalette>(() => {
  if (modelValue) return modelValue;

  const colors = wrappedExportColors(isDark.value);

  return [colors.blue, colors.purple, colors.leaf];
});

let closeTimer: ReturnType<typeof setTimeout> | undefined = undefined;
let restoringFocus = false;

const cancelClose = () => clearTimeout(closeTimer);

const enterTrigger = (event: PointerEvent) => {
  if (event.pointerType !== "mouse") return;

  cancelClose();

  if (!open.value) pinned.value = false;

  open.value = true;
};

const scheduleClose = () => {
  cancelClose();

  if (pinned.value) return;

  closeTimer = setTimeout(() => {
    if (!panel.value?.contains(document.activeElement)) open.value = false;
  }, 200);
};

const openOnFocus = (event: FocusEvent) => {
  if (restoringFocus) return;
  if (!(event.target instanceof HTMLElement) || !event.target.matches(":focus-visible")) return;

  pinned.value = true;
  open.value = true;
};

const togglePinned = () => {
  cancelClose();

  if (open.value && pinned.value) {
    open.value = false;
    pinned.value = false;

    return;
  }

  pinned.value = true;
  open.value = true;
};

const keepHoverFocus = (event: Event) => {
  if (!pinned.value) event.preventDefault();
};

const restoreFocus = (event: Event) => {
  event.preventDefault();

  if (!pinned.value) return;

  restoringFocus = true;
  trigger.value?.focus({ preventScroll: true });
  restoringFocus = false;
};

const isSelected = (colors: WrappedPalette) =>
  colors.every((color, index) => color.toLowerCase() === modelValue?.[index]);

const editColor = (index: number, event: Event) => {
  if (!(event.target instanceof HTMLInputElement)) return;

  const colors: WrappedPalette = [...swatches.value];

  colors[index] = event.target.value;
  emit("update:modelValue", colors);
};

onBeforeUnmount(cancelClose);
</script>

<style scoped>
.palette-dot {
  transition:
    margin 200ms ease,
    transform 200ms ease;
}
.palette-dot + .palette-dot {
  margin-left: -8px;
}
.palette-trigger:hover .palette-dot + .palette-dot,
.palette-trigger:focus-visible .palette-dot + .palette-dot,
.palette-trigger-open .palette-dot + .palette-dot {
  margin-left: 0;
}
.palette-trigger:hover .palette-dot,
.palette-trigger-open .palette-dot {
  transform: scale(1.05);
}
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
@media (prefers-reduced-motion: reduce) {
  .palette-dot {
    transition: none;
  }
}
</style>
