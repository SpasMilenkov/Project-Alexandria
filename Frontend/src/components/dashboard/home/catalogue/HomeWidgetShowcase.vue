<script setup lang="ts">
import { useMediaQuery } from "@vueuse/core";
import { computed, ref } from "vue";

import { provideHomeWidgetPreview } from "@/composables/useHomeWidgetPreview";
import { HomeWidgetType } from "@/enums/home-widget";
import { useHomeStore } from "@/stores/home";
import { homeWidgetStyleChoices } from "@/utils/home-widget-editor";
import { createHomeWidgetPreview, homeWidgetShowcaseLayout } from "@/utils/home-widget-showcase";

import HomeDashboardGrid from "./HomeDashboardGrid.vue";
import { homeWidgetDefinitions } from "./registry";

const home = useHomeStore();
const smallScreen = useMediaQuery("(max-width: 767px)");
const mobile = ref(smallScreen.value);
const search = ref("");

provideHomeWidgetPreview(createHomeWidgetPreview(home.ownerId ?? ""));

const streamingWidgets = new Set([
  HomeWidgetType.Player,
  HomeWidgetType.TopSongs,
  HomeWidgetType.LatestRecap,
  HomeWidgetType.Collections,
  HomeWidgetType.TranscodingJobs,
]);

const groups = ref(
  homeWidgetDefinitions
    .filter(
      (definition) =>
        import.meta.env.VITE_STREAMING_ENABLED === "true" || !streamingWidgets.has(definition.type),
    )
    .map((definition) => ({
      definition,
      layout: homeWidgetShowcaseLayout(definition.type),
      styles: homeWidgetStyleChoices(definition.type),
    })),
);

const visibleGroups = computed(() => {
  const term = search.value.trim().toLocaleLowerCase();

  return groups.value.filter(({ definition }) =>
    `${definition.label} ${definition.description}`.toLocaleLowerCase().includes(term),
  );
});

const changeStyle = (type: HomeWidgetType, value: number) => {
  const group = groups.value.find((item) => item.definition.type === type);

  if (!group?.styles) return;

  for (const widget of group.layout.widgets)
    widget.options = { ...widget.options, [group.styles.key]: value };
};
</script>

<template>
  <section
    aria-label="Widget showcase"
    class="h-full min-h-0 overflow-y-auto overscroll-contain p-4 md:p-6"
    data-home-showcase
  >
    <div class="mb-8 flex flex-wrap items-start gap-4">
      <div class="min-w-0 flex-1">
        <h2 class="text-lg font-semibold text-gray-900 dark:text-gray-100">
          Every widget, at every size
        </h2>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          Large, medium, then small. Sample content and style changes stay in this catalogue.
        </p>
      </div>

      <div class="flex items-center gap-2" role="group" aria-label="Catalogue preview width">
        <UButton
          v-for="target in ['desktop', 'mobile'] as const"
          :key="target"
          :color="mobile === (target === 'mobile') ? 'primary' : 'neutral'"
          :variant="mobile === (target === 'mobile') ? 'soft' : 'ghost'"
          :aria-pressed="mobile === (target === 'mobile')"
          @click="mobile = target === 'mobile'"
          >{{ target === "desktop" ? "Desktop" : "Mobile" }}</UButton
        >
      </div>

      <UInput
        v-model="search"
        icon="i-heroicons-magnifying-glass"
        placeholder="Find a widget"
        aria-label="Find a showcase widget"
        class="w-full sm:w-64"
      />
    </div>

    <div class="mx-auto space-y-12" :class="{ 'max-w-[420px]': mobile || smallScreen }">
      <section
        v-for="group in visibleGroups"
        :key="group.definition.type"
        :aria-label="`${group.definition.label} samples`"
        :data-showcase-type="group.definition.type"
      >
        <header class="mb-4 flex flex-wrap items-center gap-4">
          <div class="min-w-0 flex-1">
            <h3 class="font-semibold text-gray-900 dark:text-gray-100">
              {{ group.definition.label }}
            </h3>
            <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
              {{ group.definition.description }}
            </p>
          </div>

          <USelect
            v-if="group.styles"
            :model-value="group.layout.widgets[0]!.options[group.styles.key] as number"
            :items="group.styles.choices.map(({ label, value }) => ({ label, value }))"
            :aria-label="`${group.definition.label} sample style`"
            class="w-40"
            @update:model-value="changeStyle(group.definition.type, $event)"
          />
        </header>

        <div inert>
          <HomeDashboardGrid
            :layout="group.layout"
            :editing="false"
            :disabled="true"
            :mobile="mobile || smallScreen"
            :selected-id="null"
          />
        </div>
      </section>

      <p
        v-if="!visibleGroups.length"
        class="py-8 text-center text-sm text-gray-600 dark:text-gray-400"
      >
        No widgets match your search.
      </p>
    </div>
  </section>
</template>
