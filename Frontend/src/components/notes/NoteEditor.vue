<script setup lang="ts">
import type { EditorToolbarProps } from "@nuxt/ui";

import { useEventListener, usePreferredReducedMotion } from "@vueuse/core";
import { computed, nextTick, onBeforeUnmount, ref, useTemplateRef, watch } from "vue";

import type { NoteEditorMode } from "@/features/notes/editor-modes";

import { useAppToast } from "@/composables/useAppToast";
import { type NoteHeading, headingAtLine, lineAtOffset } from "@/features/notes/outline";
import { survivesRoundTrip } from "@/features/notes/round-trip";
import { NOTE_TOOLBAR_ITEMS } from "@/features/notes/toolbar";

// The markdown as stored. The parent keys this component by version, so the check below runs
// once per opened version.
const { source, headings } = defineProps<{ source: string; headings: NoteHeading[] }>();

const emit = defineEmits<{ "active-heading": [index: number | null] }>();

const draft = defineModel<string>({ required: true });
const mode = defineModel<NoteEditorMode>("mode", { required: true });

// Tiptap's packages are Nuxt UI's own dependencies, so the type is reached through its props.
type TiptapEditor = EditorToolbarProps["editor"];

const filesToast = useAppToast("files");
const reducedMotion = usePreferredReducedMotion();

// The rich editor stays invisible until the check has decided which mode the file opens in, so
// a file that falls back to source never flashes up as rich text first.
const isChecked = ref(false);
const wouldRewrite = ref(false);

const sheet = useTemplateRef<HTMLElement>("sheet");
const editorComponent = useTemplateRef<{ editor?: TiptapEditor }>("editor");
const richEditor = computed((): TiptapEditor | null => editorComponent.value?.editor ?? null);

const STARTER_KIT = { underline: false } as const;
const PLACEHOLDER = { mode: "firstLine", placeholder: "Start writing…" } as const;
const HEADING_SELECTOR = ":scope > :is(h1, h2, h3, h4, h5, h6)";

// Headings keep clear of the sticky toolbar when the outline scrolls to them.
const EDITOR_UI = {
  base: "min-h-[60vh] px-4 py-6 sm:px-8 [&_:is(h1,h2,h3,h4,h5,h6)]:scroll-mt-16",
};

// A heading counts as reached once its top passes just below the sticky toolbar.
const REACHED_OFFSET_PX = 72;

watch(richEditor, (instance) => {
  if (!instance || isChecked.value) return;

  isChecked.value = true;

  if (survivesRoundTrip(source, instance.getMarkdown())) return;

  wouldRewrite.value = true;
  mode.value = "source";
});

// Said once, at the point it matters: the first switch to rich text on a file it would rewrite.
let hasNotedRewrite = false;

watch(mode, (current) => {
  if (current !== "rich" || !wouldRewrite.value || hasNotedRewrite) return;

  hasNotedRewrite = true;
  filesToast.info(
    "Formatting may change",
    "Editing in rich text tidies this file into the editor's markdown style.",
  );
});

const sourceLabel = computed(() => {
  if (wouldRewrite.value) return "Opened as markdown to keep this file's formatting";

  return "Editing the markdown source";
});

const renderedHeadings = (): Element[] => {
  const root = richEditor.value?.view.dom;

  if (!root) return [];

  return [...root.querySelectorAll(HEADING_SELECTOR)];
};

// Only top-level headings count, matching how the outline numbers them.
const scrollToHeading = (index: number) => {
  const behavior = reducedMotion.value === "reduce" ? "auto" : "smooth";

  renderedHeadings()[index]?.scrollIntoView({ behavior, block: "start" });
};

// Rich text follows scrolling: the last heading past the toolbar is the one being read. At the
// very bottom the last heading on screen wins, since later ones can never scroll that far up.
const measureScrolledHeading = (): number | null => {
  const container = sheet.value;
  const elements = renderedHeadings();

  if (!container || elements.length === 0) return null;

  const bounds = container.getBoundingClientRect();
  const atBottom = container.scrollTop + container.clientHeight >= container.scrollHeight - 2;
  const reachedLine = bounds.top + REACHED_OFFSET_PX;
  const limit = atBottom ? bounds.bottom : reachedLine;
  let active: number | null = null;

  elements.forEach((element, index) => {
    if (element.getBoundingClientRect().top <= limit) active = index;
  });

  return active;
};

let frame = 0;

const reportScrolledHeading = () => {
  frame = 0;

  if (mode.value === "rich") emit("active-heading", measureScrolledHeading());
};

const scheduleScrollReport = () => {
  if (frame === 0) frame = requestAnimationFrame(reportScrolledHeading);
};

useEventListener(sheet, "scroll", scheduleScrollReport, { passive: true });

onBeforeUnmount(() => {
  if (frame !== 0) cancelAnimationFrame(frame);
});

// Markdown source follows the cursor, because the textarea has no heading elements to measure.
const caretLine = ref(0);

const trackCaret = (event: Event) => {
  const target = event.target;

  if (!(target instanceof HTMLTextAreaElement)) return;

  caretLine.value = lineAtOffset(target.value, target.selectionStart);
};

watch([mode, caretLine, () => headings], () => {
  if (mode.value === "source") emit("active-heading", headingAtLine(headings, caretLine.value));
});

// Edits and mode switches move headings around, so the rich-text position is measured again
// once the new layout is on screen.
watch([mode, richEditor, () => headings], async () => {
  if (mode.value !== "rich") return;

  await nextTick();
  scheduleScrollReport();
});

defineExpose({ scrollToHeading });
</script>

<template>
  <div
    ref="sheet"
    class="frosted-glass glass-surface-strong flex flex-col rounded-xl border border-gray-200/70 dark:border-gray-700/70"
  >
    <div
      class="frosted-glass glass-surface-strong sticky top-0 z-10 flex min-h-10 items-center rounded-t-xl border-b border-gray-200/70 px-2 py-1 dark:border-gray-700/70"
    >
      <UEditorToolbar
        v-if="mode === 'rich' && richEditor"
        :editor="richEditor"
        :items="NOTE_TOOLBAR_ITEMS"
        class="min-w-0 flex-1 overflow-x-auto"
      />

      <span v-else class="min-w-0 flex-1 px-2 text-xs text-gray-600 dark:text-gray-400">
        {{ sourceLabel }}
      </span>
    </div>

    <UEditor
      v-if="mode === 'rich'"
      ref="editor"
      v-model="draft"
      content-type="markdown"
      :starter-kit="STARTER_KIT"
      :mention="false"
      :placeholder="PLACEHOLDER"
      :class="{ invisible: !isChecked }"
      :ui="EDITOR_UI"
    />

    <UTextarea
      v-else
      v-model="draft"
      autoresize
      :rows="24"
      variant="none"
      aria-label="Markdown source"
      class="w-full"
      :ui="{ base: 'min-h-[60vh] px-4 py-6 font-mono text-sm leading-6 sm:px-8' }"
      @click="trackCaret"
      @keyup="trackCaret"
      @focus="trackCaret"
      @select="trackCaret"
    />
  </div>
</template>
