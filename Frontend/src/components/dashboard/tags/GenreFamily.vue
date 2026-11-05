<template>
  <UCollapsible v-model:open="isOpen">
    <UButton
      variant="ghost"
      color="neutral"
      block
      class="h-auto justify-between rounded-none px-3 py-2.5"
      :trailing-icon="isOpen ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'"
      :aria-label="`${parent.name}, ${children.length} subgenres`"
    >
      <div class="flex min-w-0 items-center gap-2.5">
        <TagIconWell :color="parent.color" :icon="parent.icon" size="xs" />
        <span class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
          {{ parent.name }}
        </span>
        <UBadge color="neutral" variant="subtle" size="sm" :label="String(children.length)" />
      </div>
    </UButton>

    <template #content>
      <div class="flex flex-wrap gap-2 pb-3 pl-12 pr-3 pt-1">
        <SystemTagChip v-for="child in children" :key="child.id" :tag="child" />
      </div>
    </template>
  </UCollapsible>
</template>

<script setup lang="ts">
import { ref } from "vue";

import type { TagDto } from "@/api/tag";

import SystemTagChip from "./SystemTagChip.vue";
import TagIconWell from "./TagIconWell.vue";

const { parent, children } = defineProps<{
  parent: TagDto;
  children: TagDto[];
}>();

const isOpen = ref(false);
</script>
