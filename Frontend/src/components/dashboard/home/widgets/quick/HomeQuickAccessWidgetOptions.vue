<script setup lang="ts">
import type { HomeQuickAccessOptions } from "@/types/home";
import type { HomeOptionsSection } from "@/utils/home-widget-editor";

import { HOME_QUICK_ACCESS_STYLES } from "@/utils/home-quick-widgets";

import HomeQuickStylePreview from "./HomeQuickStylePreview.vue";
import HomeStylePicker from "./HomeStylePicker.vue";

const { options, section = "all" } = defineProps<{
  options: HomeQuickAccessOptions;
  section?: HomeOptionsSection;
}>();

const emit = defineEmits<{ change: [options: Partial<HomeQuickAccessOptions>] }>();
</script>

<template>
  <UFormField
    v-if="section !== 'options'"
    label="Quick access style"
    description="Favorite folders follow this look as you resize."
  >
    <HomeStylePicker
      label="Quick access style"
      :styles="HOME_QUICK_ACCESS_STYLES"
      :selected="options.quickAccessStyle"
      @select="emit('change', { quickAccessStyle: $event })"
    >
      <template #preview="{ value }">
        <HomeQuickStylePreview :access-style="value" />
      </template>
    </HomeStylePicker>
  </UFormField>
</template>
