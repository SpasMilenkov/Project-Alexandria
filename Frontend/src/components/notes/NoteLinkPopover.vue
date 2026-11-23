<script setup lang="ts">
import type { Editor } from "@tiptap/vue-3";

import { useQuery } from "@pinia/colada";
import { BubbleMenu } from "@tiptap/vue-3/menus";
import { computed, onBeforeUnmount, ref, shallowRef, watch } from "vue";

import NoteTimestampInput from "@/components/notes/NoteTimestampInput.vue";
import { useLinkMomentLimit } from "@/composables/useLinkMomentLimit";
import { useOpenLinkTarget } from "@/composables/useOpenLinkTarget";
import { LinkTargetKind } from "@/enums/link-target-kind";
import { pageSuffix, pdfPageOf, startSecondsOf, startSuffix } from "@/features/notes/deep-links";
import { NOTE_EMBED_NODE, noteEmbedContent } from "@/features/notes/embed-node";
import { NOTE_LINK_NODE, noteLinkContent } from "@/features/notes/link-node";
import { momentOfMime } from "@/features/notes/link-picker";
import { linkKindNoun, linkTargetIcon, linkTargetName } from "@/features/notes/link-targets";
import { type NoteLinkRef, labelSourceOf, labelText } from "@/features/notes/links";
import { clampTimestamp } from "@/features/notes/timestamp-fields";
import { getLinkTarget } from "@/queries/links";
import { usePlayerStore } from "@/stores/stream-player";
import { formatPlaybackTime } from "@/utils/date-formatters";

// Edits the link or embed the cursor has selected: its label or caption, the moment it points
// at, and whether it shows as a chip or as the item itself. Every change rewrites the node, so
// the markdown follows on its own.
const { editor } = defineProps<{ editor: Editor }>();

const PLUGIN_KEY = "noteLinkPopover";

const player = usePlayerStore();
const { open } = useOpenLinkTarget();

interface SelectedLink {
  pos: number;
  size: number;
  isEmbed: boolean;
  ref: NoteLinkRef;
  // The label or caption's markdown source; null for a chip showing the target's live name.
  text: string | null;
}

interface NodeLike {
  type: { name: string };
  attrs: Record<string, unknown>;
  nodeSize: number;
}

const readSelected = (): SelectedLink | null => {
  const { selection } = editor.state;
  const node = "node" in selection ? (selection.node as NodeLike) : null;
  const name = node?.type.name;

  if (!node || (name !== NOTE_LINK_NODE && name !== NOTE_EMBED_NODE)) return null;

  const isEmbed = name === NOTE_EMBED_NODE;
  const text = isEmbed ? node.attrs.caption : node.attrs.label;

  return {
    isEmbed,
    pos: selection.from,
    ref: {
      id: String(node.attrs.id ?? ""),
      kind: Number(node.attrs.kind) as LinkTargetKind,
      suffix: String(node.attrs.suffix ?? ""),
    },
    size: node.nodeSize,
    text: typeof text === "string" ? text : null,
  };
};

const selected = shallowRef<SelectedLink | null>(readSelected());

const syncSelected = () => {
  selected.value = readSelected();
};

editor.on("transaction", syncSelected);

onBeforeUnmount(() => {
  editor.off("transaction", syncSelected);
});

const isLinkNode = ({ state }: { state: Editor["state"] }): boolean => {
  const { selection } = state;

  if (!("node" in selection) || !editor.isEditable) return false;

  const name = (selection.node as NodeLike).type.name;

  return name === NOTE_LINK_NODE || name === NOTE_EMBED_NODE;
};

const selectedKind = computed(() => selected.value?.ref.kind ?? LinkTargetKind.File);

const { data: target } = useQuery(() => ({
  ...getLinkTarget({ id: selected.value?.ref.id ?? "", kind: selectedKind.value }),
  enabled: selected.value !== null,
}));

const moment = computed(() => {
  if (!target.value || target.value.isEncrypted) return null;

  return momentOfMime(target.value.mimeType);
});

