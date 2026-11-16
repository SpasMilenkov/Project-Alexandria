<template>
  <div
    class="flex w-full flex-col overflow-hidden rounded-lg bg-white shadow-2xl ring-1 shadow-black/15 ring-black/10 dark:bg-neutral-900 dark:shadow-black/40 dark:ring-white/10"
    style="aspect-ratio: 520/340"
    aria-hidden="true"
  >
    <div
      class="flex items-center gap-2 border-b border-gray-200/70 px-4 py-2.5 dark:border-gray-700/70"
    >
      <Icon icon="mdi:home-outline" class="h-4 w-4 text-gray-500 dark:text-gray-500" />
      <span class="text-xs font-medium text-gray-900 dark:text-gray-100">Home</span>

      <span
        class="ms-auto flex items-center gap-1.5 rounded-md border border-gray-200/70 px-2 py-1 text-[11px] text-gray-500 dark:border-gray-700/70 dark:text-gray-500"
      >
        <Icon icon="mdi:magnify" class="h-3.5 w-3.5" />
        Search
      </span>
    </div>

    <div class="grid flex-1 grid-cols-3 content-start gap-3 p-4">
      <div
        v-for="tile in tiles"
        :key="tile.kind"
        class="flex min-w-0 flex-col gap-2 rounded-lg border border-gray-200/70 p-2.5 dark:border-gray-700/70"
      >
        <div class="flex items-start justify-between gap-1">
          <span
            class="flex h-8 w-8 items-center justify-center rounded-md bg-neutral-100 text-gray-600 dark:bg-neutral-800 dark:text-gray-400"
          >
            <Icon :icon="tile.icon" class="h-4 w-4" />
          </span>

          <span
            v-if="tile.automated"
            class="flex h-4 w-4 items-center justify-center rounded-full bg-primary text-white"
          >
            <Icon icon="mdi:lightning-bolt" class="h-2.5 w-2.5" />
          </span>
        </div>

        <span class="truncate text-xs font-medium text-gray-900 dark:text-gray-100">
          {{ tile.name }}
        </span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed } from "vue";

import {
  type LibraryCapabilities,
  type LibraryFolderDraft,
  isDraftAutomated,
  presetFor,
} from "@/features/onboarding/library-setup";

const { capabilities, drafts } = defineProps<{
  drafts: LibraryFolderDraft[];
  capabilities: LibraryCapabilities;
}>();

const tiles = computed(() =>
  drafts.map((draft) => ({
    automated: isDraftAutomated(draft, capabilities),
    icon: presetFor(draft.kind).icon,
    kind: draft.kind,
    name: draft.name.trim() || presetFor(draft.kind).defaultName,
  })),
);
</script>
