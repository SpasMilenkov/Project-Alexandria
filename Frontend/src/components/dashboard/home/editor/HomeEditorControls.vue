<script setup lang="ts">
import { ref, watch } from "vue";

import type { HomeEditorTarget, HomeLayout } from "@/types/home";

import { HOME_WIDGET_REGISTRY } from "@/components/dashboard/home/registry";
import { homeWidgetStyleLabel } from "@/utils/home-widget-editor";

const { target } = defineProps<{
  target: HomeEditorTarget;
  layout: HomeLayout;
  separateMobile: boolean;
  showWidgetTitles: boolean;
  showHeaderGreeting: boolean;
  disabled: boolean;
}>();

const emit = defineEmits<{
  select: [instanceId: string];
  hover: [instanceId: string | null];
  separate: [value: boolean];
  titles: [value: boolean];
  greeting: [value: boolean];
  reset: [];
}>();

const confirmReset = ref(false);
const shortcutsOpen = ref(false);

watch(
  () => target,
  () => {
    confirmReset.value = false;
  },
);

const reset = () => {
  emit("reset");
  confirmReset.value = false;
};
</script>

<template>
  <section aria-label="Dashboard layout settings" class="flex min-h-0 flex-1 flex-col">
    <header class="shrink-0 border-b border-gray-200/70 px-4 py-3 dark:border-gray-700/70">
      <h2 class="text-sm font-semibold text-gray-900 dark:text-gray-100">Dashboard</h2>
      <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">
        {{ layout.widgets.length }} widgets · {{ target }} layout
      </p>
    </header>

    <div class="flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto overscroll-contain p-4">
      <div class="flex flex-col gap-4">
        <h3 class="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-gray-400">
          Layout
        </h3>
        <label
          class="flex items-center justify-between gap-4 text-sm text-gray-900 dark:text-gray-100"
        >
          Separate mobile layout
          <USwitch
            :model-value="separateMobile"
            :disabled="disabled"
            aria-label="Use a separate mobile layout"
            @update:model-value="emit('separate', $event)"
          />
        </label>
        <label
          class="flex items-center justify-between gap-4 text-sm text-gray-900 dark:text-gray-100"
        >
          Widget titles
          <USwitch
            :model-value="showWidgetTitles"
            :disabled="disabled"
            aria-label="Show widget titles"
            @update:model-value="emit('titles', $event)"
          />
        </label>

        <label
          class="flex items-center justify-between gap-4 text-sm text-gray-900 dark:text-gray-100"
        >
          Header greeting
          <USwitch
            :model-value="showHeaderGreeting"
            :disabled="disabled"
            aria-label="Show header greeting"
            @update:model-value="emit('greeting', $event)"
          />
        </label>
      </div>

      <p
        v-if="target === 'mobile' && !separateMobile"
        class="text-xs leading-relaxed text-gray-600 dark:text-gray-400"
      >
        This previews your shared layout. Enable a separate mobile layout to edit it independently.
      </p>

      <div class="flex flex-col gap-2">
        <h3 class="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-gray-400">
          Widgets
        </h3>
        <button
          v-for="widget in layout.widgets"
          :key="widget.instanceId"
          type="button"
          :aria-label="`Edit ${HOME_WIDGET_REGISTRY[widget.type].label}`"
          :disabled="disabled || (target === 'mobile' && !separateMobile)"
          class="flex min-w-0 items-center gap-3 rounded-lg p-2 text-left text-gray-900 hover:bg-gray-100/70 focus-visible:outline-2 focus-visible:outline-primary dark:text-gray-100 dark:hover:bg-gray-800/70"
          @click="emit('select', widget.instanceId)"
          @mouseenter="emit('hover', widget.instanceId)"
          @mouseleave="emit('hover', null)"
          @focus="emit('hover', widget.instanceId)"
          @blur="emit('hover', null)"
        >
          <span
            class="flex size-8 shrink-0 items-center justify-center rounded-lg bg-gray-100/70 dark:bg-gray-800/70"
          >
            <UIcon :name="HOME_WIDGET_REGISTRY[widget.type].icon" class="size-4" />
          </span>
          <span class="min-w-0 flex-1">
            <span class="block truncate text-sm font-medium">{{
              HOME_WIDGET_REGISTRY[widget.type].label
            }}</span>
            <span class="block truncate text-xs text-gray-600 dark:text-gray-400">{{
              homeWidgetStyleLabel(widget)
            }}</span>
          </span>
          <span class="text-xs tabular-nums text-gray-600 dark:text-gray-400"
            >{{ widget.width }} × {{ widget.height }}</span
          >
        </button>
        <p v-if="!layout.widgets.length" class="py-4 text-sm text-gray-600 dark:text-gray-400">
          Add your first widget to get started.
        </p>
      </div>
    </div>

    <footer
      class="flex shrink-0 flex-col gap-3 border-t border-gray-200/70 p-4 dark:border-gray-700/70"
    >
      <UCollapsible v-model:open="shortcutsOpen">
        <UButton
          color="neutral"
          variant="ghost"
          icon="i-heroicons-command-line"
          class="w-full justify-start"
          >Shortcuts</UButton
        >
        <template #content>
          <dl
            class="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 py-3 text-xs text-gray-600 dark:text-gray-400"
          >
            <dt>Tab</dt>
            <dd>Select a widget</dd>
            <dt>Arrow keys</dt>
            <dd>Move one cell</dd>
            <dt>Shift + arrows</dt>
            <dd>Resize one cell</dd>
            <dt>Delete</dt>
            <dd>Remove selected widget</dd>
            <dt>Ctrl/Cmd + D</dt>
            <dd>Duplicate selected widget</dd>
            <dt>Ctrl/Cmd + Z</dt>
            <dd>Undo</dd>
            <dt>Ctrl/Cmd + Shift + Z</dt>
            <dd>Redo (also Ctrl + Y)</dd>
            <dt>Escape</dt>
            <dd>Cancel drag or leave editor</dd>
          </dl>
        </template>
      </UCollapsible>

      <div
        v-if="confirmReset"
        class="flex flex-col gap-3"
        role="group"
        aria-label="Confirm layout reset"
      >
        <p class="text-xs text-gray-600 dark:text-gray-400">
          Reset this {{ target }} layout to its defaults? Changes are saved with Done.
        </p>
        <div class="flex gap-2">
          <UButton color="error" variant="outline" :disabled="disabled" @click="reset"
            >Reset layout</UButton
          >
          <UButton color="neutral" variant="ghost" @click="confirmReset = false"
            >Keep layout</UButton
          >
        </div>
      </div>
      <UButton
        v-else
        color="error"
        variant="ghost"
        icon="i-heroicons-arrow-path"
        class="justify-start"
        :disabled="disabled || (target === 'mobile' && !separateMobile)"
        @click="confirmReset = true"
        >Reset this layout</UButton
      >
    </footer>
  </section>
</template>
