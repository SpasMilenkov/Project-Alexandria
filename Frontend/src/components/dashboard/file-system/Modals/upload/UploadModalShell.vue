<script setup lang="ts">
import DirectoryPicker from "@/components/common/DirectoryPicker.vue";

defineProps<{
  title: string;
  wide?: boolean;
  initialId?: string;
  initialName?: string;
  pickerDisabled?: boolean;
}>();

const emit = defineEmits<{ close: [] }>();

const selectedDirectoryId = defineModel<string | undefined>();
</script>

<template>
  <UModal
    :close="{ onClick: () => emit('close') }"
    :title="title"
    :ui="wide ? { body: 'space-y-4', content: 'sm:max-w-2xl' } : { body: 'space-y-4' }"
  >
    <template #body>
      <slot name="intro" />

      <DirectoryPicker
        v-model="selectedDirectoryId"
        :initial-id="initialId"
        :initial-name="initialName"
        :disabled="pickerDisabled"
      />

      <slot />
    </template>

    <template #footer>
      <slot name="footer" />
    </template>
  </UModal>
</template>
