<script setup lang="ts">
import { useMediaQuery } from "@vueuse/core";
import { computed } from "vue";

const {
  title,
  icon,
  guidance = undefined,
  wide = false,
} = defineProps<{
  title: string;
  icon: string;
  guidance?: string;
  // For hints with side-by-side detail, which would run off short screens as a tall column.
  wide?: boolean;
}>();

// Hover cards never open on touch screens, so devices without hover open them with a tap.
const canHover = useMediaQuery("(hover: hover)");

const contentClass = computed(() => {
  const width = wide ? "w-[min(40rem,calc(100vw-2rem))]" : "w-[min(20rem,calc(100vw-2rem))]";

  return `bg-default ${width} rounded-2xl border border-gray-200/70 p-0 shadow-2xl ring-0 dark:border-gray-700/70`;
});
</script>

<template>
  <UPopover
    :mode="canHover ? 'hover' : 'click'"
    :open-delay="150"
    :close-delay="200"
    :content="{ side: 'top', align: 'center', sideOffset: 8, collisionPadding: 16 }"
    :ui="{ content: contentClass }"
  >
    <button
      type="button"
      :aria-label="`About ${title.toLowerCase()}`"
      class="inline-flex size-5 shrink-0 cursor-help items-center justify-center rounded-full text-gray-500 transition-colors hover:text-gray-900 focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-primary dark:text-gray-400 dark:hover:text-gray-100"
    >
      <UIcon name="i-lucide-info" class="size-3.5" aria-hidden="true" />
    </button>

    <template #content>
      <div class="flex flex-col gap-4 p-4 text-left">
        <div class="flex items-start gap-3">
          <span
            class="flex size-9 shrink-0 items-center justify-center rounded-xl bg-gray-500/15 text-gray-900 dark:text-gray-100"
          >
            <UIcon :name="icon" class="size-5" aria-hidden="true" />
          </span>

          <div class="min-w-0">
            <p class="text-base font-semibold text-gray-900 dark:text-gray-100">{{ title }}</p>

            <p class="mt-1 text-sm leading-relaxed text-gray-600 dark:text-gray-400">
              <slot />
            </p>
          </div>
        </div>

        <slot name="detail" />

        <p
          v-if="guidance"
          class="flex items-start gap-2 rounded-xl bg-gray-500/10 px-3 py-2 text-sm text-gray-900 dark:text-gray-100"
        >
          <UIcon
            name="i-lucide-lightbulb"
            class="mt-0.5 size-4 shrink-0 text-gray-600 dark:text-gray-400"
            aria-hidden="true"
          />
          <span>{{ guidance }}</span>
        </p>
      </div>
    </template>
  </UPopover>
</template>
