<script setup lang="ts">
import { computed } from "vue";

import NoteLinkRow from "@/components/notes/NoteLinkRow.vue";
import { extractNoteLinks, linkKey } from "@/features/notes/links";

// The draft, already trailing typing, so links show up as they are written rather than after
// the next save.
const { markdown } = defineProps<{ markdown: string }>();

const links = computed(() => extractNoteLinks(markdown));
</script>

<template>
  <section aria-label="Links from this note" class="flex flex-col gap-2">
    <h3
      class="flex items-center gap-1.5 pl-2 text-xs font-semibold tracking-wide text-gray-600 uppercase dark:text-gray-400"
    >
      <UIcon name="i-lucide-link" class="h-3.5 w-3.5" aria-hidden="true" />
      Links
    </h3>

    <ul v-if="links.length > 0" class="flex flex-col gap-1.5 px-2 text-sm">
      <NoteLinkRow v-for="link in links" :key="linkKey(link)" :link="link" />
    </ul>

    <p v-else class="px-2 text-sm text-gray-600 dark:text-gray-400">
      No links yet. Type [[ to link a note, file, folder, tag or playlist.
    </p>
  </section>
</template>
