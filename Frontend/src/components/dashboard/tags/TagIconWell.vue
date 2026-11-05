<template>
  <div
    class="flex shrink-0 items-center justify-center rounded-lg transition-colors duration-150"
    :class="[sizeClass, isSelected ? 'bg-primary text-white' : 'tag-icon-well']"
    :style="{ '--tag-color': color }"
  >
    <UIcon v-if="isSelected" name="i-lucide-check" :class="glyphClass" />
    <Icon v-else :icon="getIconByValue(icon) || 'mdi:tag'" :class="glyphClass" />
  </div>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { computed } from "vue";

import { getIconByValue } from "@/utils/files/icons";

const {
  color,
  icon,
  isSelected = false,
  size = "md",
} = defineProps<{
  color: string;
  icon: string;
  isSelected?: boolean;
  size?: "xs" | "sm" | "md";
}>();

const SIZE_CLASSES = {
  md: { glyph: "w-5 h-5", well: "w-10 h-10" },
  sm: { glyph: "w-4 h-4", well: "w-9 h-9" },
  xs: { glyph: "w-4 h-4", well: "w-7 h-7" },
};

const sizeClass = computed(() => SIZE_CLASSES[size].well);

const glyphClass = computed(() => SIZE_CLASSES[size].glyph);
</script>

<style scoped>
.tag-icon-well {
  background-color: color-mix(in srgb, var(--tag-color) 18%, transparent);
  color: var(--tag-color);
}
</style>
