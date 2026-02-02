<template>
  <div
    data-tag-item
    class="flex items-center gap-4 px-4 py-3 border-b cursor-pointer transition-colors hover:bg-muted"
    :class="{
      'bg-primary/5 border-l-4 border-l-primary': isSelected,
    }"
    @click="$emit('click', $event)"
  >
    <!-- Selection Checkbox -->
    <div class="shrink-0">
      <div
        class="w-5 h-5 rounded border-2 flex items-center justify-center transition-colors"
        :class="
          isSelected ? 'bg-primary border-primary' : 'border-current opacity-50'
        "
      >
        <Icon v-if="isSelected" icon="mdi:check" class="w-3 h-3 text-white" />
      </div>
    </div>

    <!-- Tag Icon -->
    <div class="shrink-0">
      <div
        class="w-10 h-10 rounded-full bg-primary/10 flex items-center justify-center"
      >
        <Icon icon="mdi:tag" class="w-5 h-5 text-primary" />
      </div>
    </div>

    <!-- Tag Info -->
    <div class="flex-1 min-w-0">
      <h3 class="font-medium text-sm truncate">{{ tag.name }}</h3>
      <div class="flex items-center gap-3 text-xs opacity-70 mt-1">
        <span>Created {{ formatDate(tag.createdAt) }}</span>
        <span v-if="tag.updatedAt"
          >Updated {{ formatDate(tag.updatedAt) }}</span
        >
      </div>
    </div>

    <!-- Actions -->
    <div class="shrink-0">
      <UDropdownMenu :items="menuItems" @click.stop>
        <UButton
          icon="i-heroicons-ellipsis-vertical"
          size="sm"
          variant="ghost"
        />
      </UDropdownMenu>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { Icon } from "@iconify/vue";
import type { TagDto } from "@/api/tag";
import { formatDate } from "@/utils/date-formatters";

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
      label: "Edit",
      icon: "i-heroicons-pencil",
      onSelect: () => emit("edit", props.tag),
    },
  ],
  [
    {
      label: "Delete",
      icon: "i-heroicons-trash",
      onSelect: () => emit("delete", props.tag.id),
    },
  ],
]);

</script>

<style scoped></style>
