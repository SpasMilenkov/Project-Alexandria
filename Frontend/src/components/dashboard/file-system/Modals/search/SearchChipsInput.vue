<template>
  <div
    class="flex flex-wrap items-center gap-1.5 rounded-md bg-default px-3 py-2 ring ring-inset ring-accented focus-within:ring-2 focus-within:ring-primary"
    @click="focus()"
  >
    <UIcon name="i-lucide-search" class="w-5 h-5 shrink-0 text-gray-500 dark:text-gray-500" />

    <span
      v-for="chip in chips"
      :key="chip.id"
      class="inline-flex max-w-full items-center rounded-full bg-elevated text-sm"
    >
      <button
        type="button"
        class="inline-flex min-w-0 items-center gap-1 rounded-l-full py-0.5 pl-2.5 pr-1 hover:bg-accented"
        :aria-label="`Change filter ${chip.key}: ${chip.value}`"
        @click.stop="emit('editChip', chip.filter)"
      >
        <UIcon :name="chip.icon" class="w-3.5 h-3.5 shrink-0 text-gray-500 dark:text-gray-500" />
        <span class="text-gray-600 dark:text-gray-400">{{ chip.key }}:</span>
        <span class="max-w-40 truncate text-gray-900 dark:text-gray-100">{{ chip.value }}</span>
      </button>
      <button
        type="button"
        class="inline-flex items-center rounded-r-full py-1 pl-0.5 pr-2 text-gray-500 hover:bg-accented hover:text-gray-900 dark:text-gray-500 dark:hover:text-gray-100"
        :aria-label="`Remove filter ${chip.key}: ${chip.value}`"
        @click.stop="emit('removeChip', chip.filter)"
      >
        <UIcon name="i-lucide-x" class="w-3.5 h-3.5" />
      </button>
    </span>

    <input
      ref="inputRef"
      v-model="text"
      type="text"
      role="combobox"
      autocomplete="off"
      spellcheck="false"
      aria-label="Search by name or filter"
      :aria-controls="listboxId"
      :aria-expanded="expanded"
      :aria-activedescendant="activeDescendant"
      :placeholder="placeholder"
      class="min-w-32 flex-1 bg-transparent py-1 text-base text-gray-900 outline-none placeholder:text-gray-500 dark:text-gray-100 dark:placeholder:text-gray-500"
      @input="emit('typed')"
      @keydown="emit('keydown', $event)"
      @focus="emit('focusChange', true)"
      @blur="emit('focusChange', false)"
    />

    <!-- Mousedown keeps the input focused, so pressing the button toggles the list instead of reopening it. -->
    <UButton
      icon="i-lucide-sliders-horizontal"
      size="sm"
      color="neutral"
      :variant="filtersOpen ? 'subtle' : 'ghost'"
      square
      class="justify-center"
      :aria-label="filtersOpen ? 'Hide filters' : 'Show filters'"
      :aria-pressed="filtersOpen"
      @mousedown.prevent
      @click.stop="emit('toggleFilters')"
    />
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";

import type { SearchFilter } from "@/features/explorer/search-filters";
import type { SearchChip } from "@/types/search-chip";

const text = defineModel<string>({ required: true });

const { activeDescendant = undefined } = defineProps<{
  activeDescendant?: string;
  chips: SearchChip[];
  expanded: boolean;
  filtersOpen: boolean;
  listboxId: string;
  placeholder: string;
}>();

const emit = defineEmits<{
  editChip: [filter: SearchFilter];
  focusChange: [focused: boolean];
  keydown: [event: KeyboardEvent];
  removeChip: [filter: SearchFilter];
  toggleFilters: [];
  typed: [];
}>();

const inputRef = ref<HTMLInputElement | null>(null);

// Focuses the input, optionally placing the caret, as after a pick fills it in.
const focus = (caret?: number) => {
  const input = inputRef.value;
  input?.focus();
  if (input && caret !== undefined) {
    input.setSelectionRange(caret, caret);
  }
};

// Backspace only removes the last chip when nothing typed is in its way.
const isCaretAtStart = () => {
  const input = inputRef.value;
  return input?.selectionStart === 0 && input.selectionEnd === 0;
};

defineExpose({ focus, isCaretAtStart });
</script>
