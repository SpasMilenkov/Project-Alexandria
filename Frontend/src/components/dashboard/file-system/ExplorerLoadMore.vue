<template>
  <div class="flex flex-col items-center gap-2 px-4 py-5">
    <div class="flex w-full items-center gap-4">
      <span aria-hidden="true" class="h-px flex-1 bg-gray-200/70 dark:bg-gray-700/70" />

      <UButton
        :label="label"
        color="neutral"
        variant="outline"
        :disabled="loading"
        :aria-busy="loading"
        class="rounded-full px-5"
        @click="emit('load')"
      >
        <template #leading>
          <BlockSpinner v-if="loading" :size="16" />
          <UIcon v-else name="i-lucide-chevron-down" class="size-5 shrink-0" />
        </template>
      </UButton>

      <span aria-hidden="true" class="h-px flex-1 bg-gray-200/70 dark:bg-gray-700/70" />
    </div>

    <p class="text-xs tabular-nums text-gray-600 dark:text-gray-400">
      {{ shown }} of {{ total }} shown
    </p>
  </div>
</template>

<script setup lang="ts">
import BlockSpinner from "@/components/common/BlockSpinner.vue";

const { loading = false } = defineProps<{
  label: string;
  shown: number;
  total: number;
  loading?: boolean;
}>();

const emit = defineEmits<{ load: [] }>();
</script>
