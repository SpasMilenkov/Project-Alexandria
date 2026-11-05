<template>
  <div
    data-tag-item
    :role="isInteractive ? 'button' : undefined"
    :tabindex="isInteractive ? 0 : undefined"
    :aria-pressed="isInteractive ? isSelected : undefined"
    class="group relative flex min-w-0 items-center gap-3 rounded-lg border p-3 transition-colors duration-150 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
    :class="stateClass"
    @click="handleActivate"
    @keydown.enter.prevent="handleActivate"
    @keydown.space.prevent="handleActivate"
  >
    <TagIconWell :color="tag.color" :icon="tag.icon" :is-selected="isSelected" />

    <div class="min-w-0 flex-1">
      <p class="truncate text-sm font-semibold text-gray-900 dark:text-gray-100" :title="tag.name">
        {{ tag.name }}
      </p>

      <div class="mt-0.5 flex min-w-0 items-center gap-1.5">
        <UBadge v-if="isReadOnly" color="neutral" variant="outline" size="xs" class="shrink-0">
          System
        </UBadge>
        <p class="truncate text-xs text-gray-600 dark:text-gray-400" :title="subtitle">
          {{ subtitle }}
        </p>
      </div>
    </div>

    <div
      v-if="isInteractive"
      class="shrink-0 transition-opacity duration-150 pointer-fine:opacity-0 pointer-fine:group-hover:opacity-100 pointer-fine:group-focus-within:opacity-100"
      @click.stop
      @keydown.stop
    >
      <UDropdownMenu :items="menuItems">
        <UButton
          icon="i-lucide-ellipsis-vertical"
          size="sm"
          variant="ghost"
          color="neutral"
          square
          class="justify-center"
          :aria-label="`Actions for ${tag.name}`"
        />
      </UDropdownMenu>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";

import type { TagDto } from "@/api/tag";

import { describeTag } from "@/features/tags/display";

import TagIconWell from "./TagIconWell.vue";

const {
  tag,
  isSelected,
  preview = false,
} = defineProps<{
  tag: TagDto;
  isSelected: boolean;
  preview?: boolean;
}>();

const emit = defineEmits<{
  click: [event: MouseEvent | KeyboardEvent];
  edit: [tag: TagDto];
  delete: [tagId: string];
}>();

const isReadOnly = computed(() => tag.isSystem === true);

const isInteractive = computed(() => !preview && !isReadOnly.value);

const subtitle = computed(() => describeTag(tag));

const stateClass = computed(() => {
  if (isSelected) return "border-primary bg-primary/5 ring-1 ring-primary";

  if (!isInteractive.value) return "border-gray-200/70 dark:border-gray-700/70";

  return "cursor-pointer border-gray-200/70 hover:border-gray-300 hover:bg-gray-900/5 dark:border-gray-700/70 dark:hover:border-gray-600 dark:hover:bg-white/5";
});

const handleActivate = (event: MouseEvent | KeyboardEvent) => {
  if (!isInteractive.value) return;

  emit("click", event);
};

const menuItems = computed(() => [
  [
    {
      icon: "i-lucide-pencil",
      label: "Edit",
      onSelect: () => emit("edit", tag),
    },
  ],
  [
    {
      color: "error" as const,
      icon: "i-lucide-trash-2",
      label: "Delete",
      onSelect: () => emit("delete", tag.id),
    },
  ],
]);
</script>
