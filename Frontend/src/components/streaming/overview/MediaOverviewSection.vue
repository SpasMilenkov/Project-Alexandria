<script setup lang="ts">
import type { RouteLocationRaw } from "vue-router";

import { computed, useId } from "vue";

import BlockSpinner from "@/components/common/BlockSpinner.vue";

import MediaCoverFan from "./MediaCoverFan.vue";

const {
  description = "",
  to = undefined,
  linkLabel = "View all",
  variant = "card",
  emptyWide = false,
} = defineProps<{
  title: string;
  description?: string;
  to?: RouteLocationRaw;
  loading: boolean;
  failed: boolean;
  empty: boolean;
  emptySeed: string;
  emptyWide?: boolean;
  emptyTitle: string;
  emptyDescription: string;
  linkLabel?: string;
  variant?: "card" | "shelf";
}>();

const emit = defineEmits<{ retry: [] }>();
const titleId = useId();

const SECTION_CLASSES = {
  card: "frosted-glass glass-surface rounded-3xl border border-gray-200/70 p-4 sm:p-6 dark:border-gray-700/70",
  shelf: "",
} as const;

// Shelves bleed their scrollers to the sheet edge, so only chrome gets the right gutter.
const gutter = computed(() => (variant === "shelf" ? "pr-4 sm:pr-6" : ""));
</script>

<template>
  <section :aria-labelledby="titleId" class="min-w-0" :class="SECTION_CLASSES[variant]">
    <header
      class="mb-4 flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1"
      :class="gutter"
    >
      <div class="flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-1">
        <h2
          :id="titleId"
          class="font-display text-xl font-semibold tracking-tight text-gray-900 dark:text-gray-100"
        >
          {{ title }}
        </h2>

        <p v-if="description" class="text-sm text-gray-600 dark:text-gray-400">
          {{ description }}
        </p>
      </div>

      <UButton
        v-if="to"
        :to="to"
        color="neutral"
        variant="ghost"
        size="sm"
        trailing-icon="i-mdi-chevron-right"
        class="-mr-2 min-h-11 shrink-0"
      >
        {{ linkLabel
        }}<span v-if="linkLabel === 'View all'" class="sr-only"> {{ title.toLowerCase() }}</span>
      </UButton>
    </header>

    <div v-if="loading" class="flex min-h-40 items-center justify-center" :class="gutter">
      <BlockSpinner
        :size="24"
        :aria-label="`Loading ${title.toLowerCase()}`"
        class="text-gray-500"
      />
    </div>

    <div
      v-else-if="failed"
      role="alert"
      class="flex min-h-40 flex-col items-center justify-center gap-4 text-center"
      :class="gutter"
    >
      <p class="text-sm text-gray-600 dark:text-gray-400">
        Couldn’t load {{ title.toLowerCase() }}.
      </p>

      <UButton color="neutral" variant="outline" class="min-h-11" @click="emit('retry')">
        Try again<span class="sr-only"> loading {{ title.toLowerCase() }}</span>
      </UButton>
    </div>

    <div
      v-else-if="empty"
      class="flex min-h-40 flex-col items-center justify-center gap-2 px-4 py-4 text-center"
      :class="gutter"
    >
      <MediaCoverFan :fallback-seed="emptySeed" size="lg" :wide="emptyWide" class="mb-2" />

      <p class="font-medium text-gray-900 dark:text-gray-100">{{ emptyTitle }}</p>

      <p class="max-w-xs text-sm text-gray-600 dark:text-gray-400">{{ emptyDescription }}</p>

      <slot name="empty-action" />
    </div>

    <slot v-else />
  </section>
</template>
