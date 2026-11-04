<script setup lang="ts">
import { computed, ref, watch } from "vue";

import type { HomeWidgetSize } from "@/enums/home-widget";
import type { HomeLayout, HomeWidgetOptions as HomeWidgetOptionsValue } from "@/types/home";

import HomeWidgetOptions from "@/components/dashboard/home/HomeWidgetOptions.vue";
import { HOME_WIDGET_REGISTRY } from "@/components/dashboard/home/registry";
import { HOME_GRID_MAX_HEIGHT, HOME_GRID_MAX_ROWS, homeWidgetFootprint } from "@/utils/home-grid";
import { HOME_SIZES } from "@/utils/home-layout";
import { homeWidgetHasStyles, homeWidgetStyleLabel } from "@/utils/home-widget-editor";
import { homeWidgetMinimum } from "@/utils/home-widget-sizing";

const { layout, selectedId, disabled } = defineProps<{
  layout: HomeLayout;
  selectedId: string | null;
  disabled: boolean;
  canDuplicate: boolean;
}>();

const emit = defineEmits<{
  back: [];
  options: [instanceId: string, options: Partial<HomeWidgetOptionsValue>];
  preset: [instanceId: string, size: HomeWidgetSize];
  remove: [instanceId: string];
  nudge: [instanceId: string, x: number, y: number];
  resize: [instanceId: string, x: number, y: number];
  title: [instanceId: string, value: boolean | null];
  duplicate: [instanceId: string];
}>();

const tab = ref("style");
const widget = computed(() => layout.widgets.find((item) => item.instanceId === selectedId));
const definition = computed(() => widget.value && HOME_WIDGET_REGISTRY[widget.value.type]);
const hasStyles = computed(() => widget.value && homeWidgetHasStyles(widget.value.type));
const tabs = computed(() => [
  { label: "Style", value: "style", disabled: !hasStyles.value },
  { label: "Options", value: "options" },
  { label: "Layout", value: "layout" },
]);

watch(
  () => widget.value?.type,
  () => {
    tab.value = hasStyles.value ? "style" : "options";
  },
  { immediate: true },
);

const titleChoices = [
  { label: "Default", value: "inherit" },
  { label: "Show", value: "show" },
  { label: "Hide", value: "hide" },
];

const titleChoice = computed(() => {
  if (widget.value?.showTitle === true) return "show";
  if (widget.value?.showTitle === false) return "hide";

  return "inherit";
});

const changeTitle = (value: string) => {
  if (!widget.value || disabled) return;

  let showTitle: boolean | null = null;

  if (value === "show") showTitle = true;
  else if (value === "hide") showTitle = false;

  emit("title", widget.value.instanceId, showTitle);
};

const presets = computed(() => {
  if (!widget.value) return [];

  return HOME_SIZES.filter((size) => definition.value?.sizes.includes(size.value)).map((size) => ({
    ...size,
    ...homeWidgetFootprint(size.value, layout.columns),
  }));
});

const fields = computed(() => {
  const item = widget.value;

  if (!item) return [];

  const minimum = homeWidgetMinimum(item.type, layout.columns);

  return [
    {
      label: "Width",
      value: item.width,
      min: minimum.width,
      max: layout.columns - item.x,
      x: 1,
      y: 0,
      resize: true,
      decrease: "Decrease width",
      increase: "Increase width",
    },
    {
      label: "Height",
      value: item.height,
      min: minimum.height,
      max: Math.min(HOME_GRID_MAX_HEIGHT, HOME_GRID_MAX_ROWS - item.y),
      x: 0,
      y: 1,
      resize: true,
      decrease: "Decrease height",
      increase: "Increase height",
    },
    {
      label: "Column",
      value: item.x + 1,
      min: 1,
      max: layout.columns - item.width + 1,
      x: 1,
      y: 0,
      resize: false,
      decrease: "Move widget left",
      increase: "Move widget right",
    },
    {
      label: "Row",
      value: item.y + 1,
      min: 1,
      max: HOME_GRID_MAX_ROWS - item.height + 1,
      x: 0,
      y: 1,
      resize: false,
      decrease: "Move widget up",
      increase: "Move widget down",
    },
  ];
});

const step = (field: (typeof fields.value)[number], delta: number) => {
  if (!widget.value || disabled) return;

  if (field.resize) {
    emit("resize", widget.value.instanceId, field.x * delta, field.y * delta);

    return;
  }

  emit("nudge", widget.value.instanceId, field.x * delta, field.y * delta);
};
</script>

