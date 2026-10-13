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
        <WrappedPalettePicker
          :model-value="modelValue"
          :default-colors="swatches"
          @update:model-value="emit('update:modelValue', $event)"
        />

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
import { type WrappedPalette, wrappedExportColors } from "@/utils/wrapped-palette.utils";

import WrappedPalettePicker from "./WrappedPalettePicker.vue";

const { modelValue } = defineProps<{ modelValue: WrappedPalette | null }>();
const emit = defineEmits<{ "update:modelValue": [palette: WrappedPalette | null] }>();
const open = ref(false);
const pinned = ref(false);
const panel = ref<HTMLElement | null>(null);
const trigger = ref<HTMLButtonElement | null>(null);
const { isDark } = useTheme();

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
@media (prefers-reduced-motion: reduce) {
  .palette-dot {
    transition: none;
  }
}
</style>
