<script setup lang="ts">
defineProps<{
  working: boolean;
  hasSession: boolean;
  hasFailures: boolean;
  destinationName: string;
  cancelDisabled?: boolean;
}>();

defineEmits<{
  cancel: [];
  startOver: [];
  close: [];
  retry: [];
}>();
</script>

<template>
  <div class="flex w-full items-center justify-between gap-3">
    <p class="hidden min-w-0 truncate text-sm text-gray-600 sm:block dark:text-gray-400">
      Uploading to
      <span class="font-medium text-gray-900 dark:text-gray-100">{{ destinationName }}</span>
    </p>

    <div class="ml-auto flex shrink-0 items-center gap-2">
      <UButton
        v-if="working"
        label="Stop"
        icon="i-lucide-x"
        variant="outline"
        color="error"
        @click="$emit('cancel')"
      />
      <UButton
        v-else-if="hasSession"
        label="Start over"
        icon="i-lucide-rotate-ccw"
        variant="ghost"
        color="neutral"
        @click="$emit('startOver')"
      />
      <UButton
        v-else
        label="Cancel"
        variant="outline"
        color="neutral"
        :disabled="cancelDisabled"
        @click="$emit('close')"
      />

      <UButton
        v-if="hasFailures"
        label="Retry failed"
        icon="i-lucide-refresh-cw"
        variant="outline"
        color="neutral"
        @click="$emit('retry')"
      />

      <slot name="primary" />
    </div>
  </div>
</template>
