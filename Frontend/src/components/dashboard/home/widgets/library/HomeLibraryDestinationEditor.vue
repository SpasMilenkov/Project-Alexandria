<script setup lang="ts">
import { computed, ref, useId } from "vue";

import type { HomeLibraryOptions, HomeLibraryShortcut } from "@/types/home";

import { useHomeLibraryReorder } from "@/composables/useHomeLibraryReorder";
import { HomeLibraryDestination, HomeLibraryStyle } from "@/enums/home-widget";
import {
  HOME_LIBRARY_DESTINATIONS,
  homeLibraryCustomChange,
  homeLibrarySelection,
} from "@/utils/home-library";

const { options } = defineProps<{ options: HomeLibraryOptions }>();
const emit = defineEmits<{ change: [options: Partial<HomeLibraryOptions>] }>();
const streamingEnabled = import.meta.env.VITE_STREAMING_ENABLED === "true";
const adding = ref(false);
const reorderInstructions = `library-reorder-${useId()}`;

const selection = computed(() => homeLibrarySelection(options));

const rows = computed(() =>
  selection.value.map((shortcut) => ({
    ...shortcut,
    target: HOME_LIBRARY_DESTINATIONS.find((item) => item.destination === shortcut.destination)!,
  })),
);

const available = computed(() =>
  HOME_LIBRARY_DESTINATIONS.filter(
    (target) => !selection.value.some((shortcut) => shortcut.destination === target.destination),
  ).map((target) => ({
    label: target.label,
    value: target.destination,
    icon: target.icon,
  })),
);

const featuredChoices = computed(() => [
  { label: "First shortcut", value: -1 },
  ...rows.value.map((row) => ({
    label: row.label?.trim() || row.target.label,
    value: row.destination,
  })),
]);

const changeSelection = (shortcuts: HomeLibraryShortcut[]) => {
  emit("change", homeLibraryCustomChange(options, shortcuts));
};

const { list, dragging, dropTarget, announcement, start, keyboard } = useHomeLibraryReorder({
  selection: () => selection.value,
  change: changeSelection,
  label: (destination) =>
    HOME_LIBRARY_DESTINATIONS.find((target) => target.destination === destination)?.label ??
    "Shortcut",
});

const remove = (destination: HomeLibraryDestination) => {
  if (selection.value.length <= 1) return;

  changeSelection(selection.value.filter((shortcut) => shortcut.destination !== destination));
};

const rename = (destination: HomeLibraryDestination, label: string) => {
  const value = label.trim() ? label : null;
  const current = selection.value.find((shortcut) => shortcut.destination === destination);

  if (!current || current.label === value) return;

  changeSelection(
    selection.value.map((shortcut) => {
      if (shortcut.destination !== destination) return shortcut;

      return { ...shortcut, label: value };
    }),
  );
};

const add = (destination: HomeLibraryDestination | undefined) => {
  if (destination === undefined || selection.value.length >= 8) return;
  if (!available.value.some((target) => target.value === destination)) return;

  changeSelection([...selection.value, { destination, label: null }]);
  adding.value = false;
};

const feature = (destination: number) => {
  emit("change", { featuredDestination: destination === -1 ? null : destination });
};
</script>

<template>
  <div class="space-y-4">
    <fieldset class="min-w-0 space-y-2">
      <legend class="text-sm font-medium text-gray-900 dark:text-gray-100">Destinations</legend>
      <p class="text-xs text-gray-600 dark:text-gray-400">
        Choose up to eight. Drag the handles to reorder. Reordering or renaming creates a custom
        selection. Leave labels blank to use destination names.
      </p>

      <p :id="reorderInstructions" class="sr-only">
        Drag to reorder, or use the Up and Down arrow keys. Home moves to the start, End moves to
        the end, and Escape cancels a drag.
      </p>

      <p role="status" aria-live="polite" class="sr-only">{{ announcement }}</p>

      <ol
        ref="list"
        class="divide-y divide-gray-200/70 dark:divide-gray-700/70"
        aria-label="Selected destinations"
      >
        <li
          v-for="row in rows"
          :key="row.destination"
          :data-library-destination="row.destination"
          class="library-destination-row relative space-y-2 py-4 first:pt-0"
          :class="{
            'rounded-lg bg-gray-100/70 dark:bg-gray-800/70': dragging === row.destination,
          }"
          :data-drop-edge="
            dragging !== null &&
            dropTarget?.destination === row.destination &&
            dragging !== row.destination
              ? String(dropTarget.after)
              : undefined
          "
        >
          <span
            v-if="
              dragging !== null &&
              dropTarget?.destination === row.destination &&
              dragging !== row.destination
            "
            class="pointer-events-none absolute inset-x-0 h-0.5 rounded-full bg-primary"
            :class="dropTarget.after ? 'bottom-0' : 'top-0'"
          />

          <div class="flex min-w-0 items-center gap-2">
            <UButton
              color="neutral"
              variant="ghost"
              size="sm"
              icon="i-lucide-grip-vertical"
              class="size-10 shrink-0 touch-none justify-center cursor-grab active:cursor-grabbing"
              :aria-label="`Reorder ${row.target.label}`"
              :aria-describedby="reorderInstructions"
              :title="`Drag to reorder ${row.target.label}`"
              :data-library-reorder="row.destination"
              :disabled="rows.length === 1"
              @pointerdown.stop="start(row.destination, $event)"
              @keydown="keyboard(row.destination, $event)"
              @dragstart.prevent
            />

            <UIcon
              :name="row.target.icon"
              class="size-4 shrink-0 text-gray-600 dark:text-gray-400"
            />
            <span
              class="min-w-0 flex-1 truncate text-sm font-medium text-gray-900 dark:text-gray-100"
            >
              {{ row.target.label }}
            </span>

            <div class="flex shrink-0 items-center">
              <UButton
                color="neutral"
                variant="ghost"
                size="xs"
                icon="i-heroicons-x-mark"
                class="justify-center"
                :aria-label="`Remove ${row.target.label}`"
                :title="`Remove ${row.target.label}`"
                :disabled="rows.length === 1"
                @click="remove(row.destination)"
              />
            </div>
          </div>

          <UFormField :label="`Short label for ${row.target.label}`" :ui="{ label: 'sr-only' }">
            <UInput
              :model-value="row.label ?? ''"
              :placeholder="row.target.label"
              :aria-label="`Short label for ${row.target.label}`"
              :maxlength="40"
              size="sm"
              class="w-full"
              @update:model-value="rename(row.destination, String($event))"
            />
          </UFormField>

          <p
            v-if="row.target.media && !streamingEnabled"
            class="text-xs text-gray-600 dark:text-gray-400"
          >
            Hidden while streaming is disabled. Your choice is saved.
          </p>
        </li>
      </ol>
    </fieldset>

    <UFormField label="Add destination" :description="`${rows.length} of 8 shortcuts selected.`">
      <USelectMenu
        v-model:open="adding"
        :model-value="undefined"
        :items="available"
        value-key="value"
        placeholder="Choose a destination"
        aria-label="Add destination"
        :disabled="rows.length >= 8"
        class="w-full"
        @update:model-value="add"
      />
    </UFormField>

    <UFormField
      v-if="options.libraryStyle === HomeLibraryStyle.Spotlight"
      label="Featured destination"
      description="Give one shortcut the largest tile in Spotlight."
    >
      <USelect
        :model-value="options.featuredDestination ?? -1"
        :items="featuredChoices"
        aria-label="Featured destination"
        class="w-full"
        @update:model-value="feature"
      />
    </UFormField>
  </div>
</template>
