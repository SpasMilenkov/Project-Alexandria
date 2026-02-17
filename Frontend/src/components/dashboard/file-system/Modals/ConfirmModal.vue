<template>
  <UModal :title="title">
    <template #body>
      <p class="text-gray-700 dark:text-gray-300">{{ message }}</p>
    </template>
    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton
          :label="cancelLabel"
          color="neutral"
          variant="solid"
          @click="emit('close', false)"
        />
        <UButton
          :label="confirmLabel"
          :color="dangerMode ? 'error' : 'primary'"
          :variant="dangerMode ? 'solid' : 'solid'"
          :icon="confirmIcon"
          @click="emit('close', true)"
        />
      </div>
    </template>
  </UModal>
</template>
<script setup lang="ts">
interface Props {
  title?: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  confirmIcon?: string;
  dangerMode?: boolean;
}

withDefaults(defineProps<Props>(), {
  title: "Confirm deletion",
  confirmLabel: "Yes",
  cancelLabel: "No",
  confirmIcon: "mdi-check",
  dangerMode: false,
});

const emit = defineEmits(["close"]);
</script>
<style scoped></style>
