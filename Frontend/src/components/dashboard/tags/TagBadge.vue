<template>
  <span
    class="tag-badge inline-flex h-6 max-w-full items-center gap-1.5 rounded-full border pl-2 text-xs font-medium text-gray-900 transition-colors duration-150 dark:text-gray-100"
    :class="isRemovable ? 'pr-0.5' : 'pr-2.5'"
    :style="{ '--tag-color': tag.color }"
  >
    <TagTooltip :tag="tag">
      <span class="flex min-w-0 items-center gap-1.5">
        <Icon :icon="iconName" class="tag-badge-glyph h-3.5 w-3.5 shrink-0" />
        <span class="max-w-40 truncate">{{ tag.name }}</span>
      </span>
    </TagTooltip>

    <button
      v-if="isRemovable"
      type="button"
      class="tag-badge-remove flex h-5 w-5 shrink-0 cursor-pointer items-center justify-center rounded-full text-gray-500 transition-colors duration-150 hover:text-gray-900 focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-primary dark:text-gray-500 dark:hover:text-gray-100"
      :aria-label="`Remove tag ${tag.name}`"
      @click.stop="emit('removeTag', tag.id)"
    >
      <UIcon name="i-lucide-x" class="h-3.5 w-3.5" />
    </button>
  </span>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed } from "vue";

import type { TagDto } from "@/api/tag";

import { getIconByValue } from "@/utils/files/icons";

import TagTooltip from "./TagTooltip.vue";

const { tag, fileId = "" } = defineProps<{
  tag: TagDto;
  fileId?: string;
}>();

const emit = defineEmits<{
  removeTag: [tagId: string];
}>();

// Removal only makes sense when the badge is attached to a file; previews render it bare.
const isRemovable = computed(() => Boolean(fileId));

const iconName = computed(() => getIconByValue(tag.icon) || "mdi:tag");
</script>

<style scoped>
/* The tag color only tints the wash and glyph; the label stays on solid text tokens so any user-picked color stays legible. */
.tag-badge {
  background-color: color-mix(in srgb, var(--tag-color) 14%, transparent);
  border-color: color-mix(in srgb, var(--tag-color) 32%, transparent);
}

.tag-badge-glyph {
  color: color-mix(in srgb, var(--tag-color) 80%, #000);
}

.tag-badge-glyph:where(.dark *) {
  color: color-mix(in srgb, var(--tag-color) 80%, #fff);
}

.tag-badge-remove:hover {
  background-color: color-mix(in srgb, var(--tag-color) 22%, transparent);
}
</style>
