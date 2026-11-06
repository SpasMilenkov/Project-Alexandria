<template>
  <UTooltip :delay-duration="300" :content="{ side: 'top' }" :ui="TAG_TOOLTIP_UI">
    <slot />

    <template #content>
      <div class="flex flex-col gap-2 p-3">
        <div class="flex min-w-0 items-center gap-2.5">
          <TagIconWell :color="tag.color" :icon="tag.icon" size="xs" />

          <div class="min-w-0 flex-1">
            <p class="truncate text-sm font-semibold text-gray-900 dark:text-gray-100">
              {{ tag.name }}
            </p>
            <p class="truncate text-xs text-gray-600 dark:text-gray-400">
              {{ subtitle }}
            </p>
          </div>
        </div>

        <template v-if="tag.description">
          <div class="h-px bg-gray-200/70 dark:bg-gray-700/70" />

          <p class="text-xs leading-relaxed text-gray-700 dark:text-gray-300">
            {{ tag.description }}
          </p>
        </template>
      </div>
    </template>
  </UTooltip>
</template>

<script setup lang="ts">
import { computed } from "vue";

import type { TagDto } from "@/api/tag";

import { tagBreadcrumb } from "@/features/tags/display";

import TagIconWell from "./TagIconWell.vue";

const { tag } = defineProps<{
  tag: TagDto;
}>();

// Overrides only the single-line chip sizing of the default tooltip; surface,
// ring and the one-shot scale animation come from the Nuxt UI theme.
const TAG_TOOLTIP_UI = {
  content: "block h-auto w-64 max-w-[calc(100vw-2rem)] rounded-lg p-0 shadow-lg",
};

const subtitle = computed(() => {
  if (!tag.isSystem) return "Your tag";

  return tagBreadcrumb(tag) ?? "System vocabulary";
});
</script>
