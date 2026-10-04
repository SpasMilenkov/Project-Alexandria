<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useDebounceFn, useMediaQuery } from "@vueuse/core";
import { computed, onBeforeUnmount, onMounted, ref, watch, watchEffect } from "vue";

import { useBackgroundImageSync } from "@/composables/useBackgroundImageSync";
import { useSettingsSync } from "@/composables/useSettingsSync";
import { useTheme } from "@/composables/useTheme";
import {
  type ColorName,
  type FontName,
  MAX_BACKGROUND_IMAGE_BYTES,
  useSettingsStore,
} from "@/stores/settings";
import { logger } from "@/utils/logger";

const settingsStore = useSettingsStore();
const { isDark } = useTheme();
const { uploadBackgroundImage, deleteBackgroundImage } = useBackgroundImageSync();
const { saveAppearance } = useSettingsSync();

const { previewVisible = true } = defineProps<{
  previewVisible?: boolean;
}>();

const imageError = ref<string | null>(null);
const isUploading = ref(false);
const fileInputRef = ref<HTMLInputElement | null>(null);

// Computed store bindings
const selectedColor = computed({
  get: () => settingsStore.accentColor,
  set: (v: ColorName) => settingsStore.setAccentColor(v),
});
const selectedBackground = computed({
  get: () => settingsStore.backgroundColor,
  set: (v: string) => settingsStore.setBackgroundColor(v),
});
const imageOpacity = computed({
  get: () => settingsStore.backgroundImageOpacity,
  set: (v: number) => settingsStore.setBackgroundImageOpacity(v),
});
const fontFamily = computed({
  get: () => settingsStore.fontFamily,
  set: (v: FontName) => settingsStore.setFontFamily(v),
});
const cornerRadius = computed({
  get: () => settingsStore.cornerRadius,
  set: (v: number) => settingsStore.setCornerRadius(v),
});
const frostEnabled = computed({
  get: () => settingsStore.frostEnabled,
  set: (v: boolean) => settingsStore.setFrostEnabled(v),
});
const frostStrength = computed({
  get: () => settingsStore.frostStrength,
  set: (v: number) => settingsStore.setFrostStrength(v),
});
const frostDisabledOnMobile = computed({
  get: () => settingsStore.frostDisabledOnMobile,
  set: (v: boolean) => settingsStore.setFrostDisabledOnMobile(v),
});
const transparencyEnabled = computed({
  get: () => settingsStore.transparencyEnabled,
  set: (v: boolean) => settingsStore.setTransparencyEnabled(v),
});
const surfaceOpacity = computed({
  get: () => settingsStore.surfaceOpacity,
  set: (v: number) => settingsStore.setSurfaceOpacity(v),
});
const thumbnailsEnabled = computed({
  get: () => settingsStore.thumbnailsEnabled,
  set: (v: boolean) => settingsStore.setThumbnailsEnabled(v),
});
const gridIconSize = computed({
  get: () => settingsStore.gridIconSize,
  set: (v: number) => settingsStore.setGridIconSize(v),
});
const listIconSize = computed({
  get: () => settingsStore.listIconSize,
  set: (v: number) => settingsStore.setListIconSize(v),
});
const isOpen = computed({
  get: () => settingsStore.isAppearanceSectionOpen,
  set: (v: boolean) => settingsStore.setAppearanceSectionOpen(v),
});

const previewDockOpen = ref(true);

// The phone dock is fixed, so the page needs bottom room exactly matching its real
// height, otherwise trailing content (e.g. the next-section button) slides underneath
// it. The stage reports its measured height through a CSS variable that the settings
// scroll content consumes as bottom padding.
const isPhone = useMediaQuery("(max-width: 767px)");
const stageRef = ref<HTMLElement | null>(null);
const dockHeight = ref(0);

let dockObserver: ResizeObserver | null = null;

onMounted(() => {
  if (!("ResizeObserver" in window) || !stageRef.value) {
    return;
  }
  dockObserver = new ResizeObserver((entries) => {
    dockHeight.value = Math.ceil(entries[0]?.contentRect.height ?? 0);
  });
  dockObserver.observe(stageRef.value);
});

onBeforeUnmount(() => {
  dockObserver?.disconnect();
  dockObserver = null;
});

