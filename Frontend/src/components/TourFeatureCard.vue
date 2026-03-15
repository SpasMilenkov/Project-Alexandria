<template>
  <div
    :class="[
      'group flex flex-col gap-4 rounded-2xl border p-5 transition-all duration-200',
      highlight
        ? 'border-primary/20 bg-primary/4 dark:border-primary/15 dark:bg-primary/5'
        : 'border-gray-200/50 bg-white/30 dark:border-gray-700/40 dark:bg-white/2',
    ]"
  >
    <!-- Icon + badge row -->
    <div class="flex items-start justify-between">
      <div
        :class="[
          'flex h-11 w-11 items-center justify-center rounded-xl',
          highlight
            ? 'bg-primary/15 text-primary'
            : 'bg-gray-100/80 text-gray-500 dark:bg-white/10 dark:text-gray-400',
        ]"
      >
        <Icon :icon="icon" class="h-5 w-5" />
      </div>

      <span
        v-if="badge"
        :class="[
          'rounded-full px-2.5 py-1 text-[10px] font-semibold uppercase tracking-wide',
          highlight
            ? 'bg-primary/15 text-primary'
            : 'bg-gray-100/80 text-gray-500 dark:bg-white/10 dark:text-gray-400',
        ]"
      >
        {{ badge }}
      </span>
    </div>

    <!-- Title + description -->
    <div>
      <p
        :class="[
          'text-base font-semibold leading-snug',
          highlight ? 'text-gray-900 dark:text-gray-100' : 'text-gray-700 dark:text-gray-300',
        ]"
      >
        {{ title }}
      </p>
      <p class="mt-1.5 text-sm leading-relaxed text-gray-500 dark:text-gray-400">
        <slot name="description">{{ description }}</slot>
      </p>
    </div>

    <!-- Tips list (if provided) -->
    <ul
      v-if="tips && tips.length"
      :class="[
        'mt-auto space-y-2 border-t pt-4',
        highlight
          ? 'border-primary/15 dark:border-primary/10'
          : 'border-gray-200/60 dark:border-gray-700/50',
      ]"
    >
      <li
        v-for="tip in tips"
        :key="tip"
        class="flex items-start gap-2 text-xs leading-relaxed text-gray-500 dark:text-gray-400"
      >
        <Icon
          :icon="highlight ? 'mdi:check-circle' : 'mdi:arrow-right-thin'"
          :class="[
            'mt-0.5 h-3.5 w-3.5 shrink-0',
            highlight ? 'text-primary' : 'text-gray-400 dark:text-gray-500',
          ]"
        />
        {{ tip }}
      </li>
    </ul>

    <!-- Custom slot override for fully custom card body -->
    <div v-if="$slots.default" class="mt-auto">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";

defineProps<{
  icon: string;
  title: string;
  description?: string;
  badge?: string;
  highlight?: boolean;
  tips?: string[];
}>();
</script>
