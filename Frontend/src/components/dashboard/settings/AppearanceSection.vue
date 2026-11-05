<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useDebounceFn, useMediaQuery } from "@vueuse/core";
import { computed, onBeforeUnmount, onMounted, ref, watch, watchEffect } from "vue";

import { useBackgroundImageSync } from "@/composables/useBackgroundImageSync";
import { useColorSchedule } from "@/composables/useColorSchedule";
import { useSettingsSync } from "@/composables/useSettingsSync";
import { useTheme } from "@/composables/useTheme";
import {
  type ColorName,
  type FontName,
  MAX_BACKGROUND_IMAGE_BYTES,
  useSettingsStore,
} from "@/stores/settings";
import { logger } from "@/utils/logger";

import ExplorerPreview from "./ExplorerPreview.vue";
import SettingsChip from "./SettingsChip.vue";
import SettingsSection from "./SettingsSection.vue";

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

const { colorMode, schedule, setEnabled, setTimes } = useColorSchedule();

type ColorModeChoice = "auto" | "light" | "dark" | "scheduled";

const COLOR_MODE_OPTIONS: { label: string; value: ColorModeChoice }[] = [
  { label: "System", value: "auto" },
  { label: "Light", value: "light" },
  { label: "Dark", value: "dark" },
  { label: "Scheduled", value: "scheduled" },
];

const colorModeChoice = computed<ColorModeChoice>({
  get: () => {
    if (schedule.value.enabled) return "scheduled";

    return colorMode.value;
  },
  set: (choice) => {
    if (choice === "scheduled") {
      setEnabled(true);
      return;
    }

    setEnabled(false);
    colorMode.value = choice;
  },
});

const colorModeHint = computed(() => {
  if (colorModeChoice.value === "scheduled") {
    return "Switches by local time on this device. The navbar toggle overrides it until the next switch.";
  }

  if (colorModeChoice.value === "auto") return "Follows your operating system.";

  return "Stays fixed until you change it here or with the navbar toggle.";
});

const lightFrom = computed({
  get: () => schedule.value.lightFrom,
  set: (value: string) => setTimes({ darkFrom: schedule.value.darkFrom, lightFrom: value }),
});

