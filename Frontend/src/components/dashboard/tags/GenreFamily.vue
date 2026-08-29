<template>
  <UCollapsible v-model:open="isOpen">
    <UButton
      variant="ghost"
      color="neutral"
      block
      class="justify-between h-auto py-2.5"
      :trailing-icon="isOpen ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'"
    >
      <div class="flex items-center gap-2 min-w-0">
        <span
          class="w-6 h-6 rounded-md shrink-0 flex items-center justify-center"
          :style="{
            backgroundColor: `color-mix(in srgb, ${parent.color} 15%, transparent)`,
            color: parent.color,
          }"
        >
          <Icon :icon="getIconByValue(parent.icon) || 'mdi:tag'" class="w-3.5 h-3.5" />
        </span>
        <span class="text-sm font-medium truncate">{{ parent.name }}</span>
        <UBadge color="neutral" variant="subtle" size="sm" :label="String(children.length)" />
      </div>
    </UButton>

    <template #content>
      <div
        class="flex flex-wrap gap-3 pt-3 pb-2 pl-3 ml-2 border-l border-gray-200/70 dark:border-gray-700/70"
      >
        <SystemTagChip v-for="child in children" :key="child.id" :tag="child" />
      </div>
    </template>
  </UCollapsible>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { ref } from "vue";

import type { TagDto } from "@/api/tag";

import { getIconByValue } from "@/utils/icon.utils";

import SystemTagChip from "./SystemTagChip.vue";

defineProps<{
  parent: TagDto;
  children: TagDto[];
}>();

const isOpen = ref(false);
</script>

<style scoped></style>
