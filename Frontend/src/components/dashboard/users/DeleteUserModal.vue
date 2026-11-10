<template>
  <ConfirmModal
    :open="open"
    title="Confirm deletion"
    :confirm-label="`Delete ${formatCount(ids.length, 'user')}`"
    confirm-icon="i-lucide-trash-2"
    danger-mode
    :loading="loading"
    :alert="{
      title: `Delete ${formatCount(ids.length, 'user')}?`,
      description: 'Accounts will be soft-deleted. Admins can restore them later.',
    }"
    @confirm="emit('confirm')"
    @close="emit('close', $event)"
  />
</template>

<script setup lang="ts">
import { formatCount } from "@/utils/count";

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
