<script setup lang="ts">
import type { HomeQuickUploadOptions } from "@/types/home";
import type { HomeOptionsSection } from "@/utils/home-widget-editor";

import { HOME_QUICK_UPLOAD_STYLES } from "@/utils/home-quick-widgets";

import HomeQuickStylePreview from "./HomeQuickStylePreview.vue";
import HomeStylePicker from "./HomeStylePicker.vue";

const { options, section = "all" } = defineProps<{
  options: HomeQuickUploadOptions;
  section?: HomeOptionsSection;
}>();

const emit = defineEmits<{ change: [options: Partial<HomeQuickUploadOptions>] }>();
</script>

<template>
  <UFormField
    v-if="section !== 'options'"
    label="Quick upload style"
    description="Uploads open a dialog and go to your library root."
  >
    <HomeStylePicker
      label="Quick upload style"
      :styles="HOME_QUICK_UPLOAD_STYLES"
      :selected="options.quickUploadStyle"
      @select="emit('change', { quickUploadStyle: $event })"
    >
      <template #preview="{ value }">
        <HomeQuickStylePreview :upload-style="value" />
      </template>
    </HomeStylePicker>
  </UFormField>
</template>
