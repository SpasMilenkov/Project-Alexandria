<script setup lang="ts">
import type { HomeStorageOptions } from "@/types/home";
import type { HomeOptionsSection } from "@/utils/home-widget-editor";

import { HOME_STORAGE_STYLES } from "@/utils/home-file-widgets";

import HomeFileStylePreview from "./HomeFileStylePreview.vue";
import HomeStylePicker from "./HomeStylePicker.vue";

const { options, section = "all" } = defineProps<{
  options: HomeStorageOptions;
  section?: HomeOptionsSection;
}>();
const emit = defineEmits<{ change: [options: Partial<HomeStorageOptions>] }>();
</script>

<template>
  <UFormField
    v-if="section !== 'options'"
    label="Storage style"
    description="Keep this look as you resize the widget."
  >
    <HomeStylePicker
      label="Storage style"
      :styles="HOME_STORAGE_STYLES"
      :selected="options.storageStyle"
      @select="emit('change', { storageStyle: $event })"
    >
      <template #preview="{ value }">
        <HomeFileStylePreview :storage-style="value" />
      </template>
    </HomeStylePicker>
  </UFormField>
</template>
