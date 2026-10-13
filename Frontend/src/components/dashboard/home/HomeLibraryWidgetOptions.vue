<script setup lang="ts">
import type { HomeLibraryOptions } from "@/types/home";
import type { HomeOptionsSection } from "@/utils/home-widget-editor";

import { HomeShortcutGroup } from "@/enums/home-widget";
import { HOME_LIBRARY_STYLES, homeLibraryPresetChange } from "@/utils/home-library";

import HomeLibraryDestinationEditor from "./HomeLibraryDestinationEditor.vue";
import HomeLibraryStylePreview from "./HomeLibraryStylePreview.vue";
import HomeStylePicker from "./HomeStylePicker.vue";

const { options, section = "all" } = defineProps<{
  options: HomeLibraryOptions;
  section?: HomeOptionsSection;
}>();
const emit = defineEmits<{ change: [options: Partial<HomeLibraryOptions>] }>();

const groups = [
  { label: "Files and folders", value: HomeShortcutGroup.Files },
  { label: "Music and videos", value: HomeShortcutGroup.Media },
  { label: "Files and media", value: HomeShortcutGroup.Mixed },
  { label: "Custom", value: HomeShortcutGroup.Custom },
];
</script>

<template>
  <UFormField
    v-if="section !== 'options'"
    label="Library shortcuts style"
    description="Choose how your destinations are arranged."
  >
    <HomeStylePicker
      label="Library shortcuts style"
      :styles="HOME_LIBRARY_STYLES"
      :selected="options.libraryStyle"
      :columns="2"
      @select="emit('change', { libraryStyle: $event })"
    >
      <template #preview="{ value }">
        <HomeLibraryStylePreview :library-style="value" />
      </template>
    </HomeStylePicker>
  </UFormField>

  <UFormField
    v-if="section !== 'style'"
    label="Shortcut group"
    description="Choose which destinations appear here."
  >
    <USelect
      :model-value="options.shortcutGroup"
      :items="groups"
      aria-label="Shortcut group"
      class="w-full"
      @update:model-value="emit('change', homeLibraryPresetChange(options, $event))"
    />
  </UFormField>

  <HomeLibraryDestinationEditor
    v-if="section !== 'style'"
    :options="options"
    @change="emit('change', $event)"
  />
</template>
