<script setup lang="ts" generic="T extends number">
import { computed } from "vue";

const {
  styles,
  selected,
  columns = 3,
} = defineProps<{
  label: string;
  styles: readonly { value: T; label: string; description: string }[];
  selected: T;
  columns?: 2 | 3;
}>();

const emit = defineEmits<{ select: [value: T] }>();
const description = computed(() => styles.find((style) => style.value === selected)?.description);
</script>

<template>
  <div class="flex flex-col gap-3" role="group" :aria-label="label">
    <div class="grid gap-2" :class="columns === 2 ? 'grid-cols-2' : 'grid-cols-3'">
      <button
        v-for="style in styles"
        :key="style.value"
        type="button"
        :aria-label="style.label"
        :aria-pressed="selected === style.value"
        class="flex min-w-0 flex-col items-center rounded-lg border border-gray-200/70 px-1 pb-2 text-gray-900 transition-colors hover:bg-gray-100/70 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary dark:border-gray-700/70 dark:text-gray-100 dark:hover:bg-gray-800/70"
        :class="{ 'ring-1 ring-primary border-primary': selected === style.value }"
        @click="emit('select', style.value)"
      >
        <span
          class="flex h-20 w-full items-center justify-center overflow-hidden"
          aria-hidden="true"
        >
          <span class="flex shrink-0 scale-70 items-center justify-center">
            <slot name="preview" :value="style.value" />
          </span>
        </span>
        <span class="w-full truncate text-center text-xs font-medium">{{ style.label }}</span>
      </button>
    </div>

    <p v-if="description" class="text-xs leading-relaxed text-gray-600 dark:text-gray-400">
      {{ description }}
    </p>
  </div>
</template>