watchEffect(() => {
  if (typeof document === "undefined") {
    return;
  }
  if (isPhone.value && dockHeight.value > 0) {
    document.documentElement.style.setProperty("--preview-dock-h", `${dockHeight.value}px`);
  } else {
    document.documentElement.style.removeProperty("--preview-dock-h");
  }
});

// Qualitative words for slider readouts (non-technical labels, D4).
const frostWord = (v: number): string => {
  if (v <= 0) {
    return "Off";
  }
  if (v <= 8) {
    return "Subtle";
  }
  if (v <= 16) {
    return "Soft";
  }
  return "Deep frost";
};

const opacityWord = (v: number): string => {
  if (v < 35) {
    return "Airy";
  }
  if (v <= 65) {
    return "Balanced";
  }
  return "Nearly solid";
};

const imageVisibilityWord = (v: number): string => {
  if (v < 0.3) {
    return "Faint";
  }
  if (v <= 0.5) {
    return "Balanced";
  }
  return "Vivid";
};

const gridSizeWord = (v: number): string => {
  if (v <= 32) {
    return "Compact";
  }
  if (v <= 56) {
    return "Comfortable";
  }
  return "Spacious";
};

const listSizeWord = (v: number): string => {
  if (v <= 16) {
    return "Compact";
  }
  if (v <= 28) {
    return "Comfortable";
  }
  return "Spacious";
};

// Discrete steps for the segmented controls, replacing continuous sliders.
// Each value is a representative point inside the range its *Word() function labels.
const frostOptions = [
  { label: "Subtle", value: 4 },
  { label: "Soft", value: 12 },
  { label: "Deep frost", value: 20 },
];
const opacityOptions = [
  { label: "Airy", value: 25 },
  { label: "Balanced", value: 50 },
  { label: "Nearly solid", value: 80 },
];
const imageVisibilityOptions = [
  { label: "Faint", value: 0.2 },
  { label: "Balanced", value: 0.4 },
  { label: "Vivid", value: 0.6 },
];
const gridSizeOptions = [
  { label: "Compact", value: 24 },
  { label: "Comfortable", value: 44 },
  { label: "Spacious", value: 64 },
];
const listSizeOptions = [
  { label: "Compact", value: 12 },
  { label: "Comfortable", value: 20 },
  { label: "Spacious", value: 32 },
];
const radiusOptions = [
  { label: "Sharp", value: 0 },
  { label: "Soft", value: 0.25 },
  { label: "Round", value: 0.5 },
];

const selectedColorLabel = computed(() => {
  const found = settingsStore.AVAILABLE_COLORS.find((c) => c.name === selectedColor.value);
  return found ? found.name.charAt(0).toUpperCase() + found.name.slice(1) : undefined;
});

const accentRgb = computed(() => {
  const found = settingsStore.AVAILABLE_COLORS.find((c) => c.name === selectedColor.value);
  return found ? found.value : "59 130 246";
});

const backgroundLabel = computed(() => {
  if (settingsStore.backgroundImageKey) {
    return "Custom image";
  }
  const found = settingsStore.AVAILABLE_BACKGROUNDS.find(
    (b) => b.name === settingsStore.backgroundColor,
  );
  return found ? `${found.label} background` : undefined;
});

const glassSummary = computed(() => {
  const parts: string[] = [];
  if (frostEnabled.value) {
    parts.push(`${frostWord(frostStrength.value)} blur`);
  }
  if (transparencyEnabled.value) {
    parts.push(`${opacityWord(surfaceOpacity.value)} panels`);
  }
  if (parts.length === 0) {
    return "Solid surfaces";
  }
  return parts.join(", ");
});

const checkOn = (value: string): string => {
  const channel = (c: number): number => {
    const v = c / 255;
    if (v <= 0.03928) {
      return v / 12.92;
    }
    return ((v + 0.055) / 1.055) ** 2.4;
  };
  const [r = 0, g = 0, b = 0] = value.split(" ").map(Number);
  const luminance = 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
  if (luminance > 0.5) {
    return "text-gray-900";
  }
  return "text-white";
};

const persistAppearance = useDebounceFn(async () => {
  await saveAppearance({ ...settingsStore.getAppearanceSettings });
}, 600);