const name = computed(() => {
  const live = target.value ? linkTargetName(target.value) : null;

  if (live) return live;

  return `Linked ${linkKindNoun(selectedKind.value)}`;
});

const icon = computed(() => linkTargetIcon(selectedKind.value, target.value));

const textLabel = computed(() => {
  if (selected.value?.isEmbed) return "Caption";

  return "Label";
});

const textPlaceholder = computed(() => {
  if (selected.value?.isEmbed) return "No caption";

  return name.value;
});

const { isPreviewCut, limit } = useLinkMomentLimit(
  computed(() => selected.value?.ref.id ?? null),
  moment,
);

const pageHelp = computed(() => {
  if (!isPreviewCut.value) return undefined;

  return `Links open the preview, which has the first ${limit.value} pages.`;
});

const pageCountLabel = computed(() => {
  if (moment.value !== "page" || limit.value === null) return null;

  return `of ${limit.value}`;
});

const playingSeconds = computed(() => {
  const id = selected.value?.ref.id;

  if (!id || player.activeFile?.fileId !== id) return null;

  return Math.floor(player.currentTime);
});

// The fields hold what is being typed and start over whenever another node is selected or the
// selected one changes underneath them.
const textDraft = ref("");
const timeDraft = ref(0);
const pageDraft = ref<number | null>(null);

const resetDrafts = () => {
  const current = selected.value;
  const suffix = current?.ref.suffix ?? "";

  textDraft.value = current?.text ? labelText(current.text) : "";
  timeDraft.value = startSecondsOf(suffix) ?? 0;
  pageDraft.value = pdfPageOf(suffix);
};

watch(
  () => {
    const current = selected.value;

    if (!current) return null;

    return `${current.pos}|${current.isEmbed}|${current.ref.suffix}|${current.text}`;
  },
  resetDrafts,
  { immediate: true },
);

const setAttributes = (changes: Record<string, unknown>) => {
  const current = selected.value;

  if (!current) return;

  editor
    .chain()
    .command(({ tr }) => {
      for (const [key, value] of Object.entries(changes)) tr.setNodeAttribute(current.pos, key, value);

      return true;
    })
    .run();
};

const commitText = () => {
  const current = selected.value;

  if (!current) return;

  const typed = textDraft.value.trim();

  if (current.isEmbed) {
    const caption = typed === "" ? "" : labelSourceOf(typed);

    if (caption !== (current.text ?? "")) setAttributes({ caption });
    return;
  }

  const label = typed === "" ? null : labelSourceOf(typed);

  if (label !== current.text) setAttributes({ label });
};

// The start of the file is where a link without a moment opens anyway, so it writes none.
const draftSuffix = (): string => {
  if (moment.value === "page") {
    const page = Math.min(pageDraft.value ?? 1, limit.value ?? Number.POSITIVE_INFINITY);

    return page > 1 ? pageSuffix(page) : "";
  }

  const seconds = clampTimestamp(timeDraft.value, limit.value);

  return seconds > 0 ? startSuffix(seconds) : "";
};

const commitMoment = () => {
  const current = selected.value;

  if (!current || !moment.value) return;

  const suffix = draftSuffix();

  if (suffix !== current.ref.suffix) setAttributes({ suffix });
};

const useCurrentTime = () => {
  if (playingSeconds.value === null) return;

  timeDraft.value = playingSeconds.value;
  commitMoment();
};

const backToEditor = () => {
  editor.commands.focus();
};

const commitAndReturn = (commit: () => void) => {
  commit();
  backToEditor();
};

const cancelEdit = () => {
  resetDrafts();
  backToEditor();
};

// Chips and embeds share the same markdown slot, `[text](...)` and `![text](...)`, so the label
// and the caption carry over.
const replacementFor = (current: SelectedLink) => {
  if (!current.isEmbed) return noteEmbedContent(current.ref, current.text ?? "");

  // An embed without a caption becomes a chip showing the target's live name.
  if (current.text === "" || current.text === null) return noteLinkContent(current.ref, null);

  return noteLinkContent(current.ref, current.text);
};

