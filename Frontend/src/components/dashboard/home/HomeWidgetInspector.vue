<script setup lang="ts">
import { computed, ref } from "vue";

import type { HomeWidgetSize } from "@/enums/home-widget";
import type { HomeLayout, HomeWidget } from "@/types/home";

import HomeWidgetOptions from "@/components/dashboard/home/HomeWidgetOptions.vue";
import { HOME_WIDGET_REGISTRY } from "@/components/dashboard/home/registry";
import { HOME_GRID_MAX_ROWS, homeWidgetFootprint } from "@/utils/home-grid";
import { HOME_SIZES } from "@/utils/home-layout";
import { glassModalContent } from "@/utils/modalUi";

const { layout, selectedId, disabled } = defineProps<{
  layout: HomeLayout;
  selectedId: string | null;
  disabled: boolean;
}>();

const emit = defineEmits<{
  select: [instanceId: string];
  change: [widget: HomeWidget];
  preset: [instanceId: string, size: HomeWidgetSize];
  remove: [instanceId: string];
  nudge: [instanceId: string, x: number, y: number];
}>();

const optionsOpen = ref(false);

const widget = computed(() => layout.widgets.find((item) => item.instanceId === selectedId));

const widgetChoices = computed(() =>
  layout.widgets.map((item, index) => ({
    label: `${HOME_WIDGET_REGISTRY[item.type].label} ${index + 1}`,
    value: item.instanceId,
  })),
);

const sizes = computed(() => {
  if (!widget.value) return [];

  const supported = HOME_SIZES.filter((size) =>
    HOME_WIDGET_REGISTRY[widget.value!.type].sizes.includes(size.value),
  );

  return [{ label: "Custom", value: "custom", disabled: true }, ...supported];
});

const selectedPreset = computed(() => {
  if (!widget.value) return "custom";

  const footprint = homeWidgetFootprint(widget.value.size, layout.columns);

  if (widget.value.width !== footprint.width || widget.value.height !== footprint.height)
    return "custom";

  return widget.value.size;
});

const selectPreset = (size: HomeWidgetSize | string) => {
  if (widget.value && typeof size === "number") emit("preset", widget.value.instanceId, size);
};

const directions = [
  { label: "Move widget left", icon: "i-heroicons-arrow-left", x: -1, y: 0 },
  { label: "Move widget up", icon: "i-heroicons-arrow-up", x: 0, y: -1 },
  { label: "Move widget down", icon: "i-heroicons-arrow-down", x: 0, y: 1 },
  { label: "Move widget right", icon: "i-heroicons-arrow-right", x: 1, y: 0 },
];

const cannotMove = (x: number, y: number) => {
  if (!widget.value || disabled) return true;

  const item = widget.value;

  return (
    item.x + x < 0 ||
    item.x + x + item.width > layout.columns ||
    item.y + y < 0 ||
    item.y + y + item.height > HOME_GRID_MAX_ROWS
  );
};
</script>

<template>
  <section aria-label="Widget editing controls" class="min-w-0">
    <fieldset v-if="widget" :disabled="disabled" class="grid grid-cols-[minmax(0,1fr)_7rem] gap-4">
      <UFormField label="Selected widget" class="min-w-0">
        <USelect
          :model-value="widget.instanceId"
          :items="widgetChoices"
          :disabled="disabled"
          aria-label="Selected widget"
          class="w-full"
          @update:model-value="emit('select', $event)"
        />
      </UFormField>

      <UFormField label="Preset" class="min-w-0">
        <USelect
          :model-value="selectedPreset"
          :items="sizes"
          :disabled="disabled"
          aria-label="Widget size"
          class="w-full"
          @update:model-value="selectPreset"
        />
      </UFormField>

      <div class="col-span-2 flex flex-col gap-2">
        <div class="flex items-center justify-between gap-2">
          <span class="text-sm text-gray-900 dark:text-gray-100">Position</span>
          <span class="text-xs text-gray-600 dark:text-gray-400">
            Column {{ widget.x + 1 }}, row {{ widget.y + 1 }}
          </span>
        </div>

        <div class="flex gap-2">
          <UButton
            v-for="direction in directions"
            :key="direction.label"
            color="neutral"
            variant="outline"
            :icon="direction.icon"
            :aria-label="direction.label"
            :disabled="cannotMove(direction.x, direction.y)"
            class="min-h-10 min-w-10 justify-center"
            @click="emit('nudge', widget!.instanceId, direction.x, direction.y)"
          />

          <UButton
            color="error"
            variant="outline"
            icon="i-heroicons-trash"
            aria-label="Remove widget"
            :disabled="disabled"
            class="ml-auto min-h-10 min-w-10 justify-center"
            @click="emit('remove', widget!.instanceId)"
          />
        </div>
      </div>

      <p class="col-span-2 text-xs text-gray-600 dark:text-gray-400">
        {{ widget.width }} columns × {{ widget.height }} rows. Drag an edge or corner to resize.
      </p>

      <UButton
        color="neutral"
        variant="outline"
        icon="i-heroicons-cog-6-tooth"
        :disabled="disabled"
        class="col-span-2 justify-center"
        @click="optionsOpen = true"
        >Widget options</UButton
      >
    </fieldset>

    <p v-else class="text-sm text-gray-600 dark:text-gray-400">
      Add a widget to start building your layout.
    </p>
  </section>

  <UModal
    v-if="widget"
    v-model:open="optionsOpen"
    title="Widget options"
    :description="HOME_WIDGET_REGISTRY[widget.type].label"
    :ui="{ content: glassModalContent }"
  >
    <template #body>
      <fieldset :disabled="disabled">
        <HomeWidgetOptions
          :widget="widget"
          @change="emit('change', { ...widget!, options: $event })"
        />
      </fieldset>
    </template>

    <template #footer>
      <UButton color="neutral" variant="outline" @click="optionsOpen = false">
        Close options
      </UButton>
    </template>
  </UModal>
</template>