watch(
  () => [
    settingsStore.accentColor,
    settingsStore.backgroundColor,
    settingsStore.backgroundImageOpacity,
    settingsStore.gridIconSize,
    settingsStore.listIconSize,
    settingsStore.frostEnabled,
    settingsStore.frostStrength,
    settingsStore.frostDisabledOnMobile,
    settingsStore.transparencyEnabled,
    settingsStore.surfaceOpacity,
    settingsStore.thumbnailsEnabled,
    settingsStore.fontFamily,
    settingsStore.cornerRadius,
  ],
  persistAppearance,
);

const visibleBackgrounds = computed(() =>
  settingsStore.AVAILABLE_BACKGROUNDS.filter((bg) => {
    if (bg.mode === "both") return true;
    return isDark.value ? bg.mode === "dark" : bg.mode === "light";
  }),
);

watch(isDark, (dark) => {
  const current = settingsStore.AVAILABLE_BACKGROUNDS.find(
    (b) => b.name === settingsStore.backgroundColor,
  );
  if (!current || current.mode === "both") return;
  if (dark && current.mode === "light") settingsStore.setBackgroundColor("system");
  if (!dark && current.mode === "dark") settingsStore.setBackgroundColor("system");
});

const swatchFor = (bg: (typeof settingsStore.AVAILABLE_BACKGROUNDS)[number]) =>
  isDark.value ? bg.darkSwatch : bg.lightSwatch;

const modeLabel = computed(() => (isDark.value ? "dark" : "light"));

// Image upload, S3 flow
const triggerFileInput = () => fileInputRef.value?.click();

const handleFileChange = async (event: Event) => {
  imageError.value = null;
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) return;

  if (!file.type.startsWith("image/")) {
    imageError.value = "Please upload an image file.";
    return;
  }
  if (file.size > MAX_BACKGROUND_IMAGE_BYTES) {
    imageError.value = `Image is too large. Maximum size is ${MAX_BACKGROUND_IMAGE_BYTES / 1024 / 1024} MB.`;
    return;
  }

  try {
    isUploading.value = true;
    await uploadBackgroundImage(file);
  } catch (err) {
    imageError.value = "Upload failed. Please try again.";
    logger.error(err);
  } finally {
    isUploading.value = false;
    if (fileInputRef.value) fileInputRef.value.value = "";
  }
};

const clearImage = async () => {
  imageError.value = null;
  await deleteBackgroundImage();
  await persistAppearance();
};

const imageName = computed(() => {
  if (!settingsStore.backgroundImageKey) return null;
  return settingsStore.backgroundImageKey.split("/").pop() ?? "background";
});

// Live-preview fixtures. The stage demonstrates grid tile size, list row size,
// thumbnails and the accent selection, so plain markup bound to the store shows
// the effect more faithfully than a full file tile with menus and tags.
const gridIconStyle = computed(() => ({
  height: `${gridIconSize.value}px`,
  width: `${gridIconSize.value}px`,
}));

const gridThumbStyle = computed(() => ({
  height: `${Math.round(gridIconSize.value * 1.125)}px`,
  width: `${Math.round(gridIconSize.value * 1.5)}px`,
}));

const listIconStyle = computed(() => ({
  height: `${listIconSize.value}px`,
  width: `${listIconSize.value}px`,
}));

const listRowStyle = computed(() => {
  const padding = 4 + Math.round(listIconSize.value * 0.25);
  return {
    paddingBottom: `${padding}px`,
    paddingTop: `${padding}px`,
  };
});
</script>

