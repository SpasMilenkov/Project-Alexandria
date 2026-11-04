<script setup lang="ts">
import type { HomeOptionsSection } from "@/features/home/editor/styles";
import type { HomeQuickAccessOptions } from "@/types/home";

import { HOME_QUICK_ACCESS_STYLES } from "@/features/home/widgets/quick";

import HomeStylePicker from "@/components/dashboard/home/options/HomeStylePicker.vue";
import HomeQuickStylePreview from "@/components/dashboard/home/options/style-previews/HomeQuickStylePreview.vue";

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
