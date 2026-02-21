<template>
  <ConfirmModal
    :open="open"
    title="Confirm deletion"
    :confirm-label="`Delete ${ids.length} user${ids.length !== 1 ? 's' : ''}`"
    confirm-icon="i-lucide-trash-2"
    danger-mode
    :loading="loading"
    :alert="{
      title: `Delete ${ids.length} user${ids.length !== 1 ? 's' : ''}?`,
      description:
        'Accounts will be soft-deleted. Admins can restore them later.',
    }"
    @confirm="emit('confirm')"
    @close="emit('close', $event)"
  />
</template>

<script setup lang="ts">
defineProps<{
  open: boolean;
  ids: string[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  confirm: [];
  close: [confirmed: boolean];
}>();
</script>
