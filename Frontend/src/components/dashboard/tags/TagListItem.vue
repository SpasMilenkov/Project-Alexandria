<template>
  <div
    data-tag-item
    :role="isInteractive ? 'button' : undefined"
    :tabindex="isInteractive ? 0 : undefined"
    :aria-pressed="isInteractive ? isSelected : undefined"
    class="group flex items-center gap-3 px-4 py-2.5 transition-colors duration-150 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary"
    :class="stateClass"
    @click="handleActivate"
    @keydown.enter.prevent="handleActivate"
    @keydown.space.prevent="handleActivate"
  >
    <TagIconWell :color="tag.color" :icon="tag.icon" :is-selected="isSelected" size="sm" />

    <div class="min-w-0 flex-1">
      <div class="flex min-w-0 items-center gap-2">
        <p class="truncate text-sm font-medium text-gray-900 dark:text-gray-100" :title="tag.name">
          {{ tag.name }}
        </p>
        <UBadge v-if="isReadOnly" color="neutral" variant="outline" size="xs" class="shrink-0">
          System
        </UBadge>
      </div>

      <div v-if="secondaryLine" class="mt-0.5 flex min-w-0 items-center gap-1.5">
        <span
          v-if="breadcrumb"
          class="max-w-1/2 shrink-0 truncate text-xs font-medium text-gray-700 dark:text-gray-300"
          :title="breadcrumb"
        >
          {{ breadcrumb }}
        </span>
        <span
          v-if="breadcrumb"
          class="shrink-0 text-xs text-gray-600 dark:text-gray-400"
          aria-hidden="true"
        >
          ·
        </span>
        <p class="truncate text-xs text-gray-600 dark:text-gray-400" :title="secondaryLine">
          {{ secondaryLine }}
        </p>
      </div>
    </div>

    <span
      v-if="!isReadOnly"
      class="hidden shrink-0 text-xs font-medium text-gray-600 sm:block dark:text-gray-400"
    >
      {{ formatDate(tag.createdAt) }}
    </span>

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

import { describeTag, tagBreadcrumbPrefix } from "@/features/tags/display";
import { formatDate } from "@/utils/date-formatters";

import TagIconWell from "./TagIconWell.vue";

const { tag, isSelected } = defineProps<{
  tag: TagDto;
  isSelected: boolean;
}>();

const emit = defineEmits<{
  click: [event: MouseEvent | KeyboardEvent];
  edit: [tag: TagDto];
  delete: [tagId: string];
}>();

const isReadOnly = computed(() => tag.isSystem === true);

const isInteractive = computed(() => !isReadOnly.value);

// The created date has its own right-aligned column here, so only descriptions and
// system facets earn a second line.
const secondaryLine = computed(() => {
  if (isReadOnly.value) return describeTag(tag);

  return tag.description ?? "";
});

const breadcrumb = computed(() => tagBreadcrumbPrefix(tag));

const stateClass = computed(() => {
  if (isSelected) return "bg-primary/5 ring-1 ring-inset ring-primary";

  if (!isInteractive.value) return "";

  return "cursor-pointer hover:bg-gray-900/5 dark:hover:bg-white/5";
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
