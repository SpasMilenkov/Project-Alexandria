<template>
  <div
    data-tag-item
    class="flex items-center gap-3 px-4 py-3 cursor-pointer transition-colors hover:bg-gray-50/60 dark:hover:bg-white/5"
    :class="isSelected ? 'ring-1 ring-inset ring-primary bg-primary/5' : ''"
    :style="{ '--tag-color': tag.color }"
    @click="$emit('click', $event)"
  >
    <!-- Selection checkbox -->
    <div class="shrink-0">
      <div
        class="w-5 h-5 rounded border-2 flex items-center justify-center transition-colors"
        :class="isSelected ? 'bg-primary border-primary' : 'border-gray-300 dark:border-gray-600'"
      >
        <UIcon v-if="isSelected" name="i-lucide-check" class="w-3 h-3 text-white" />
      </div>
    </div>

    <!-- Tag icon — colored with the tag's own color -->
    <div
      class="shrink-0 w-9 h-9 rounded-lg flex items-center justify-center"
      :style="{
        backgroundColor: `color-mix(in srgb, ${tag.color} 15%, transparent)`,
        color: tag.color,
      }"
    >
      <Icon :icon="getIconByValue(tag.icon) || 'mdi:tag'" class="w-4 h-4" />
    </div>

    <!-- Tag info -->
    <div class="flex-1 min-w-0">
      <p class="text-sm font-medium truncate">{{ tag.name }}</p>
      <p class="text-xs text-muted mt-0.5">Created {{ formatDate(tag.createdAt) }}</p>
    </div>

    <!-- Color swatch — no raw value -->
    <div
      class="shrink-0 w-4 h-4 rounded-full border border-gray-200/70 dark:border-gray-700/70 hidden sm:block"
      :style="{ backgroundColor: tag.color }"
    />

    <!-- Actions -->
    <div class="shrink-0" @click.stop>
      <UDropdownMenu :items="menuItems">
        <UButton icon="i-lucide-ellipsis-vertical" size="sm" variant="ghost" color="neutral" />
      </UDropdownMenu>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { Icon } from "@iconify/vue";
import type { TagDto } from "@/api/tag";
import { formatDate } from "@/utils/date-formatters";
import { getIconByValue } from "@/utils/icon.utils";

const props = defineProps<{
  tag: TagDto;
  isSelected: boolean;
}>();

const emit = defineEmits<{
  click: [event: MouseEvent];
  edit: [tag: TagDto];
  delete: [tagId: string];
}>();

const menuItems = computed(() => [
  [
    {
      icon: "i-lucide-pencil",
      label: "Edit",
      onSelect: () => emit("edit", props.tag),
    },
  ],
  [
    {
      icon: "i-lucide-trash-2",
      label: "Delete",
      onSelect: () => emit("delete", props.tag.id),
    },
  ],
]);
</script>

<style scoped></style>