<template>
  <UCard class="overflow-hidden frosted-glass glass-surface" :ui="{ body: 'p-2 sm:p-2' }">
    <UCollapsible
      v-model:open="isOpen"
      :unmount-on-hide="false"
      :ui="{
        content:
          'data-[state=open]:animate-[collapsible-down_350ms_ease-in-out] motion-reduce:animate-none!',
      }"
    >
      <UButton
        variant="ghost"
        color="neutral"
        block
        class="justify-between"
        :trailing-icon="isOpen ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'"
      >
        <div class="flex items-center gap-2 min-w-0">
          <Icon icon="mdi:palette-outline" class="w-5 h-5 text-muted shrink-0" />
          <h2 class="text-lg font-semibold shrink-0">Appearance</h2>
          <span class="hidden md:block text-xs text-gray-500 dark:text-gray-400 truncate"
            >Colors, glass effects and file tiles</span
          >
          <span v-if="!isOpen" class="flex flex-wrap items-center gap-1.5 ml-1">
            <span
              class="inline-flex items-center gap-1.5 h-6 px-2.5 rounded-full bg-gray-100 dark:bg-neutral-800 border border-gray-200 dark:border-gray-700 text-xs text-gray-600 dark:text-gray-400"
            >
              <span
                class="w-2 h-2 rounded-full"
                :style="{ backgroundColor: `rgb(${accentRgb})` }"
              />
              {{ selectedColorLabel }}
            </span>
            <span
              v-if="backgroundLabel"
              class="inline-flex items-center h-6 px-2.5 rounded-full bg-gray-100 dark:bg-neutral-800 border border-gray-200 dark:border-gray-700 text-xs text-gray-600 dark:text-gray-400"
            >
              {{ backgroundLabel }}
            </span>
            <span
              class="inline-flex items-center h-6 px-2.5 rounded-full bg-gray-100 dark:bg-neutral-800 border border-gray-200 dark:border-gray-700 text-xs text-gray-600 dark:text-gray-400"
            >
              {{ glassSummary }}
            </span>
          </span>
        </div>
      </UButton>

      <template #content>
        <div class="flex flex-col gap-8 px-2 pt-4 pb-6">
          <!-- Live preview stage -->
          <Teleport to="body" :disabled="!isPhone">
            <section
              v-show="previewVisible && (!isPhone || isOpen)"
              ref="stageRef"
              aria-label="Live preview"
              class="preview-dock z-10 max-md:bg-default rounded-2xl border border-gray-200/70 dark:border-gray-700/70 p-3 sm:p-4 frosted-glass glass-surface"
              :class="{ 'preview-dock-collapsed': !previewDockOpen }"
            >
              <button
                type="button"
                class="md:hidden flex w-full items-center justify-between py-2 cursor-pointer"
                :aria-expanded="previewDockOpen"
                aria-label="Toggle live preview"
                @click="previewDockOpen = !previewDockOpen"
              >
                <span
                  class="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400"
                  >Live preview</span
                >
                <UIcon
                  name="i-lucide-chevron-down"
                  class="size-4.5 text-gray-500 dark:text-gray-400 transition-transform duration-200 shrink-0"
                  :class="{ 'rotate-180': !previewDockOpen }"
                />
              </button>

              <div class="preview-cards">
                <div class="preview-cards-inner">
                  <div class="grid gap-3 sm:gap-4 md:grid-cols-2">
                    <div>
                      <p
                        class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wide mb-2"
                      >
                        Grid
                      </p>
                      <div class="flex items-end justify-center gap-3">
                        <div
                          class="flex flex-col items-center gap-1.5 rounded-lg px-2 py-2 outline outline-1 outline-primary -outline-offset-1 bg-primary/10"
                        >
                          <span
                            v-if="thumbnailsEnabled"
                            class="preview-thumb rounded-md"
                            :style="gridThumbStyle"
                          />
                          <span
                            v-else
                            class="preview-size-anim grid place-items-center rounded-lg bg-gray-100 dark:bg-neutral-800 text-gray-500 dark:text-gray-400"
                            :style="gridIconStyle"
                          >
                            <UIcon name="i-lucide-image" class="w-[55%] h-[55%]" />
                          </span>
                          <span
                            class="max-w-24 truncate text-[11px] text-gray-600 dark:text-gray-400"
                            >sunset-harbor.jpg</span
                          >
                        </div>
                        <div class="flex flex-col items-center gap-1.5 rounded-lg px-2 py-2">
                          <span
                            class="preview-size-anim grid place-items-center rounded-lg bg-gray-100 dark:bg-neutral-800 text-gray-500 dark:text-gray-400"
                            :style="gridIconStyle"
                          >
                            <UIcon name="i-lucide-file-text" class="w-[55%] h-[55%]" />
                          </span>
                          <span
                            class="max-w-24 truncate text-[11px] text-gray-600 dark:text-gray-400"
                            >project-specification.pdf</span
                          >
                        </div>
                      </div>
                    </div>

                    <div>
                      <p
                        class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wide mb-2"
                      >
                        List
                      </p>
                      <div class="flex flex-col gap-1">
                        <div
                          class="preview-row-anim flex items-center gap-2.5 rounded-lg bg-primary/10 px-2"
                          :style="listRowStyle"
                        >
                          <UIcon
                            name="i-lucide-image"
                            class="shrink-0 preview-size-anim text-gray-500 dark:text-gray-400"
                            :style="listIconStyle"
                          />
                          <span
                            class="flex-1 min-w-0 truncate text-xs text-gray-900 dark:text-gray-100"
                            >sunset-harbor.jpg</span
                          >
                          <span class="flex-none text-[11px] text-gray-500 dark:text-gray-500"
                            >2.4 MB</span
                          >
                        </div>
                        <div
                          class="preview-row-anim flex items-center gap-2.5 rounded-lg px-2"
                          :style="listRowStyle"
                        >
                          <UIcon
                            name="i-lucide-file-text"
                            class="shrink-0 preview-size-anim text-gray-500 dark:text-gray-400"
                            :style="listIconStyle"
                          />
                          <span
                            class="flex-1 min-w-0 truncate text-xs text-gray-900 dark:text-gray-100"
                            >project-specification.pdf</span
                          >
                          <span class="flex-none text-[11px] text-gray-500 dark:text-gray-500"
                            >1.05 MB</span
                          >
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </section>
          </Teleport>
          <div class="flex items-center justify-between">
            <h3 class="text-sm font-medium text-gray-500 dark:text-gray-400">Visual Settings</h3>
            <UButton
              label="Reset"
              color="error"
              variant="outline"
              size="xs"
              :disabled="!settingsStore.isAppearanceModified"
              @click="settingsStore.resetAppearanceSettings()"
            />
          </div>

          <div class="flex flex-col gap-8">
            <!-- Group: Theme -->
            <section aria-label="Theme" class="space-y-4">
              <div>
                <h3 class="text-sm font-medium text-gray-900 dark:text-gray-100">Theme</h3>
                <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                  Accent color, background and backdrop image.
                </p>
              </div>

              <!-- Accent color swatches -->
              <div class="flex items-start justify-between gap-4">
                <div class="min-w-0">
                  <p class="text-sm font-medium text-gray-900 dark:text-gray-100">Accent color</p>
                  <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                    Used for the selected state, switches and the main action across the app.
                  </p>
                </div>
                <span
                  class="inline-flex flex-none items-center gap-1.5 text-xs font-medium text-gray-700 dark:text-gray-300"
                >
                  <span
                    class="w-2.5 h-2.5 rounded-full"
                    :style="{ backgroundColor: `rgb(${accentRgb})` }"
                  />
                  {{ selectedColorLabel }}
                </span>
              </div>

              <div class="flex flex-wrap gap-3 pt-1" role="group" aria-label="Accent color">
                <button
                  v-for="color in settingsStore.AVAILABLE_COLORS"
                  :key="color.name"
                  @click="selectedColor = color.name"
                  class="w-10 h-10 rounded-full transition-all relative"
                  :class="
                    selectedColor === color.name
                      ? 'ring-2 ring-primary ring-offset-2 scale-105 shadow-md'
                      : 'hover:scale-105 hover:shadow-sm'
                  "
                  :style="{ backgroundColor: `rgb(${color.value})` }"
                  :title="color.name.charAt(0).toUpperCase() + color.name.slice(1)"
                  :aria-label="color.name.charAt(0).toUpperCase() + color.name.slice(1)"
                  :aria-pressed="selectedColor === color.name"
                >
                  <span
                    v-if="selectedColor === color.name"
                    class="absolute inset-0 flex items-center justify-center"
                    :class="checkOn(color.value)"
                  >
                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="3"
                        d="M5 13l4 4L19 7"
                      />
                    </svg>
                  </span>
                </button>
              </div>

              <!-- Background preset tiles -->
              <UFormField label="Background Color">
                <template #description>
                  Showing
                  <span class="font-medium text-gray-700 dark:text-gray-300"
                    >{{ modeLabel }}-mode</span
                  >
                  backgrounds, switch modes to see the other set
                </template>

                <div class="grid grid-cols-2 sm:grid-cols-5 gap-2 mt-1 p-2">
                  <button
                    v-for="bg in visibleBackgrounds"
                    :key="bg.name"
                    @click="selectedBackground = bg.name"
                    :title="bg.description"
                    :disabled="settingsStore.hasBackgroundImage"
                    :class="[
                      'flex flex-col items-center gap-1.5 rounded-lg border p-2 transition-all text-xs',
                      settingsStore.hasBackgroundImage
                        ? 'opacity-40 cursor-not-allowed'
                        : selectedBackground === bg.name
                          ? 'border-primary ring-1 ring-primary shadow-sm scale-[1.03]'
                          : 'border-gray-200 dark:border-gray-700 hover:border-gray-400 dark:hover:border-gray-500 hover:shadow-sm',
                    ]"
                  >
                    <span
                      class="w-full h-8 rounded border border-gray-200 dark:border-gray-600 relative block"
                      :style="bg.name !== 'system' ? { backgroundColor: swatchFor(bg) } : {}"
                      :class="bg.name === 'system' ? (isDark ? 'bg-zinc-900' : 'bg-white') : ''"
                    >
                      <span
                        v-if="!settingsStore.hasBackgroundImage && selectedBackground === bg.name"
                        class="absolute inset-0 flex items-center justify-center"
                      >
                        <svg
                          class="w-4 h-4 drop-shadow"
                          :class="isDark ? 'text-white' : 'text-gray-700'"
                          fill="none"
                          stroke="currentColor"
                          viewBox="0 0 24 24"
                        >
                          <path
                            stroke-linecap="round"
                            stroke-linejoin="round"
                            stroke-width="3"
                            d="M5 13l4 4L19 7"
                          />
                        </svg>
                      </span>
                      <span
                        v-if="bg.mode === 'both' && bg.name !== 'system'"
                        class="absolute bottom-0.5 right-0.5 text-[9px] leading-none bg-black/20 text-white rounded px-0.5"
                        >☀︎ ☾</span
                      >
                    </span>
                    <span class="font-medium text-gray-700 dark:text-gray-300 leading-tight">{{
                      bg.label
                    }}</span>
                    <span
                      class="text-gray-400 dark:text-gray-500 leading-tight text-center line-clamp-1"
                      >{{ bg.description }}</span
                    >
                  </button>
                </div>
              </UFormField>

              <!-- Background image -->
              <UFormField
                label="Background Image"
                description="Adds a custom image behind the app, blended with the color overlay above. Max 2 MB."
              >
                <input
                  ref="fileInputRef"
                  type="file"
                  accept="image/*"
                  class="hidden"
                  @change="handleFileChange"
                />

                <div class="space-y-3 mt-1">
                  <div class="flex items-center gap-2 flex-wrap">
                    <UButton
                      :label="settingsStore.hasBackgroundImage ? 'Replace image' : 'Upload image'"
                      :icon="
                        settingsStore.hasBackgroundImage ? 'i-lucide-image-up' : 'i-lucide-upload'
                      "
                      :loading="isUploading"
                      :disabled="isUploading"
                      color="neutral"
                      variant="outline"
                      size="sm"
                      @click="triggerFileInput"
                    />
                    <UButton
                      v-if="settingsStore.hasBackgroundImage"
                      label="Remove"
                      icon="i-lucide-x"
                      color="error"
                      variant="ghost"
                      size="sm"
                      @click="clearImage"
                    />

                    <span
                      v-if="imageName"
                      class="inline-flex items-center gap-1.5 text-xs bg-gray-100 dark:bg-neutral-800 rounded-full px-2.5 py-1 truncate max-w-50"
                      :title="imageName"
                    >
                      <Icon icon="mdi:image-outline" class="w-3.5 h-3.5 shrink-0" />
                      {{ imageName }}
                    </span>
                  </div>

                  <p v-if="imageError" class="text-xs text-red-500 flex items-center gap-1">
                    <Icon icon="mdi:alert-circle-outline" class="w-3.5 h-3.5 shrink-0" />
                    {{ imageError }}
                  </p>

                  <Transition
                    enter-active-class="transition-all duration-200 ease-out"
                    enter-from-class="opacity-0 -translate-y-1"
                    enter-to-class="opacity-100 translate-y-0"
                    leave-active-class="transition-all duration-150 ease-in"
                    leave-from-class="opacity-100 translate-y-0"
                    leave-to-class="opacity-0 -translate-y-1"
                  >
                    <div
                      v-if="settingsStore.hasBackgroundImage"
                      class="flex items-center justify-between gap-6 pt-1"
                    >
                      <p class="text-sm text-gray-700 dark:text-gray-300">Image visibility</p>
                      <div
                        class="inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-0.5 shrink-0"
                      >
                        <UButton
                          v-for="opt in imageVisibilityOptions"
                          :key="opt.label"
                          :label="opt.label"
                          size="xs"
                          :color="
                            imageVisibilityWord(imageOpacity) === opt.label ? 'primary' : 'neutral'
                          "
                          :variant="
                            imageVisibilityWord(imageOpacity) === opt.label ? 'solid' : 'ghost'
                          "
                          @click="imageOpacity = opt.value"
                        />
                      </div>
                    </div>
                  </Transition>
                </div>
              </UFormField>

              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pt-2">
                <div class="min-w-0">
                  <p class="text-sm font-medium text-gray-900 dark:text-gray-100">Interface font</p>
                  <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                    Typeface used across the whole app.
                  </p>
                </div>
                <div
                  class="inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-0.5 self-start sm:self-auto shrink-0"
                  role="group"
                  aria-label="Interface font"
                >
                  <UButton
                    v-for="font in settingsStore.AVAILABLE_FONTS"
                    :key="font.name"
                    :label="font.label"
                    size="xs"
                    :color="fontFamily === font.name ? 'primary' : 'neutral'"
                    :variant="fontFamily === font.name ? 'solid' : 'ghost'"
                    @click="fontFamily = font.name"
                  />
                </div>
              </div>

              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pt-2">
                <div class="min-w-0">
                  <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
                    Corner roundness
                  </p>
                  <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                    How rounded buttons, inputs, cards and dialogs are.
                  </p>
                </div>
                <div
                  class="inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-0.5 self-start sm:self-auto shrink-0"
                  role="group"
                  aria-label="Corner roundness"
                >
                  <UButton
                    v-for="opt in radiusOptions"
                    :key="opt.label"
                    :label="opt.label"
                    size="xs"
                    :color="cornerRadius === opt.value ? 'primary' : 'neutral'"
                    :variant="cornerRadius === opt.value ? 'solid' : 'ghost'"
                    @click="cornerRadius = opt.value"
                  />
                </div>
              </div>
            </section>

            <!-- Group: Glass look -->
            <section aria-label="Glass look" class="space-y-4">
              <div>
                <h3 class="text-sm font-medium text-gray-900 dark:text-gray-100">Glass look</h3>
                <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                  Background blur and see-through panels.
                </p>
              </div>

              <div class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
                <div class="py-4 first:pt-0 last:pb-0 space-y-4">
                  <div class="flex items-center justify-between gap-6">
                    <div class="min-w-0">
                      <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
                        Background blur
                      </p>
                      <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                        Soften what shows through cards, bars and popovers.
                      </p>
                    </div>
                    <USwitch v-model="frostEnabled" size="lg" class="shrink-0" />
                  </div>

                  <div
                    v-if="frostEnabled"
                    class="pl-4 border-l-2 border-gray-200 dark:border-gray-700 space-y-4"
                  >
                    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                      <p class="text-sm text-gray-700 dark:text-gray-300">Blur amount</p>
                      <div
                        class="inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-0.5 self-start sm:self-auto shrink-0"
                      >
                        <UButton
                          v-for="opt in frostOptions"
                          :key="opt.label"
                          :label="opt.label"
                          size="xs"
                          :color="frostWord(frostStrength) === opt.label ? 'primary' : 'neutral'"
                          :variant="frostWord(frostStrength) === opt.label ? 'solid' : 'ghost'"
                          @click="frostStrength = opt.value"
                        />
                      </div>
                    </div>

                    <div class="flex items-center justify-between gap-6">
                      <div class="min-w-0">
                        <p class="text-sm text-gray-700 dark:text-gray-300">Calm phones down</p>
                        <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                          Keep small screens solid, blur can lag on older phones.
                        </p>
                      </div>
                      <USwitch v-model="frostDisabledOnMobile" size="lg" class="shrink-0" />
                    </div>
                  </div>
                </div>

                <div class="py-4 first:pt-0 last:pb-0 space-y-4">
                  <div class="flex items-center justify-between gap-6">
                    <div class="min-w-0">
                      <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
                        See-through panels
                      </p>
                      <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                        Let the background show through cards, bars and sheets.
                      </p>
                    </div>
                    <USwitch v-model="transparencyEnabled" size="lg" class="shrink-0" />
                  </div>

                  <div
                    v-if="transparencyEnabled"
                    class="pl-4 border-l-2 border-gray-200 dark:border-gray-700"
                  >
                    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                      <p class="text-sm text-gray-700 dark:text-gray-300">Panel clarity</p>
                      <div
                        class="inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-0.5 self-start sm:self-auto shrink-0"
                      >
                        <UButton
                          v-for="opt in opacityOptions"
                          :key="opt.label"
                          :label="opt.label"
                          size="xs"
                          :color="opacityWord(surfaceOpacity) === opt.label ? 'primary' : 'neutral'"
                          :variant="opacityWord(surfaceOpacity) === opt.label ? 'solid' : 'ghost'"
                          @click="surfaceOpacity = opt.value"
                        />
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </section>

            <!-- Group: File browsing -->
            <section aria-label="File browsing" class="space-y-4">
              <div>
                <h3 class="text-sm font-medium text-gray-900 dark:text-gray-100">File browsing</h3>
                <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                  How files look in the explorer.
                </p>
              </div>

              <div class="divide-y divide-gray-200/70 dark:divide-gray-700/70">
                <div class="flex items-center justify-between gap-6 py-4 first:pt-0 last:pb-0">
                  <div class="min-w-0">
                    <p class="text-sm font-medium text-gray-900 dark:text-gray-100">
                      File thumbnails
                    </p>
                    <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                      Show image previews instead of file-type icons in the explorer grid.
                    </p>
                  </div>
                  <USwitch v-model="thumbnailsEnabled" size="lg" class="shrink-0" />
                </div>

                <div
                  class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 py-4 first:pt-0 last:pb-0"
                >
                  <div class="min-w-0">
                    <p class="text-sm text-gray-700 dark:text-gray-300">Grid tile size</p>
                    <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                      How big icons and thumbnails are in grid view.
                    </p>
                  </div>
                  <div
                    class="inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-0.5 self-start sm:self-auto shrink-0"
                  >
                    <UButton
                      v-for="opt in gridSizeOptions"
                      :key="opt.label"
                      :label="opt.label"
                      size="xs"
                      :color="gridSizeWord(gridIconSize) === opt.label ? 'primary' : 'neutral'"
                      :variant="gridSizeWord(gridIconSize) === opt.label ? 'solid' : 'ghost'"
                      @click="gridIconSize = opt.value"
                    />
                  </div>
                </div>

                <div
                  class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 py-4 first:pt-0 last:pb-0"
                >
                  <div class="min-w-0">
                    <p class="text-sm text-gray-700 dark:text-gray-300">List row size</p>
                    <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                      How tall rows are in list view.
                    </p>
                  </div>
                  <div
                    class="inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-0.5 self-start sm:self-auto shrink-0"
                  >
                    <UButton
                      v-for="opt in listSizeOptions"
                      :key="opt.label"
                      :label="opt.label"
                      size="xs"
                      :color="listSizeWord(listIconSize) === opt.label ? 'primary' : 'neutral'"
                      :variant="listSizeWord(listIconSize) === opt.label ? 'solid' : 'ghost'"
                      @click="listIconSize = opt.value"
                    />
                  </div>
                </div>
              </div>
            </section>
          </div>
        </div>
      </template>
    </UCollapsible>
  </UCard>
