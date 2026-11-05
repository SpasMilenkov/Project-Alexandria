<script setup lang="ts">
import DirectoryPicker from "@/components/common/DirectoryPicker.vue";
import { glassModalBody, glassModalContentWide } from "@/utils/modal-ui";

defineProps<{
  title: string;
  description: string;
  initialId?: string;
  initialName?: string;
  pickerDisabled?: boolean;
}>();

const emit = defineEmits<{ close: [] }>();

const selectedDirectoryId = defineModel<string | undefined>();

const destinationName = defineModel<string>("destinationName", { default: "Home" });
</script>

<template>
  <UModal
    :close="{ onClick: () => emit('close') }"
    :title="title"
    :description="description"
    :ui="{ body: glassModalBody, content: glassModalContentWide }"
  >
    <template #body>
      <slot />

      <DirectoryPicker
        v-model="selectedDirectoryId"
        v-model:destination-name="destinationName"
        :initial-id="initialId"
        :initial-name="initialName"
        :disabled="pickerDisabled"
      />
    </template>

    <template #footer>
      <slot name="footer" />
    </template>
  </UModal>
</template>
