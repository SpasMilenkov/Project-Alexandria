<script setup lang="ts">
import { computed, ref, watch } from "vue";

import type { HomeWidgetType } from "@/enums/home-widget";

import HomeWidgetCataloguePreview from "@/components/dashboard/home/HomeWidgetCataloguePreview.vue";
import { homeWidgetDefinitions } from "@/components/dashboard/home/registry";

const {
  disabled,
  draggable,
  dragging = false,
  disabledReason = "",
} = defineProps<{
  disabled: boolean;
  draggable: boolean;
  dragging?: boolean;
  disabledReason?: string;
}>();

const emit = defineEmits<{
  add: [type: HomeWidgetType];
  pointerDrag: [type: HomeWidgetType, event: PointerEvent];
}>();

const dragged = ref(false);
const search = ref("");
const category = ref("All");
const categories = ["All", "Files", "Media", "System", "General"];
const definitions = computed(() => {
  const query = search.value.trim().toLocaleLowerCase();

  return homeWidgetDefinitions.filter(
    (definition) =>
      (category.value === "All" || category.value === definition.category) &&
      `${definition.label} ${definition.description}`.toLocaleLowerCase().includes(query),
  );
});

watch(
  () => dragging,
  (active) => {
    if (active) dragged.value = true;
  },
);

const add = (type: HomeWidgetType) => {
  if (!disabled && !dragged.value) emit("add", type);
};

const startDrag = (type: HomeWidgetType, event: PointerEvent) => {
  dragged.value = false;

  if (!disabled && draggable) emit("pointerDrag", type, event);
};
</script>

<template>
  <div class="flex min-h-0 w-full flex-1 flex-col">
    <div class="shrink-0 px-3 pb-3">
      <UInput
        v-model="search"
        icon="i-heroicons-magnifying-glass"
        placeholder="Search widgets"
        aria-label="Search widgets"
        autofocus
        class="w-full"
      />
      <div class="mt-3 flex flex-wrap gap-2" role="group" aria-label="Widget categories">
        <UButton
          v-for="item in categories"
          :key="item"
          size="xs"
          :color="category === item ? 'primary' : 'neutral'"
          :variant="category === item ? 'soft' : 'outline'"
          :aria-pressed="category === item"
          class="rounded-full"
          @click="category = item"
          >{{ item }}</UButton
        >
      </div>
    </div>

    <div
      class="catalogue-cards grid min-h-0 grid-cols-2 gap-2 overflow-y-auto overscroll-contain px-3 pb-3 md:grid-cols-3"
    >
      <div
        v-for="definition in definitions"
        :key="definition.type"
        :data-catalogue-widget="definition.type"
        :data-draggable="draggable && !disabled"
        role="button"
        tabindex="0"
        :aria-label="`Add ${definition.label}`"
        :aria-disabled="disabled"
        class="flex min-w-0 flex-col gap-2 rounded-lg border border-gray-200/70 bg-gray-100/50 p-3 text-left hover:border-primary focus-visible:outline-2 focus-visible:outline-primary dark:border-gray-700/70 dark:bg-gray-800/50"
        :class="{
          'cursor-grab touch-none select-none active:cursor-grabbing': draggable && !disabled,
        }"
        @pointerdown="startDrag(definition.type, $event)"
        @dragstart.prevent
        @click="add(definition.type)"
        @keydown.enter.prevent="!disabled && emit('add', definition.type)"
        @keydown.space.prevent="!disabled && emit('add', definition.type)"
      >
        <HomeWidgetCataloguePreview :type="definition.type" :icon="definition.icon" />
        <p class="text-sm font-semibold text-gray-900 dark:text-gray-100">{{ definition.label }}</p>
        <p class="line-clamp-2 text-xs leading-relaxed text-gray-600 dark:text-gray-400">
          {{ definition.description }}
        </p>
      </div>
    </div>

    <p v-if="!definitions.length" class="p-6 text-center text-sm text-gray-600 dark:text-gray-400">
      No widgets match your search.
    </p>
    <p v-if="disabled" class="shrink-0 px-3 pb-3 text-sm text-gray-600 dark:text-gray-400">
      {{ disabledReason || "This layout has reached its widget limit." }}
    </p>
  </div>
</template>