const toggleKind = () => {
  const current = selected.value;

  if (!current) return;

  const replacement = replacementFor(current);

  editor
    .chain()
    .focus()
    .insertContentAt({ from: current.pos, to: current.pos + current.size }, replacement)
    .setNodeSelection(current.pos)
    .run();
};

const toggleLabel = computed(() => {
  if (selected.value?.isEmbed) return "Show as link";

  return "Show as embed";
});

const toggleIcon = computed(() => {
  if (selected.value?.isEmbed) return "i-lucide-link";

  return "i-lucide-panel-top";
});

const openTarget = () => {
  open(target.value, selected.value?.ref.suffix ?? "");
};

const remove = () => {
  const current = selected.value;

  if (!current) return;

  editor
    .chain()
    .focus()
    .deleteRange({ from: current.pos, to: current.pos + current.size })
    .run();
};
</script>

<template>
  <BubbleMenu
    :editor="editor"
    :plugin-key="PLUGIN_KEY"
    :should-show="isLinkNode"
    :options="{ flip: true, offset: 8, placement: 'bottom-start', shift: { padding: 8 } }"
  >
    <div
      v-if="selected"
      class="flex w-72 flex-col gap-3 rounded-md bg-default p-3 shadow-lg ring ring-default"
      @keydown.esc.stop="cancelEdit"
    >
      <div class="flex min-w-0 items-center gap-2">
        <UIcon :name="icon" class="h-4 w-4 shrink-0 text-gray-500 dark:text-gray-500" />
        <span class="min-w-0 flex-1 truncate text-sm font-medium text-gray-900 dark:text-gray-100">
          {{ name }}
        </span>
      </div>

      <UFormField :label="textLabel" size="sm">
        <UInput
          v-model="textDraft"
          :placeholder="textPlaceholder"
          size="sm"
          class="w-full"
          @blur="commitText"
          @keydown.enter.prevent="commitAndReturn(commitText)"
        />
      </UFormField>

      <UFormField
        v-if="moment === 'page'"
        label="Page"
        :help="pageHelp"
        size="sm"
      >
        <div class="flex items-center gap-2">
          <UInputNumber
            v-model="pageDraft"
            :min="1"
            :max="limit ?? undefined"
            placeholder="1"
            size="sm"
            class="w-28"
            @blur="commitMoment"
            @keydown.enter.prevent="commitAndReturn(commitMoment)"
          />

          <span v-if="pageCountLabel" class="text-xs tabular-nums text-gray-500 dark:text-gray-500">
            {{ pageCountLabel }}
          </span>
        </div>
      </UFormField>

      <UFormField v-else-if="moment === 'time'" label="Start at" size="sm">
        <div class="flex items-center justify-between gap-2">
          <NoteTimestampInput
            v-model="timeDraft"
            :limit="limit"
            @commit="commitMoment"
            @submit="commitAndReturn(commitMoment)"
          />

          <UTooltip v-if="playingSeconds !== null" text="Use where it's playing">
            <UButton
              icon="i-lucide-timer"
              :label="formatPlaybackTime(playingSeconds)"
              size="sm"
              color="neutral"
              variant="outline"
              class="shrink-0 tabular-nums"
              @click="useCurrentTime"
            />
          </UTooltip>
        </div>
      </UFormField>

      <div class="flex items-center gap-1 border-t border-gray-200/70 pt-2 dark:border-gray-700/70">
        <UButton
          icon="i-lucide-arrow-up-right"
          label="Open"
          size="sm"
          color="neutral"
          variant="ghost"
          :disabled="!target"
          @click="openTarget"
        />

        <UButton
          :icon="toggleIcon"
          :label="toggleLabel"
          size="sm"
          color="neutral"
          variant="ghost"
          @click="toggleKind"
        />

        <span class="ml-auto">
          <UTooltip text="Remove">
            <UButton
              icon="i-lucide-trash-2"
              size="sm"
              color="error"
              variant="ghost"
              square
              class="justify-center"
              aria-label="Remove"
              @click="remove"
            />
          </UTooltip>
        </span>
      </div>
    </div>
  </BubbleMenu>
</template>
