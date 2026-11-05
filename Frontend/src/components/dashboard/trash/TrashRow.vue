<template>
  <li
    role="checkbox"
    :aria-checked="isSelected"
    :aria-disabled="disabled || undefined"
    :tabindex="disabled ? -1 : 0"
    class="flex items-center gap-3 px-4 py-2.5 transition-colors duration-150 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary"
    :class="stateClass"
    @click="handleToggle"
    @keydown.space.prevent="handleToggle"
    @keydown.enter.prevent="handleToggle"
  >
    <span
      class="flex h-4 w-4 shrink-0 items-center justify-center rounded-sm border transition-colors duration-150"
      :class="checkboxClass"
      aria-hidden="true"
    >
      <UIcon v-if="isSelected" name="i-lucide-check" class="h-3 w-3" />
    </span>

    <Icon :icon="icon" class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400" />

    <div class="min-w-0 flex-1">
      <p class="truncate text-sm font-medium text-gray-900 dark:text-gray-100" :title="name">
        {{ name }}
      </p>
      <p v-if="detail" class="truncate text-xs text-gray-600 dark:text-gray-400">{{ detail }}</p>
    </div>

    <span
      v-if="deletedLabel"
      class="hidden shrink-0 text-xs font-medium tabular-nums text-gray-600 sm:block dark:text-gray-400"
    >
      {{ deletedLabel }}
    </span>
  </li>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed } from "vue";

const {
  name,
  icon,
  detail = null,
  deletedLabel = null,
  isSelected,
  disabled = false,
} = defineProps<{
  name: string;
  icon: string;
  detail?: string | null;
  deletedLabel?: string | null;
  isSelected: boolean;
  disabled?: boolean;
}>();

const emit = defineEmits<{
  toggle: [];
}>();

const stateClass = computed(() => {
  if (disabled) return "cursor-not-allowed";

  if (isSelected) return "cursor-pointer bg-primary/5";

  return "cursor-pointer hover:bg-gray-900/5 dark:hover:bg-white/5";
});

const checkboxClass = computed(() => {
  if (isSelected) return "border-primary bg-primary text-white";

  return "border-gray-400 dark:border-gray-500";
});

const handleToggle = () => {
  if (disabled) return;

  emit("toggle");
};
</script>
