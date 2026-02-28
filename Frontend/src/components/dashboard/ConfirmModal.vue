<template>
  <UModal
    :open="open"
    :title="title"
    :description="description"
    :close="{ onClick: () => emit('close', false) }"
  >
    <template #body>
      <div class="space-y-4 p-1">
        <UAlert
          v-if="alert"
          :color="alert.color ?? (dangerMode ? 'error' : 'warning')"
          variant="subtle"
          :icon="alert.icon ?? 'i-lucide-triangle-alert'"
          :title="alert.title"
          :description="alert.description"
        />
        <slot />
      </div>
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton
          :label="cancelLabel"
          color="neutral"
          variant="outline"
          @click="emit('close', false)"
        />
        <UButton
          :label="confirmLabel"
          :color="dangerMode ? 'error' : confirmColor"
          variant="solid"
          :icon="confirmIcon"
          :loading="loading"
          @click="emit('confirm')"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
interface AlertProps {
  title: string;
  description?: string;
  color?: string;
  icon?: string;
}

interface Props {
  open?: boolean;
  title?: string;
  description?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  confirmIcon?: string;
  confirmColor?: string;
  dangerMode?: boolean;
  loading?: boolean;
  alert?: AlertProps;
}

withDefaults(defineProps<Props>(), {
  cancelLabel: "Cancel",
  confirmColor: "primary",
  confirmIcon: "i-lucide-check",
  confirmLabel: "Confirm",
  dangerMode: false,
  loading: false,
  open: false,
  title: "Confirm",
});

// 'confirm' = the action button was clicked (submit the inner form or proceed)
// 'close'   = modal should close (cancel, backdrop, X button)
const emit = defineEmits<{
  confirm: [];
  close: [confirmed: boolean];
}>();
</script>
