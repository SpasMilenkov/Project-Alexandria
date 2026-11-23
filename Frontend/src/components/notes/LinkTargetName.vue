<script setup lang="ts">
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { NoteLinkRef } from "@/features/notes/links";

import { linkKindNoun, linkTargetName } from "@/features/notes/link-targets";
import { getLinkTarget } from "@/queries/links";

// An autolink inside quoted note text, shown by the target's live name.
const { link } = defineProps<{ link: NoteLinkRef }>();

const { data: target, isPending } = useQuery(() =>
  getLinkTarget({ id: link.id, kind: link.kind }),
);

const name = computed(() => {
  const live = target.value ? linkTargetName(target.value) : null;

  if (live) return live;
  if (isPending.value) return `linked ${linkKindNoun(link.kind)}`;

  return `unavailable ${linkKindNoun(link.kind)}`;
});
</script>

<template>
  <span class="font-medium text-gray-900 dark:text-gray-100">{{ name }}</span>
</template>
