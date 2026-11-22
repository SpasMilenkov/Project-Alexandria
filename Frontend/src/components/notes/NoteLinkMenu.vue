<script setup lang="ts">
import type { Editor } from "@tiptap/vue-3";

import UIcon from "@nuxt/ui/components/Icon.vue";
import { useEditorMenu } from "@nuxt/ui/composables";
import { computed, h, nextTick, onBeforeUnmount, onMounted, ref, toRef } from "vue";

import type { NoteLinkRef } from "@/features/notes/links";

import { useAppToast } from "@/composables/useAppToast";
import { type NoteLinkMenuItem, useNoteLinkSearch } from "@/composables/useNoteLinkSearch";
import { LinkTargetKind } from "@/enums/link-target-kind";
import { noteLinkContent } from "@/features/notes/link-node";
import { createNote } from "@/mutations/notes";

const { editor, fileId, newNoteFolderId } = defineProps<{
  editor: Editor;
  // The note being edited, which the picker never offers.
  fileId: string;
  // Where "New note" from the picker goes; undefined puts it in the Notes folder itself.
  newNoteFolderId: string | undefined;
}>();

const PLUGIN_KEY = "noteLinkMenu";

const toast = useAppToast("files");
const searchTerm = ref("");
const { items } = useNoteLinkSearch(
  searchTerm,
  toRef(() => fileId),
);
const { mutateAsync: createNoteAsync } = createNote();

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

const select = (instance: Editor, range: { from: number; to: number }, item: NoteLinkMenuItem) => {
  if (item.type === "label") return;

  instance.chain().focus().deleteRange(range).run();

  if (item.target) {
    insertLink(instance, { ...item.target, suffix: "" });
    return;
  }

  if (item.createTitle) void createAndLink(instance, item.createTitle);
};

const renderItem = (item: NoteLinkMenuItem, styles: typeof ui) => {
  if (item.type === "label") return [h("span", {}, item.label)];

  const description = item.description
    ? h("span", { class: styles.value.itemDescription() }, item.description)
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
    items,
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