const darkFrom = computed({
  get: () => schedule.value.darkFrom,
  set: (value: string) => setTimes({ darkFrom: value, lightFrom: schedule.value.lightFrom }),
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

const handleResetAppearance = async () => {
  if (settingsStore.hasBackgroundImage) {
    try {
      await deleteBackgroundImage();
    } catch (err) {
      logger.error(err);
    }
  }

  settingsStore.resetAppearanceSettings();
};

const imageName = computed(() => {
  if (!settingsStore.backgroundImageKey) return null;
  return settingsStore.backgroundImageKey.split("/").pop() ?? "background";
});

const backgroundTileClass = (name: string) => {
  if (settingsStore.hasBackgroundImage) return "opacity-40 cursor-not-allowed";

  if (selectedBackground.value === name) {
    return "border-primary ring-1 ring-primary shadow-sm scale-[1.03]";
  }

  return "border-gray-200 dark:border-gray-700 hover:border-gray-400 dark:hover:border-gray-500 hover:shadow-sm";
};
</script>

<template>
  <SettingsSection
    v-model:open="isOpen"
    icon="mdi:palette-outline"
    title="Appearance"
    description="Colors, glass effects and file tiles"
    :modified="settingsStore.isAppearanceModified"
    @reset="handleResetAppearance"
  >
    <template #summary>
      <SettingsChip :swatch="`rgb(${accentRgb})`">{{ selectedColorLabel }}</SettingsChip>
      <SettingsChip v-if="backgroundLabel">{{ backgroundLabel }}</SettingsChip>
      <SettingsChip>{{ glassSummary }}</SettingsChip>
    </template>

    <div class="flex flex-col gap-8">
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
              class="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-gray-400"
              >Live preview</span
            >
            <UIcon
              name="i-lucide-chevron-down"
              class="size-4.5 text-gray-600 dark:text-gray-400 transition-transform duration-200 shrink-0"
              :class="{ 'rotate-180': !previewDockOpen }"
            />
          </button>

          <div class="preview-cards">
            <div class="preview-cards-inner">
              <ExplorerPreview />
            </div>
          </div>
        </section>
      </Teleport>

      <div class="flex flex-col gap-8">
        <!-- Group: Theme -->
        <section aria-label="Theme" class="space-y-4">
          <div>
            <h3 class="text-sm font-medium text-gray-900 dark:text-gray-100">Theme</h3>
            <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
              Accent color, background and backdrop image.
            </p>
          </div>

          <!-- Light and dark mode -->
          <div class="flex flex-col gap-3">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div class="min-w-0">
                <p class="text-sm font-medium text-gray-900 dark:text-gray-100">Light and dark</p>
                <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                  {{ colorModeHint }}
                </p>
              </div>
              <div
                class="inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-0.5 self-start sm:self-auto shrink-0"
                role="group"
                aria-label="Light and dark"
              >
                <UButton
                  v-for="opt in COLOR_MODE_OPTIONS"
                  :key="opt.value"
                  :label="opt.label"
                  size="xs"
                  :color="colorModeChoice === opt.value ? 'primary' : 'neutral'"
                  :variant="colorModeChoice === opt.value ? 'solid' : 'ghost'"
                  :aria-pressed="colorModeChoice === opt.value"
                  @click="colorModeChoice = opt.value"
                />
              </div>
            </div>

            <div
              v-if="colorModeChoice === 'scheduled'"
              class="flex flex-wrap items-end gap-4 border-l-2 border-gray-200/70 pl-4 dark:border-gray-700/70"
            >
              <UFormField label="Light from" name="color-schedule-light">
                <UInput v-model="lightFrom" type="time" size="sm" class="w-32" />
              </UFormField>
              <UFormField label="Dark from" name="color-schedule-dark">
                <UInput v-model="darkFrom" type="time" size="sm" class="w-32" />
              </UFormField>
              <p v-if="lightFrom === darkFrom" class="text-xs text-error">
                Pick two different times.
              </p>
            </div>
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
              <span class="font-medium text-gray-700 dark:text-gray-300">{{ modeLabel }}-mode</span>
              backgrounds, switch modes to see the other set
            </template>

            <div class="grid grid-cols-2 sm:grid-cols-5 gap-2 mt-1 p-2">
              <button
                v-for="bg in visibleBackgrounds"
                :key="bg.name"
                @click="selectedBackground = bg.name"
                :title="bg.description"
                :disabled="settingsStore.hasBackgroundImage"
                class="flex flex-col items-center gap-1.5 rounded-lg border p-2 transition-all text-xs"
                :class="backgroundTileClass(bg.name)"
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
                  :icon="settingsStore.hasBackgroundImage ? 'i-lucide-image-up' : 'i-lucide-upload'"
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
                  class="inline-flex items-center gap-1.5 text-xs text-gray-600 dark:text-gray-400 border border-gray-200/70 dark:border-gray-700/70 rounded-full px-2.5 py-1 truncate max-w-50"
                  :title="imageName"
                >
                  <Icon icon="mdi:image-outline" class="w-3.5 h-3.5 shrink-0" />
                  {{ imageName }}
                </span>
              </div>

              <p v-if="imageError" class="text-xs text-error flex items-center gap-1">
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
                      :variant="imageVisibilityWord(imageOpacity) === opt.label ? 'solid' : 'ghost'"
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
              <p class="text-sm font-medium text-gray-900 dark:text-gray-100">Corner roundness</p>
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
                <p class="text-sm font-medium text-gray-900 dark:text-gray-100">File thumbnails</p>
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
  </SettingsSection>
</template>

<style scoped>
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
}
</style>
