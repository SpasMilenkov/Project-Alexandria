<script setup lang="ts">
import DirectoryPicker from "@/components/common/DirectoryPicker.vue";
import { glassModalBody, glassModalContentWide } from "@/utils/modalUi";

defineProps<{
  title: string;
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
    :ui="{ body: glassModalBody, content: glassModalContentWide }"
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
