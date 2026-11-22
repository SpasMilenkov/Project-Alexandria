<script setup lang="ts">
import type { EditorCustomHandlers } from "@nuxt/ui";

import { type Editor, VueNodeViewRenderer } from "@tiptap/vue-3";
import { useDebounceFn, useEventListener, usePreferredReducedMotion } from "@vueuse/core";
import { computed, nextTick, onBeforeUnmount, ref, useTemplateRef, watch } from "vue";

import type { NoteEditorMode } from "@/features/notes/editor-modes";

import NoteLinkChip from "@/components/notes/NoteLinkChip.vue";
import NoteLinkMenu from "@/components/notes/NoteLinkMenu.vue";
import { useAppToast } from "@/composables/useAppToast";
import { NoteLinkNode, noteLinkContent } from "@/features/notes/link-node";
import { pastedLinkRef } from "@/features/notes/links";
import { type NoteHeading, headingAtLine, lineAtOffset } from "@/features/notes/outline";
import { survivesRoundTrip } from "@/features/notes/round-trip";
import { NOTE_LINK_TOOL, NOTE_TOOLBAR_ITEMS } from "@/features/notes/toolbar";
import { logger } from "@/utils/logger";

// The markdown as stored. The parent keys this component by version, so the check below runs
// once per opened version.
const { source, headings, fileId, newNoteFolderId } = defineProps<{
  source: string;
  headings: NoteHeading[];
  fileId: string;
  // Where a note created from the link picker goes; undefined means the Notes folder itself.
  newNoteFolderId: string | undefined;
}>();

const emit = defineEmits<{ "active-heading": [index: number | null] }>();

const draft = defineModel<string>({ required: true });
const mode = defineModel<NoteEditorMode>("mode", { required: true });

const filesToast = useAppToast("files");
const reducedMotion = usePreferredReducedMotion();

// The rich editor stays invisible until the check has decided which mode the file opens in, so
// a file that falls back to source never flashes up as rich text first.
const isChecked = ref(false);
const wouldRewrite = ref(false);

const sheet = useTemplateRef<HTMLElement>("sheet");
const editorComponent = useTemplateRef<{ editor?: Editor }>("editor");
const richEditor = computed((): Editor | null => editorComponent.value?.editor ?? null);

const STARTER_KIT = { underline: false } as const;
const PLACEHOLDER = { mode: "firstLine", placeholder: "Start writing…" } as const;
const HEADING_SELECTOR = ":scope > :is(h1, h2, h3, h4, h5, h6)";

// Headings keep clear of the sticky toolbar when the outline scrolls to them.
const EDITOR_UI = {
  base: "min-h-[60vh] px-4 py-6 sm:px-8 [&_:is(h1,h2,h3,h4,h5,h6)]:scroll-mt-16",
};

// A heading counts as reached once its top passes just below the sticky toolbar.
const REACHED_OFFSET_PX = 72;

// Turning the document into markdown costs about 2 ms at 100 paragraphs and 9 ms at 400, so it
// waits for a pause in typing instead of running on every keystroke. Steady typing still brings
// the outline, links and word count up to date this often.
const SYNC_PAUSE_MS = 300;
const SYNC_MAX_WAIT_MS = 2000;

// ID links read and write their markdown in this file's node; the chip is the only UI part.
const EXTENSIONS = [
  NoteLinkNode.extend({ addNodeView: () => VueNodeViewRenderer(NoteLinkChip) }),
];

const inCode = (instance: Editor) => instance.isActive("code") || instance.isActive("codeBlock");

// The toolbar's link button types the same "[[" that opens the picker.
const HANDLERS: EditorCustomHandlers = {
  [NOTE_LINK_TOOL]: {
    canExecute: (instance) => !inCode(instance),
    execute: (instance) => instance.chain().focus().insertContent("[["),
    isActive: () => false,
    isDisabled: inCode,
  },
};

// Pasting a link to something in the library, or the address of its page here, makes a chip.
// Everything else, other sites included, pastes as usual.
const handlePaste = (_view: unknown, event: ClipboardEvent): boolean => {
  const instance = richEditor.value;
  const link = pastedLinkRef(
    event.clipboardData?.getData("text/plain") ?? "",
    window.location.origin,
  );

  if (!instance || !link || inCode(instance)) return false;

  instance.chain().focus().insertContent(noteLinkContent(link)).run();

  return true;
};

const EDITOR_PROPS = { handlePaste };

// The rich editor is handed the text once, when it mounts, and never again: writing the model
// back would make it serialize a second time just to compare. It mounts again on every switch
// back from source mode, so it starts from whatever the source edits left.
const richStart = ref(draft.value);
let hasUnsyncedEdit = false;

// Brings the model up to date with the rich editor now. The page calls it before anything that
// needs the exact text: saving, leaving, copying.
const syncDraft = () => {
  const instance = richEditor.value;

  if (!hasUnsyncedEdit || !instance) return;

  hasUnsyncedEdit = false;
  draft.value = instance.getMarkdown();
};

const scheduleSync = useDebounceFn(syncDraft, SYNC_PAUSE_MS, { maxWait: SYNC_MAX_WAIT_MS });

const noteEdit = () => {
  hasUnsyncedEdit = true;
  void scheduleSync();
};

watch(richEditor, (instance) => {
  if (!instance) return;

  // UEditor's own update handler serializes the whole document on every keystroke to emit its
  // model. Nothing listens to that model here, so the handler is swapped for one that only notes
  // the edit and leaves the markdown to the pause above. UEditor overrides an `onUpdate` passed
  // to it, so removing its handler is the only way to skip that work.
  const ownUpdate = instance.options.onUpdate;

  if (ownUpdate) {
    instance.off("update", ownUpdate);
  } else {
    // Typing still works without the swap, it just serializes on every keystroke again.
    logger.warn("UEditor no longer sets onUpdate; the note editor serializes on every keystroke");
  }

  instance.on("update", noteEdit);

  if (isChecked.value) return;

  isChecked.value = true;

  if (survivesRoundTrip(source, instance.getMarkdown())) return;

  wouldRewrite.value = true;
  mode.value = "source";
});

// Said once, at the point it matters: the first switch to rich text on a file it would rewrite.
// The page reads it too, so the first save can say the formatting was tidied.
const rewroteFormatting = ref(false);

// Runs before the switch renders, while the rich editor is still mounted to read from.
watch(mode, (current, previous) => {
  if (previous === "rich") syncDraft();
  if (current === "rich") richStart.value = draft.value;
});

watch(mode, (current) => {
  if (current !== "rich" || !wouldRewrite.value || rewroteFormatting.value) return;

  rewroteFormatting.value = true;
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

defineExpose({ rewroteFormatting, scrollToHeading, syncDraft });
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

    <template v-if="mode === 'rich'">
      <UEditor
        ref="editor"
        :model-value="richStart"
        content-type="markdown"
        :starter-kit="STARTER_KIT"
        :mention="false"
        :placeholder="PLACEHOLDER"
        :extensions="EXTENSIONS"
        :handlers="HANDLERS"
        :editor-props="EDITOR_PROPS"
        :class="{ invisible: !isChecked }"
        :ui="EDITOR_UI"
      />

      <!-- Tiptap creates its editor on mount, so the menu waits for the instance. -->
      <NoteLinkMenu
        v-if="richEditor"
        :editor="richEditor"
        :file-id="fileId"
        :new-note-folder-id="newNoteFolderId"
      />
    </template>

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
