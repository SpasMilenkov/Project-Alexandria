<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { NoteLinkRef } from "@/features/notes/links";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import { useOpenLinkTarget } from "@/composables/useOpenLinkTarget";
import {
  linkKindNoun,
  linkStateLabel,
  linkTargetIcon,
  linkTargetName,
} from "@/features/notes/link-targets";
import { getLinkTarget } from "@/queries/links";

const { link } = defineProps<{ link: NoteLinkRef }>();

const { data: target, isPending } = useQuery(() =>
  getLinkTarget({ id: link.id, kind: link.kind }),
);

const { hrefFor, open } = useOpenLinkTarget();

const href = computed(() => hrefFor(target.value));
const icon = computed(() => linkTargetIcon(link.kind, target.value));
const stateLabel = computed(() => linkStateLabel(target.value));

const name = computed(() => {
  const live = target.value ? linkTargetName(target.value) : null;

  if (live) return live;

  return `Unavailable ${linkKindNoun(link.kind)}`;
});

// Plain clicks stay in the app; modified clicks keep the browser's own handling.
const follow = (event: MouseEvent) => {
  if (event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) return;

  event.preventDefault();
  open(target.value);
};
</script>

<template>
  <li class="flex min-w-0 items-center gap-2">
    <div v-if="isPending" class="flex h-5 items-center text-gray-500">
      <BlockSpinner :size="14" aria-label="Loading link" />
    </div>

    <template v-else>
      <UIcon :name="icon" class="h-4 w-4 shrink-0 text-gray-500 dark:text-gray-500" aria-hidden="true" />

      <a
        v-if="href"
        :href="href"
        :title="name"
        class="min-w-0 flex-1 truncate rounded-xs text-gray-900 hover:underline focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-100"
        :class="{ 'line-through': stateLabel !== null }"
        @click="follow"
      >
        {{ name }}
      </a>

      <span v-else class="min-w-0 flex-1 truncate text-gray-600 dark:text-gray-400">
        {{ name }}
      </span>

      <span v-if="stateLabel" class="shrink-0 text-xs text-gray-500 dark:text-gray-500">
        {{ stateLabel }}
      </span>
    </template>
  </li>
</template>
