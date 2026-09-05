<script setup lang="ts">
defineProps<{
  working: boolean;
  hasSession: boolean;
  hasFailures: boolean;
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
  <div class="flex justify-between w-full gap-2">
    <div class="flex gap-2">
      <UButton
        v-if="working"
        label="Cancel"
        icon="i-lucide-x"
        variant="outline"
        color="error"
        @click="$emit('cancel')"
      />
      <UButton
        v-else-if="hasSession"
        label="Start Over"
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
    </div>

    <div class="flex gap-2">
      <UButton
        v-if="hasFailures"
        label="Retry Failed"
        icon="i-lucide-refresh-cw"
        variant="outline"
        color="neutral"
        @click="$emit('retry')"
      />
      <slot name="primary" />
    </div>
  </div>
</template>
