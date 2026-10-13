<script setup lang="ts">
import type { HomeWidget, HomeWidgetOptions } from "@/types/home";
import type { HomeOptionsSection } from "@/utils/home-widget-editor";

import { HOME_WIDGET_REGISTRY } from "./registry";

const { widget, section = "all" } = defineProps<{
  widget: HomeWidget;
  section?: HomeOptionsSection;
}>();
const emit = defineEmits<{ change: [options: Partial<HomeWidgetOptions>] }>();
</script>

<template>
  <component
    v-if="HOME_WIDGET_REGISTRY[widget.type].optionsComponent"
    :is="HOME_WIDGET_REGISTRY[widget.type].optionsComponent"
    :options="widget.options"
    :section="section"
    @change="emit('change', $event)"
  />
</template>
