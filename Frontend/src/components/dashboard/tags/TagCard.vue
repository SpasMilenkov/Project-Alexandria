<template>
  <div
    draggable
    data-tag-item
    class="relative group cursor-pointer rounded-xl border p-6 transition-all duration-300 "
    :class="[
      isSelected
        ? 'bg-primary/10 border-primary ring-2 ring-primary shadow-lg'
        : ' border-gray-200 dark:border-gray-700 hover:border-primary/60 hover:shadow-xl hover:-translate-y-0.5',
    ]"
    :style="{ '--tag-color': props.tag.color }"
    @click="$emit('click', $event)"
  >
    <!-- Selection Indicator -->
    <transition
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
    </transition>

    <!-- Actions Menu -->
    <div
      class="absolute top-3 right-3 opacity-0 group-hover:opacity-100 transition-opacity duration-200"
    >
      <UDropdownMenu :items="menuItems" @click.stop>
        <UButton
          icon="i-heroicons-ellipsis-vertical"
          size="sm"
          variant="ghost"
          color="gray"
        />
      </UDropdownMenu>
    </div>

    <!-- Main Content -->
    <div class="items-start gap-4 xs:flex-col md:flex">
      <!-- Tag Icon -->
      <div class="shrink-0">
        <div
          class="tag-icon-bg w-16 h-16 rounded-2xl flex items-center justify-center transition-transform duration-300 group-hover:scale-110"
        >
          <Icon
            :icon="getIconByValue(tag.icon) || 'mdi:tag'"
            class="tag-icon w-8 h-8"
          />
        </div>
      </div>

      <!-- Tag Details -->
      <div class="flex-1 min-w-0">
        <!-- Tag Name -->
        <h3
          class="font-semibold text-lg mb-2 truncate"
          :class="
            isSelected
              ? 'text-primary'
              : 'text-gray-900 dark:text-gray-100 group-hover:text-primary transition-colors'
          "
          :title="tag.name"
        >
          {{ tag.name }}
        </h3>

        <!-- Tag Metadata -->
        <div class="space-y-1.5">
          <div
            class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400"
          >
            <Icon icon="mdi:calendar" class="w-4 h-4" />
            <span>Created {{ formatDate(tag.createdAt) }}</span>
          </div>

          <!-- Tag Color Preview -->
          <div
            class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400"
          >
            <Icon icon="mdi:palette" class="w-4 h-4" />
            <span>Color:</span>
            <div
              class="w-4 h-4 rounded-full border border-gray-300 dark:border-gray-600"
              :style="{ backgroundColor: tag.color }"
            ></div>
            <span class="font-mono text-xs">{{ tag.color }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Bottom Section - Appears on Hover -->
    <div
      class="mt-4 pt-4 border-t border-gray-200 dark:border-gray-700 opacity-0 group-hover:opacity-100 transition-all duration-300 max-h-0 group-hover:max-h-20 overflow-hidden"
    >
      <div
        class="flex items-center justify-between text-xs text-gray-500 dark:text-gray-400"
      >
        <div class="flex items-center gap-1">
          <Icon icon="mdi:clock-outline" class="w-3.5 h-3.5" />
          <span>{{ formatDateTime(tag.createdAt) }}</span>
        </div>
        <div class="flex items-center gap-1">
          <Icon icon="mdi:identifier" class="w-3.5 h-3.5" />
          <span class="font-mono">{{ tag.id.slice(0, 8) }}...</span>
        </div>
      </div>
    </div>

    <!-- Hover Gradient Effect -->
    <div
      class="absolute inset-0 rounded-xl opacity-0 group-hover:opacity-100 transition-opacity duration-300 pointer-events-none tag-gradient"
    ></div>
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
}>();

const emit = defineEmits<{
  click: [event: MouseEvent];
  edit: [tagId: TagDto];
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



const formatDateTime = (dateString: string) => {
  const date = new Date(dateString);
  return date.toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
};
</script>

<style scoped>
.tag-card {
  --tag-color: #000000;
}

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
