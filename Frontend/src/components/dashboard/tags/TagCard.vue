<template>
  <div
    draggable
    data-tag-item
    class="relative group cursor-pointer rounded-xl border p-5 transition-all duration-300"
    :class="[
      isSelected
        ? 'bg-primary/10 border-primary ring-2 ring-primary shadow-lg'
        : preview
          ? 'border-gray-300/70 dark:border-gray-700/70'
          : 'border-gray-300/70 dark:border-gray-700/70 hover:border-primary/60 hover:shadow-xl hover:-translate-y-0.5',
    ]"
    :style="{ '--tag-color': tag.color }"
    @click="!preview && $emit('click', $event)"
  >
    <!-- Selection indicator -->
    <Transition
      enter-active-class="transition duration-200 ease-out"
      enter-from-class="scale-0 opacity-0"
      enter-to-class="scale-100 opacity-100"
      leave-active-class="transition duration-150 ease-in"
      leave-from-class="scale-100 opacity-100"
      leave-to-class="scale-0 opacity-0"
    >
      <div
        v-if="isSelected"
        class="absolute top-3 left-3 w-6 h-6 bg-primary rounded-full flex items-center justify-center shadow-md"
      >
        <Icon icon="mdi:check" class="w-4 h-4 text-white" />
      </div>
    </Transition>

    <!-- Actions menu — hidden in preview mode -->
    <div
      v-if="!preview"
      class="absolute top-3 right-3 opacity-0 group-hover:opacity-100 transition-opacity duration-200"
      @click.stop
    >
      <UDropdownMenu :items="menuItems">
        <UButton icon="i-lucide-ellipsis-vertical" size="sm" variant="ghost" color="neutral" />
      </UDropdownMenu>
    </div>

    <!-- Main content -->
    <div class="flex items-start gap-4">
      <!-- Tag icon -->
      <div class="shrink-0">
        <div
          class="tag-icon-bg w-14 h-14 rounded-2xl flex items-center justify-center transition-transform duration-300 group-hover:scale-110"
        >
          <Icon :icon="getIconByValue(tag.icon) || 'mdi:tag'" class="tag-icon w-7 h-7" />
        </div>
      </div>

      <!-- Tag details -->
      <div class="flex-1 min-w-0 pt-0.5">
        <h3
          class="font-semibold text-base truncate transition-colors"
          :class="
            isSelected
              ? 'text-primary'
              : 'text-gray-900 dark:text-gray-100 group-hover:text-primary'
          "
          :title="tag.name"
        >
          {{ tag.name }}
        </h3>

        <div class="flex items-center gap-2 mt-2 text-xs text-muted">
          <UIcon name="i-lucide-calendar" class="w-3.5 h-3.5 shrink-0" />
          <span>{{ formatDate(tag.createdAt) }}</span>
        </div>

        <!-- Color — swatch only, no raw value shown -->
        <div class="flex items-center gap-2 mt-1.5 text-xs text-muted">
          <UIcon name="i-lucide-palette" class="w-3.5 h-3.5 shrink-0" />
          <div
            class="w-3.5 h-3.5 rounded-full border border-gray-200/70 dark:border-gray-700/70"
            :style="{ backgroundColor: tag.color }"
          />
        </div>
      </div>
    </div>

    <!-- Hover gradient overlay — hidden in preview mode -->
    <div
      v-if="!preview"
      class="absolute inset-0 rounded-xl opacity-0 group-hover:opacity-100 transition-opacity duration-300 pointer-events-none tag-gradient"
    />
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { Icon } from "@iconify/vue";
import type { TagDto } from "@/api/tag";
import { getIconByValue } from "@/utils/icon.utils";
import { formatDate } from "@/utils/date-formatters";

const props = defineProps<{
  tag: TagDto;
  isSelected: boolean;
  preview?: boolean;
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

<style scoped>
.tag-icon-bg {
  background-color: color-mix(in srgb, var(--tag-color) 15%, transparent);
}

.tag-icon {
  color: var(--tag-color);
}

.tag-gradient {
  background: radial-gradient(
    circle at top right,
    color-mix(in srgb, var(--tag-color) 5%, transparent) 0%,
    transparent 70%
  );
}
</style>