</template>

<style scoped>
.preview-thumb {
  display: block;
  transition:
    width 200ms ease-out,
    height 200ms ease-out;
  background:
    radial-gradient(circle at 74% 28%, #fde68a 0 12%, transparent 13%),
    linear-gradient(160deg, #38bdf8, #6366f1 55%, #f472b6);
}

.preview-size-anim {
  transition:
    width 150ms ease-out,
    height 150ms ease-out;
}

.preview-row-anim {
  transition: padding 150ms ease-out;
}

@media (max-width: 767px) {
  .preview-dock {
    position: fixed;
    left: 0;
    right: 0;
    bottom: 0;
    z-index: 30;
    margin: 0;
    border-radius: 16px 16px 0 0;
    max-height: 52vh;
    overflow-y: auto;
    padding-bottom: env(safe-area-inset-bottom);
  }
  .preview-cards {
    display: grid;
    grid-template-rows: 1fr;
    transition: grid-template-rows 150ms ease-out;
  }
  .preview-cards-inner {
    min-height: 0;
    overflow: hidden;
  }
  .preview-dock-collapsed .preview-cards {
    grid-template-rows: 0fr;
    transition: grid-template-rows 150ms ease-in;
  }
}

@media (prefers-reduced-motion: reduce) {
  .preview-cards {
    transition: none;
  }
  .preview-thumb,
  .preview-size-anim,
  .preview-row-anim {
    transition: none;
  }
}
</style>
