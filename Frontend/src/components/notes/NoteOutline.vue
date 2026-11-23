<script setup lang="ts">
import { nextTick, useTemplateRef, watch } from "vue";

import type { NoteHeading } from "@/features/notes/outline";

const { headings, canJump, activeIndex } = defineProps<{
  headings: NoteHeading[];
  // Jumping needs the rendered headings, which only the rich editor has.
  canJump: boolean;
  // The heading at the reader's position, or null above the first one.
  activeIndex: number | null;
}>();

const emit = defineEmits<{ select: [index: number]; collapse: [] }>();

const list = useTemplateRef<HTMLOListElement>("list");

const INDENT_BY_LEVEL: Record<number, string> = {
  1: "pl-2",
  2: "pl-4",
  3: "pl-6",
};

const DEEPEST_INDENT = "pl-8";

const indentFor = (level: number): string => INDENT_BY_LEVEL[level] ?? DEEPEST_INDENT;

const toneFor = (heading: NoteHeading, index: number): string => {
  if (index === activeIndex) return "bg-primary/10 font-medium text-primary";
  if (heading.text.length === 0) return "text-gray-500 dark:text-gray-500";
  if (heading.level === 1) return "font-medium text-gray-900 dark:text-gray-100";

  return "text-gray-600 dark:text-gray-400";
};

// A long outline keeps the current entry in view; "nearest" leaves it alone when it already is.
watch(
  () => activeIndex,
  async (index) => {
    if (index === null) return;

    await nextTick();
    list.value?.children[index]?.scrollIntoView({ block: "nearest" });
  },
);
</script>

<template>
  <!-- Marginalia on the desk: no card of its own, so the sheet stays the one surface. -->
  <nav aria-label="Outline" class="group flex min-h-0 flex-col gap-2 py-1">
    <div class="flex shrink-0 items-center justify-between gap-2 pl-2">
      <h2 class="text-xs font-semibold tracking-wide text-gray-600 uppercase dark:text-gray-400">
        Outline
      </h2>

      <UTooltip text="Hide outline">
        <UButton
          icon="i-lucide-minus"
          size="xs"
          color="neutral"
          variant="ghost"
          square
          class="justify-center opacity-0 transition-opacity duration-150 group-hover:opacity-100 motion-reduce:transition-none group-focus-within:opacity-100"
          aria-label="Hide outline"
          @click="emit('collapse')"
        />
      </UTooltip>
    </div>

    <p v-if="headings.length === 0" class="px-2 text-sm text-gray-600 dark:text-gray-400">
      Headings you add show up here.
    </p>

    <!-- The list grows with its headings and scrolls on its own once it reaches the desk's height. -->
    <ol
      v-else
      ref="list"
      class="-mx-1 flex min-h-0 flex-col overflow-y-auto overscroll-contain px-1"
    >
      <li v-for="(heading, index) in headings" :key="index">
        <button
          type="button"
          class="w-full truncate rounded-md py-1 pr-2 text-left text-sm enabled:hover:bg-gray-500/10 disabled:cursor-default"
          :class="[indentFor(heading.level), toneFor(heading, index)]"
          :disabled="!canJump"
          :title="heading.text"
          :aria-current="index === activeIndex ? 'location' : undefined"
          @click="emit('select', index)"
        >
          {{ heading.text || "Untitled heading" }}
        </button>
      </li>
    </ol>

    <p
      v-if="headings.length > 0 && !canJump"
      class="shrink-0 px-2 text-xs text-gray-500 dark:text-gray-500"
    >
      Switch to rich text to jump to a heading.
    </p>
  </nav>
</template>
