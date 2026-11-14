<template>
  <div class="flex flex-col gap-4 p-4 text-left">
    <div class="flex items-start gap-3">
      <span
        class="flex size-9 shrink-0 items-center justify-center rounded-xl bg-gray-500/15 text-gray-900 dark:text-gray-100"
      >
        <Icon :icon="icon" class="size-5" aria-hidden="true" />
      </span>

      <div class="min-w-0">
        <p
          class="line-clamp-2 wrap-break-word text-base font-semibold text-gray-900 dark:text-gray-100"
        >
          {{ name }}
        </p>

        <p class="mt-0.5 truncate text-sm text-gray-600 dark:text-gray-400">{{ subtitle }}</p>
      </div>
    </div>

    <dl class="grid grid-cols-2 gap-1 rounded-xl bg-gray-500/10 p-1.5">
      <slot />
    </dl>

    <div v-if="$slots.notes" class="flex flex-col gap-2">
      <slot name="notes" />
    </div>

    <slot name="footer" />
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";

// Shared shell for the explorer's rich tooltips, in the same card language as the
// informational hints elsewhere in the app: neutral icon tile, a detail panel of
// ExplorerTooltipStat tiles, and optional ExplorerTooltipNote strips below it.
const { icon, name, subtitle } = defineProps<{
  icon: string;
  name: string;
  subtitle: string;
}>();
</script>
