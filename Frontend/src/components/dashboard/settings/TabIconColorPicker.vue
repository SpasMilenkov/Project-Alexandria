<script setup lang="ts">
import { getIcon } from "@iconify/vue";
import { computed } from "vue";

import { resolveAccentRGB } from "@/composables/useTheme";
import { PLACE_GLYPHS } from "@/features/site-icon/places";
import { buildSiteIconSvg, toDataUri } from "@/features/site-icon/render";
import { accentTile, resolveSiteIcon } from "@/features/site-icon/state";
import {
  type ColorName,
  TAB_ICON_MATCH_ACCENT,
  type TabIconColor,
  useSettingsStore,
} from "@/stores/settings";

const settingsStore = useSettingsStore();

const selected = computed({
  get: () => settingsStore.tabIconColor,
  set: (v: TabIconColor) => settingsStore.setTabIconColor(v),
});

const isMatchingAccent = computed(() => selected.value === TAB_ICON_MATCH_ACCENT);

const capitalize = (name: string) => name.charAt(0).toUpperCase() + name.slice(1);

const effectiveColor = computed((): ColorName => {
  if (selected.value === TAB_ICON_MATCH_ACCENT) return settingsStore.accentColor;

  return selected.value;
});

const selectedLabel = computed(() => {
  if (isMatchingAccent.value) return `Matches accent (${capitalize(settingsStore.accentColor)})`;

  return capitalize(effectiveColor.value);
});

// Drawn by the same builder as the real favicon, so the preview can't drift from the tab.
const previewUri = computed(() => {
  const glyph = getIcon(PLACE_GLYPHS.home);

  if (!glyph) return null;

  const state = resolveSiteIcon({
    accentRgb: resolveAccentRGB(effectiveColor.value),
    health: null,
    isOnline: true,
    pageTitle: "Settings",
    place: "home",
    playback: null,
    upload: null,
    uploadOutcome: null,
  });

  return toDataUri(buildSiteIconSvg(state, glyph));
});

const swatchClass = (name: ColorName) => {
  if (selected.value === name) return "ring-2 ring-primary ring-offset-2 scale-105 shadow-md";

  return "hover:scale-105 hover:shadow-sm";
};
</script>

<template>
  <div class="flex flex-col gap-3">
    <div class="flex items-start justify-between gap-4">
      <div class="min-w-0">
        <p class="text-sm font-medium text-gray-900 dark:text-gray-100">Tab icon color</p>
        <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
          The color of Alexandria's icon in your browser tab. Uploads, playback and alerts still
          show on top of it.
        </p>
      </div>

      <span
        class="inline-flex flex-none items-center gap-1.5 text-xs font-medium text-gray-700 dark:text-gray-300"
      >
        <img v-if="previewUri" :src="previewUri" alt="" class="w-4 h-4" />
        {{ selectedLabel }}
      </span>
    </div>

    <div class="flex flex-wrap items-center gap-3 pt-1" role="group" aria-label="Tab icon color">
      <UButton
        label="Match accent"
        size="xs"
        :color="isMatchingAccent ? 'primary' : 'neutral'"
        :variant="isMatchingAccent ? 'solid' : 'outline'"
        :aria-pressed="isMatchingAccent"
        @click="selected = TAB_ICON_MATCH_ACCENT"
      />

      <button
        v-for="color in settingsStore.AVAILABLE_COLORS"
        :key="color.name"
        type="button"
        class="w-8 h-8 rounded-lg transition-all"
        :class="swatchClass(color.name)"
        :style="{ backgroundColor: accentTile(color.value) }"
        :title="capitalize(color.name)"
        :aria-label="capitalize(color.name)"
        :aria-pressed="selected === color.name"
        @click="selected = color.name"
      />
    </div>
  </div>
</template>
