<script setup lang="ts">
import type { Editor } from "@tiptap/vue-3";

import UIcon from "@nuxt/ui/components/Icon.vue";
import { useEditorMenu } from "@nuxt/ui/composables";
import { useEventListener } from "@vueuse/core";
import { computed, h, nextTick, onBeforeUnmount, onMounted, ref, toRef, watch } from "vue";

import type { NoteLinkRef } from "@/features/notes/links";

import { useAppToast } from "@/composables/useAppToast";
import { useLinkMomentLimit } from "@/composables/useLinkMomentLimit";
import { type NoteLinkMenuItem, useNoteLinkSearch } from "@/composables/useNoteLinkSearch";
import { LinkTargetKind } from "@/enums/link-target-kind";
import { noteEmbedContent } from "@/features/notes/embed-node";
import { noteLinkContent } from "@/features/notes/link-node";
import {
  type LinkMoment,
  isPastPreview,
  momentLabel,
  momentStep,
  normalizeTypedSuffix,
  readMomentInput,
  splitPickerQuery,
} from "@/features/notes/link-picker";
import { createNote } from "@/mutations/notes";
import { usePlayerStore } from "@/stores/stream-player";
import { PREVIEW_PAGE_LIMIT } from "@/utils/files/mime-catalog";

const { editor, fileId, newNoteFolderId } = defineProps<{
  editor: Editor;
  // The note being edited, which the picker never offers.
  fileId: string;
  // Where "New note" from the picker goes; undefined puts it in the Notes folder itself.
  newNoteFolderId: string | undefined;
}>();

const PLUGIN_KEY = "noteLinkMenu";

const toast = useAppToast("files");
const player = usePlayerStore();
const searchTerm = ref("");

// The name searched for, without a `?t=`/`#page=` typed after it.
const query = computed(() => splitPickerQuery(searchTerm.value));

const { items } = useNoteLinkSearch(
  toRef(() => query.value.name),
  toRef(() => fileId),
);
const { mutateAsync: createNoteAsync } = createNote();

interface Completion {
  item: NoteLinkMenuItem;
  moment: LinkMoment;
  name: string;
}

// Set once Tab completes the name of a file a link can point into: the picker then waits for a
// start time or a page, the way an editor's suggestions fill in a name and then its arguments.
const completion = ref<Completion | null>(null);

// What was typed after the completed name, or null once the name itself was edited.
const momentText = computed(() => {
  const done = completion.value;

  if (!done || !searchTerm.value.startsWith(done.name)) return null;

  return searchTerm.value.slice(done.name.length);
});

watch(momentText, (text) => {
  if (text === null) completion.value = null;
});

// Whole seconds, so the step re-renders once a second while the file plays.
const playingSeconds = computed(() => {
  const id = completion.value?.item.target?.id;

  if (!id || player.activeFile?.fileId !== id) return null;

  return Math.floor(player.currentTime);
});

// The recording's length or the PDF's page count, so the step can say a moment is past the end.
const { limit } = useLinkMomentLimit(
  computed(() => completion.value?.item.target?.id ?? null),
  computed(() => completion.value?.moment ?? null),
);

const stepItems = computed((): NoteLinkMenuItem[] | null => {
  const done = completion.value;
  const text = momentText.value;

  if (!done || text === null) return null;

  const input = readMomentInput(done.moment, text, playingSeconds.value, limit.value);
  const step = momentStep(done.moment, input, playingSeconds.value, limit.value);

  return [
    { label: step.hint, type: "label" },
    { ...done.item, blocked: step.blocked, description: step.description, suffix: step.suffix },
  ];
});

// A `?t=1:30` or `#page=4` typed after the name goes with every file it fits.
const searchItems = computed((): NoteLinkMenuItem[] => {
  const typed = query.value.typedSuffix;

  if (typed === null) return items.value;

  return items.value.map((item) => {
    if (item.type === "label" || !item.target) return item;

    const suffix = normalizeTypedSuffix(item.moment ?? null, typed);

    if (suffix === null) return item;

    if (isPastPreview(suffix)) {
      return {
        ...item,
        blocked: true,
        description: `Past the preview's last page (${PREVIEW_PAGE_LIMIT})`,
        suffix,
      };
    }

    return { ...item, description: momentLabel(suffix) ?? undefined, suffix };
  });
});

const menuItems = computed(() => stepItems.value ?? searchItems.value);

// The menu picks the highlighted item on Enter and Tab alike, so the picker notes which key it
// was. A click clears it.
let pickedWithTab = false;

useEventListener(
  window,
  "keydown",
  (event: KeyboardEvent) => {
    pickedWithTab = event.key === "Tab";
  },
  { capture: true },
);

useEventListener(
  window,
  "pointerdown",
  () => {
    pickedWithTab = false;
  },
  { capture: true },
);

// The menu calls each slot with an optional extra class, the way a Tailwind Variants theme does.
const slot =
  (base: string) =>
  (props?: { class?: string }): string =>
    props?.class ? `${base} ${props.class}` : base;

