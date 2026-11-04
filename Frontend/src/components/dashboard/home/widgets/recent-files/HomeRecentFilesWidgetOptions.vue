<script setup lang="ts">
import type { HomeOptionsSection } from "@/features/home/editor/styles";
import type { HomeRecentFilesOptions } from "@/types/home";

import { HOME_RECENT_FILES_SOURCES, HOME_RECENT_FILES_STYLES } from "@/features/home/widgets/files";

import HomeStylePicker from "@/components/dashboard/home/options/HomeStylePicker.vue";
import HomeFileStylePreview from "@/components/dashboard/home/options/style-previews/HomeFileStylePreview.vue";

const { options, section = "all" } = defineProps<{
  options: HomeRecentFilesOptions;
  section?: HomeOptionsSection;
}>();
const emit = defineEmits<{ change: [options: Partial<HomeRecentFilesOptions>] }>();
</script>

<template>
  <div class="flex flex-col gap-6">
    <UFormField
      v-if="section !== 'options'"
      label="Recent files style"
      description="Keep this look as you resize the widget."
    >
      <HomeStylePicker
        label="Recent files style"
        :styles="HOME_RECENT_FILES_STYLES"
        :selected="options.recentFilesStyle"
        @select="emit('change', { recentFilesStyle: $event })"
      >
        <template #preview="{ value }">
          <HomeFileStylePreview :recent-style="value" />
        </template>
      </HomeStylePicker>
    </UFormField>

    <UFormField
      v-if="section !== 'style'"
      label="Files to show"
      description="Choose which file changes appear here."
    >
      <USelect
        :model-value="options.recentFilesSource"
        :items="HOME_RECENT_FILES_SOURCES"
        aria-label="Files to show"
        class="w-full"
        @update:model-value="emit('change', { recentFilesSource: $event })"
      />
    </UFormField>
  </div>
</template>
