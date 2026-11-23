<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { NodeViewWrapper, nodeViewProps } from "@tiptap/vue-3";
import { computed } from "vue";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import NoteEmbedMedia from "@/components/notes/NoteEmbedMedia.vue";
import NoteEmbedPreview from "@/components/notes/NoteEmbedPreview.vue";
import { useOpenLinkTarget } from "@/composables/useOpenLinkTarget";
import { deepLinkLabel, pdfPageOf, startSecondsOf } from "@/features/notes/deep-links";
import { type NoteEmbedAttributes } from "@/features/notes/embed-node";
import { embedView } from "@/features/notes/embeds";
import {
  OPEN_CLICK_HINT,
  isOpenClick,
  linkKindNoun,
  linkStateLabel,
  linkTargetIcon,
  linkTargetName,
} from "@/features/notes/link-targets";
import { labelText } from "@/features/notes/links";
import { getLinkTarget } from "@/queries/links";
import { getFileTypeReadable } from "@/utils/files/mimetype";

// Tiptap's node view renderer only accepts components declaring its runtime props object.
// oxlint-disable-next-line vue/define-props-declaration
const { editor, getPos, node, selected } = defineProps(nodeViewProps);

const attrs = computed(() => node.attrs as unknown as NoteEmbedAttributes);

const { data: target, isPending } = useQuery(() =>
  getLinkTarget({ id: attrs.value.id, kind: attrs.value.kind }),
);

const { open } = useOpenLinkTarget();

const view = computed(() => embedView(target.value, isPending.value));
const caption = computed(() => labelText(attrs.value.caption).trim());

const name = computed(() => {
  const live = target.value ? linkTargetName(target.value) : null;

  if (live) return live;

  return `Unavailable ${linkKindNoun(attrs.value.kind)}`;
});

const icon = computed(() => linkTargetIcon(attrs.value.kind, target.value));
const stateLabel = computed(() => linkStateLabel(target.value));
const deepLabel = computed(() => deepLinkLabel(attrs.value.suffix));
const startSeconds = computed(() => startSecondsOf(attrs.value.suffix));
const page = computed(() => pdfPageOf(attrs.value.suffix));

const cardDetail = computed(() => {
  if (stateLabel.value) return stateLabel.value;
  if (target.value?.isEncrypted) return "Encrypted, no preview";
  if (target.value?.mimeType) return getFileTypeReadable(target.value.mimeType, target.value.name ?? undefined);

  return linkKindNoun(attrs.value.kind);
});

const canOpen = computed(() => view.value !== "unavailable" && view.value !== "pending");

const follow = () => {
  if (canOpen.value) open(target.value, attrs.value.suffix);
};

// A plain click on the card selects the embed for its edit popover, like a link chip.
const onCardClick = (event: MouseEvent) => {
  if (isOpenClick(event)) {
    follow();
    return;
  }

  const pos = getPos();

  if (typeof pos === "number") editor.chain().focus().setNodeSelection(pos).run();
};

// Without the suffix, so a timestamped embed opens the file instead of playing from its start.
const openFile = () => {
  if (canOpen.value) open(target.value, "");
};
</script>

<template>
  <NodeViewWrapper as="span" class="my-2 block">
    <span
      contenteditable="false"
      class="block overflow-hidden rounded-lg border bg-gray-500/5 transition-shadow duration-150"
      :class="[
        view === 'unavailable'
          ? 'border-dashed border-gray-300 dark:border-gray-600'
          : 'border-gray-200/70 dark:border-gray-700/70',
        { 'ring-2 ring-primary': selected },
      ]"
    >
      <span v-if="view === 'pending'" class="flex h-16 items-center justify-center text-gray-500">
        <BlockSpinner :size="18" aria-label="Loading embed" />
      </span>

      <template v-else-if="(view === 'image' || view === 'pdf') && target?.versionId && target.mimeType">
        <span class="flex items-center gap-2 border-b border-gray-200/70 px-3 py-1.5 dark:border-gray-700/70">
          <UIcon :name="icon" class="h-4 w-4 shrink-0 text-gray-500 dark:text-gray-500" aria-hidden="true" />

          <span class="min-w-0 flex-1 truncate text-sm text-gray-900 dark:text-gray-100">
            {{ name }}
            <span v-if="deepLabel" class="tabular-nums text-gray-600 dark:text-gray-400">· {{ deepLabel }}</span>
          </span>

          <UTooltip text="Open">
            <UButton
              icon="i-lucide-arrow-up-right"
              size="xs"
              color="neutral"
              variant="ghost"
              square
              class="justify-center"
              :aria-label="`Open ${name}`"
              @click.stop="follow"
            />
          </UTooltip>
        </span>

        <span class="block p-2">
          <NoteEmbedPreview
            :file-id="target.id"
            :version-id="target.versionId"
            :mime-type="target.mimeType"
            :name="name"
            :page="page"
            :kind="view"
          />
        </span>
      </template>

      <NoteEmbedMedia
        v-else-if="view === 'media' && target"
        :file-id="target.id"
        :name="name"
        :start-seconds="startSeconds"
        @open="openFile"
      />

      <span
        v-else
        role="link"
        :title="canOpen ? OPEN_CLICK_HINT : undefined"
        :aria-disabled="canOpen ? undefined : 'true'"
        class="flex cursor-pointer items-center gap-3 px-3 py-2.5"
        :class="{ 'hover:bg-gray-500/10': canOpen }"
        @click="onCardClick"
      >
        <UIcon :name="icon" class="h-5 w-5 shrink-0 text-gray-500 dark:text-gray-500" aria-hidden="true" />

        <span class="flex min-w-0 flex-1 flex-col">
          <span
            class="truncate text-sm font-medium"
            :class="
              view === 'trashed'
                ? 'text-gray-600 line-through dark:text-gray-400'
                : 'text-gray-900 dark:text-gray-100'
            "
          >
            {{ name }}
          </span>

          <span class="truncate text-xs text-gray-600 dark:text-gray-400">{{ cardDetail }}</span>
        </span>
      </span>

      <span
        v-if="caption"
        class="block border-t border-gray-200/70 px-3 py-1.5 text-sm text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
      >
        {{ caption }}
      </span>
    </span>
  </NodeViewWrapper>
</template>
