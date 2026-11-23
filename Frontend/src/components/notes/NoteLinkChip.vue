<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { NodeViewWrapper, nodeViewProps } from "@tiptap/vue-3";
import { computed } from "vue";

import { useOpenLinkTarget } from "@/composables/useOpenLinkTarget";
import { LinkTargetState } from "@/enums/link-target-state";
import { deepLinkLabel } from "@/features/notes/deep-links";
import { type NoteLinkAttributes } from "@/features/notes/link-node";
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

// Tiptap's node view renderer only accepts components declaring its runtime props object.
// oxlint-disable-next-line vue/define-props-declaration
const { editor, getPos, node, selected } = defineProps(nodeViewProps);

const attrs = computed(() => node.attrs as unknown as NoteLinkAttributes);

const { data: target, isPending } = useQuery(() =>
  getLinkTarget({ id: attrs.value.id, kind: attrs.value.kind }),
);

const { open } = useOpenLinkTarget();

const canOpen = computed(
  () => target.value !== undefined && target.value.state !== LinkTargetState.Unavailable,
);

// The author's label wins; otherwise the target's live name, so renames show up on their own.
const text = computed(() => {
  const label = attrs.value.label === null ? "" : labelText(attrs.value.label).trim();

  if (label !== "") return label;

  const name = target.value ? linkTargetName(target.value) : null;

  if (name) return name;
  if (isPending.value) return `Linked ${linkKindNoun(attrs.value.kind)}`;

  return `Unavailable ${linkKindNoun(attrs.value.kind)}`;
});

const icon = computed(() => linkTargetIcon(attrs.value.kind, target.value));
const stateLabel = computed(() => linkStateLabel(target.value));

// "12:04" for a timestamp, "p. 3" for a page.
const deepLabel = computed(() => deepLinkLabel(attrs.value.suffix));

const tooltip = computed(() => {
  const name = target.value ? linkTargetName(target.value) : null;
  const parts = [name ?? text.value, deepLabel.value, stateLabel.value, OPEN_CLICK_HINT].filter(
    Boolean,
  );

  return parts.join(" · ");
});

const tone = computed(() => {
  if (target.value?.state === LinkTargetState.Trashed) {
    return "text-gray-600 line-through dark:text-gray-400";
  }

  if (target.value?.state === LinkTargetState.Unavailable) {
    return "border-dashed text-gray-500 dark:text-gray-500";
  }

  return "text-gray-900 hover:bg-gray-500/15 dark:text-gray-100";
});

const follow = () => {
  if (canOpen.value) open(target.value, attrs.value.suffix);
};

// A plain click selects the chip, which opens its edit popover; that popover can open it too.
const onClick = (event: MouseEvent) => {
  if (isOpenClick(event)) {
    follow();
    return;
  }

  const pos = getPos();

  if (typeof pos === "number") editor.chain().focus().setNodeSelection(pos).run();
};
</script>

<template>
  <!-- A span rather than an anchor: the editor styles every anchor as an accent-coloured text
       link. The Connections panel lists the same links as real, keyboard-reachable links. -->
  <NodeViewWrapper as="span" class="inline">
    <span
      role="link"
      :title="tooltip"
      :aria-disabled="canOpen ? undefined : 'true'"
      contenteditable="false"
      draggable="false"
      class="mx-px inline-flex max-w-full cursor-pointer items-center gap-1 rounded-md border border-gray-200/70 bg-gray-500/10 px-1.5 align-baseline text-[0.95em] leading-snug transition-colors duration-150 dark:border-gray-700/70"
      :class="[tone, { 'ring-2 ring-primary': selected }]"
      @click="onClick"
    >
      <UIcon :name="icon" class="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
      <span class="truncate">{{ text }}</span>

      <span v-if="deepLabel" class="shrink-0 tabular-nums text-gray-600 dark:text-gray-400">
        · {{ deepLabel }}
      </span>
    </span>
  </NodeViewWrapper>
</template>