// Nuxt UI's mention menu look at its default size, written out here because its generated theme
// files aren't public API. A little wider than that menu so file names fit.
const MENU_UI = {
  content: slot(
    "flex max-h-96 min-w-64 max-w-80 flex-col overflow-hidden rounded-md bg-default shadow-lg ring ring-default data-[state=open]:animate-[scale-in_100ms_var(--ease-out)]",
  ),
  group: slot("isolate p-1"),
  item: slot(
    "group relative flex w-full items-start gap-1.5 p-1.5 text-sm text-default outline-none select-none transition-colors before:absolute before:inset-px before:z-[-1] before:rounded-md before:transition-colors data-disabled:cursor-not-allowed data-disabled:opacity-75 data-highlighted:not-data-disabled:text-highlighted data-highlighted:not-data-disabled:before:bg-elevated/50",
  ),
  itemDescription: slot("truncate text-muted"),
  itemLabel: slot("truncate"),
  itemLeadingIcon: slot(
    "flex size-5 shrink-0 items-center justify-center text-base text-dimmed transition-colors group-data-highlighted:not-group-data-disabled:text-default",
  ),
  itemWrapper: slot("flex min-w-0 flex-1 flex-col text-start"),
  label: slot("flex w-full items-center gap-1.5 p-1.5 text-xs font-semibold text-highlighted"),
  separator: slot("-mx-1 my-1 h-px bg-border"),
  viewport: slot("relative flex-1 scroll-py-1 divide-y divide-default overflow-y-auto"),
};

const ui = computed(() => MENU_UI);

// A trailing space lets typing carry on after the chip.
const insertLink = (instance: Editor, link: NoteLinkRef) => {
  instance
    .chain()
    .focus()
    .insertContent([noteLinkContent(link), { text: " ", type: "text" }])
    .run();
};

const createAndLink = async (instance: Editor, title: string) => {
  try {
    const created = await createNoteAsync({ directoryId: newNoteFolderId ?? null, title });

    if (instance.isDestroyed) return;

    insertLink(instance, { id: created.fileId, kind: LinkTargetKind.File, suffix: "" });
  } catch (error) {
    toast.error("Couldn't create the note", error);
  }
};

const insertEmbed = (instance: Editor, link: NoteLinkRef) => {
  instance.chain().focus().insertContent(noteEmbedContent(link)).run();
};

// "![[" is "[[" with the "!" of a markdown image in front, so the picked item is embedded.
const typedEmbed = (instance: Editor, from: number): boolean =>
  from > 0 && instance.state.doc.textBetween(from - 1, from) === "!";

// Fills in the full name and leaves the picker open on it, asking for the moment.
const complete = (
  instance: Editor,
  range: { from: number; to: number },
  item: NoteLinkMenuItem,
  moment: LinkMoment,
) => {
  instance
    .chain()
    .focus()
    .insertContentAt(range, { text: `[[${item.label} `, type: "text" })
    .run();

  completion.value = { item, moment, name: item.label };
};

const select = (instance: Editor, range: { from: number; to: number }, item: NoteLinkMenuItem) => {
  const withTab = pickedWithTab;
  pickedWithTab = false;

  if (item.type === "label" || item.blocked) return;

  // A moment typed as `?t=`/`#page=` is already there, so Tab inserts it like Enter.
  if (withTab && item.moment && item.target && !item.suffix && !completion.value) {
    complete(instance, range, item, item.moment);
    return;
  }

  completion.value = null;

  const embed = typedEmbed(instance, range.from);
  const from = embed ? range.from - 1 : range.from;
  const suffix = item.suffix ?? "";

  instance.chain().focus().deleteRange({ from, to: range.to }).run();

  if (item.target && embed) {
    insertEmbed(instance, { ...item.target, suffix });
    return;
  }

  if (item.target) {
    insertLink(instance, { ...item.target, suffix });
    return;
  }

  // A note made from the picker is empty, so there is nothing to embed yet; it gets a link.
  if (item.createTitle) void createAndLink(instance, item.createTitle);
};

const renderItem = (item: NoteLinkMenuItem, styles: typeof ui) => {
  if (item.type === "label") return [h("span", {}, item.label)];

  const descriptionClass = item.blocked ? "truncate text-error" : styles.value.itemDescription();
  const description = item.description
    ? h("span", { class: descriptionClass }, item.description)
    : null;

  return [
    h(UIcon, { class: styles.value.itemLeadingIcon(), name: item.icon ?? "i-lucide-file" }),
    h("span", { class: styles.value.itemWrapper() }, [
      h("span", { class: styles.value.itemLabel() }, item.label),
      description,
    ]),
  ];
};

let menu: ReturnType<typeof useEditorMenu> | null = null;

onMounted(async () => {
  await nextTick();

  if (editor.isDestroyed) return;

  menu = useEditorMenu<NoteLinkMenuItem>({
    char: "[[",
    editor,
    ignoreFilter: true,
    items: menuItems,
    limit: 30,
    onSelect: select,
    pluginKey: PLUGIN_KEY,
    renderItem,
    searchTerm,
    // Note names have spaces, and a link can start mid-sentence, right after "(" for example.
    // Inside code "[[" is just text.
    suggestion: {
      allow: () => !editor.isActive("code") && !editor.isActive("codeBlock"),
      allowSpaces: true,
      allowedPrefixes: null,
    },
    ui,
  });

  editor.registerPlugin(menu.plugin);
});

onBeforeUnmount(() => {
  menu?.destroy();
  menu = null;

  if (!editor.isDestroyed) editor.unregisterPlugin(PLUGIN_KEY);
});
</script>

<template>
  <span hidden />
</template>
