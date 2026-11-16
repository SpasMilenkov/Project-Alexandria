<template>
  <div class="flex flex-col gap-5">
    <!-- Icon -->
    <div
      :class="[
        'flex h-12 w-12 items-center justify-center rounded-xl transition-colors duration-300',
        feature.highlight
          ? 'bg-primary/15 text-primary'
          : 'bg-gray-100/80 text-gray-500 dark:bg-white/10 dark:text-gray-400',
      ]"
    >
      <Icon :icon="feature.icon" class="h-6 w-6" />
    </div>

    <!-- Title + description -->
    <div>
      <h2 class="text-xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">
        {{ feature.title }}
      </h2>
      <p class="mt-2 text-sm leading-relaxed text-gray-500 dark:text-gray-400">
        {{ feature.description }}
      </p>
    </div>

    <!-- Tips -->
    <ul class="space-y-2.5">
      <li
        v-for="(tip, i) in feature.tips"
        :key="tip"
        class="flex items-start gap-3 text-sm text-gray-600 dark:text-gray-300"
      >
        <div
          :class="[
            'mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-[10px] font-bold',
            feature.highlight
              ? 'bg-primary/15 text-primary'
              : 'bg-gray-100 text-gray-500 dark:bg-white/10 dark:text-gray-400',
          ]"
        >
          {{ i + 1 }}
        </div>
        {{ tip }}
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";

defineProps<{
  feature: {
    icon: string;
    title: string;
    description: string;
    tips: string[];
    highlight?: boolean;
  };
}>();
</script>