<template>
  <section
    v-if="widget && definition"
    aria-label="Widget editing controls"
    class="flex min-h-0 flex-1 flex-col"
  >
    <header
      class="flex shrink-0 items-center gap-2 border-b border-gray-200/70 px-4 py-3 dark:border-gray-700/70"
    >
      <UButton
        color="neutral"
        variant="ghost"
        icon="i-lucide-chevron-left"
        aria-label="Back to dashboard settings"
        @click="emit('back')"
      />
      <UIcon :name="definition.icon" class="size-5 shrink-0 text-gray-600 dark:text-gray-400" />
      <div class="min-w-0 flex-1">
        <h2 class="truncate text-sm font-semibold text-gray-900 dark:text-gray-100">
          {{ definition.label }}
        </h2>
        <p class="truncate text-xs text-gray-600 dark:text-gray-400">
          {{ homeWidgetStyleLabel(widget) }}
        </p>
      </div>
      <UButton
        color="neutral"
        variant="ghost"
        icon="i-lucide-copy"
        aria-label="Duplicate widget"
        title="Duplicate (Ctrl/Cmd+D)"
        :disabled="disabled || !canDuplicate"
        class="justify-center"
        @click="emit('duplicate', widget.instanceId)"
      />

      <UButton
        color="error"
        variant="ghost"
        icon="i-heroicons-trash"
        aria-label="Remove widget"
        :disabled="disabled"
        @click="emit('remove', widget.instanceId)"
      />
    </header>

    <UTabs
      v-model="tab"
      :items="tabs"
      :content="false"
      size="sm"
      class="shrink-0 px-4 pt-3"
      :ui="{ list: 'w-full', trigger: 'flex-1' }"
      aria-label="Widget settings"
    />

    <fieldset
      :disabled="disabled"
      class="flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto overscroll-contain p-4"
    >
      <HomeWidgetOptions
        v-if="tab === 'style' || tab === 'options'"
        :key="`${widget.instanceId}:${tab}`"
        :widget="widget"
        :section="tab === 'style' ? 'style' : 'options'"
        @change="emit('options', widget!.instanceId, $event)"
      />

      <UFormField
        v-if="tab === 'options'"
        label="Widget title"
        description="Default follows the dashboard setting."
      >
        <USelect
          :model-value="titleChoice"
          :items="titleChoices"
          :disabled="disabled"
          aria-label="Widget title"
          class="w-full"
          @update:model-value="changeTitle"
        />
      </UFormField>

      <template v-if="tab === 'layout'">
        <div>
          <h3
            class="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-gray-400"
          >
            Size presets
          </h3>
          <div class="grid grid-cols-3 gap-2" role="group" aria-label="Widget size">
            <button
              v-for="preset in presets"
              :key="preset.value"
              type="button"
              :disabled="disabled"
              :aria-label="`${preset.label} widget`"
              :aria-pressed="widget.width === preset.width && widget.height === preset.height"
              class="rounded-lg border border-gray-200/70 px-2 py-3 text-sm text-gray-900 hover:bg-gray-100/70 focus-visible:outline-2 focus-visible:outline-primary dark:border-gray-700/70 dark:text-gray-100 dark:hover:bg-gray-800/70"
              :class="{
                'ring-1 ring-primary':
                  widget.width === preset.width && widget.height === preset.height,
              }"
              @click="emit('preset', widget.instanceId, preset.value)"
            >
              <span class="block font-semibold">{{ preset.label }}</span>
              <span class="mt-1 block text-xs text-gray-600 dark:text-gray-400"
                >{{ preset.width }} × {{ preset.height }}</span
              >
            </button>
          </div>
        </div>

        <div class="flex flex-col gap-3">
          <div
            v-for="field in fields"
            :key="field.label"
            class="flex items-center justify-between gap-4"
          >
            <span class="text-sm text-gray-900 dark:text-gray-100">{{ field.label }}</span>
            <div
              class="flex items-center rounded-lg border border-gray-200/70 dark:border-gray-700/70"
            >
              <UButton
                color="neutral"
                variant="ghost"
                icon="i-lucide-minus"
                :aria-label="field.decrease"
                :disabled="disabled || field.value <= field.min"
                class="min-h-9 min-w-9 justify-center"
                @click="step(field, -1)"
              />
              <span
                class="min-w-9 text-center text-sm tabular-nums text-gray-900 dark:text-gray-100"
                >{{ field.value }}</span
              >
              <UButton
                color="neutral"
                variant="ghost"
                icon="i-heroicons-plus"
                :aria-label="field.increase"
                :disabled="disabled || field.value >= field.max"
                class="min-h-9 min-w-9 justify-center"
                @click="step(field, 1)"
              />
            </div>
          </div>
        </div>

        <p class="text-xs text-gray-600 dark:text-gray-400">
          Column {{ widget.x + 1 }}, row {{ widget.y + 1 }}
        </p>
        <p class="text-xs leading-relaxed text-gray-600 dark:text-gray-400">
          {{ widget.width }} columns × {{ widget.height }} rows. Drag any edge or corner to resize
          directly on the canvas.
        </p>
      </template>
    </fieldset>
  </section>
</template>
