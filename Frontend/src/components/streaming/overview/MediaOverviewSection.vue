<script setup lang="ts">
import type { RouteLocationRaw } from "vue-router";

import { useId } from "vue";

const { linkLabel = "View all" } = defineProps<{
  title: string;
  description: string;
  to: RouteLocationRaw;
  loading: boolean;
  failed: boolean;
  empty: boolean;
  emptyIcon: string;
  emptyTitle: string;
  emptyDescription: string;
  linkLabel?: string;
}>();

const emit = defineEmits<{ retry: [] }>();
const titleId = useId();
</script>

<template>
  <section
    :aria-labelledby="titleId"
    class="frosted-glass glass-surface min-w-0 rounded-2xl border border-gray-200/70 p-4 sm:p-6 dark:border-gray-700/70"
  >
    <header class="mb-6 flex items-start justify-between gap-4">
      <div class="min-w-0">
        <h2 :id="titleId" class="text-lg font-semibold text-gray-900 dark:text-gray-100">
          {{ title }}
        </h2>
        <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">{{ description }}</p>
      </div>

      <UButton :to="to" color="neutral" variant="ghost" class="shrink-0 min-h-11">
        {{ linkLabel
        }}<span v-if="linkLabel === 'View all'" class="sr-only"> {{ title.toLowerCase() }}</span>
      </UButton>
    </header>

    <div
      v-if="loading"
      role="status"
      :aria-label="`Loading ${title.toLowerCase()}`"
      class="flex min-h-48 items-center justify-center"
    >
      <UIcon name="i-mdi-loading" class="size-6 animate-spin text-gray-500 dark:text-gray-500" />
    </div>

    <div
      v-else-if="failed"
      role="alert"
      class="flex min-h-48 flex-col items-center justify-center gap-4 text-center"
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
      class="flex min-h-48 flex-col items-center justify-center gap-2 text-center"
    >
      <UIcon :name="emptyIcon" class="mb-2 size-10 text-gray-400 dark:text-gray-600" />
      <p class="font-medium text-gray-900 dark:text-gray-100">{{ emptyTitle }}</p>
      <p class="max-w-sm text-sm text-gray-600 dark:text-gray-400">{{ emptyDescription }}</p>
    </div>

    <slot v-else />
  </section>
</template>
