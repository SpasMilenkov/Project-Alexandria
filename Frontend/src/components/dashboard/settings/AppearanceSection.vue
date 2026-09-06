<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useDebounceFn } from "@vueuse/core";
import { computed, ref, watch } from "vue";

import type { FileResult } from "@/api/file";
import type { FileDto } from "@/api/tag";

import { useBackgroundImageSync } from "@/composables/useBackgroundImageSync";
import { useSettingsSync } from "@/composables/useSettingsSync";
import { useTheme } from "@/composables/useTheme";
import { type ColorName, MAX_BACKGROUND_IMAGE_BYTES, useSettingsStore } from "@/stores/settings";
import { logger } from "@/utils/logger";

const settingsStore = useSettingsStore();
const { isDark } = useTheme();
const { uploadBackgroundImage, deleteBackgroundImage } = useBackgroundImageSync();
const { saveAppearance } = useSettingsSync();

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

const selectedColorLabel = computed(() => {
  const found = settingsStore.AVAILABLE_COLORS.find((c) => c.name === selectedColor.value);
  return found ? found.name.charAt(0).toUpperCase() + found.name.slice(1) : undefined;
});

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

const exampleFile: FileResult = {
  createdAt: "2025-01-10T09:15:30.000Z",
  currentVersion: {
    id: "version-1",
    isDeleted: false,
    isEncrypted: false,
    mimeType: "application/pdf",
    size: "1048576",
    versionNumber: 3,
  },
  deletedAt: null,
  directoryId: null,
  fileId: "file-123e4567-e89b-12d3-a456-426614174000",
  fileName: "project-specification.pdf",
  mimeType: "application/pdf",
  owner: { email: "jane.doe@example.com", id: "user-1", name: "Jane Doe" },
  tags: [
    {
      color: "blue",
      createdAt: "2025-01-10T09:20:00.000Z",
      icon: "mdi-tag",
      id: "tag-1",
      name: "documentation",
      updatedAt: null,
      userId: "user-1",
    },
    {
      color: "blue",
      createdAt: "2025-01-11T08:00:00.000Z",
      icon: "mdi-tag",
      id: "tag-2",
      name: "important",
      updatedAt: "2025-01-15T10:30:00.000Z",
      userId: "user-1",
    },
  ],
  updatedAt: "2025-02-01T14:42:10.000Z",
};
</script>

<template>
  <UCard class="overflow-hidden frosted-glass glass-surface" :ui="{ body: 'p-2 sm:p-2' }">
    <UCollapsible v-model:open="isOpen">
      <UButton
        variant="ghost"
        color="neutral"
        block
        class="justify-between"
        :trailing-icon="isOpen ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'"
      >
        <div class="flex items-center gap-2">
          <Icon icon="mdi:palette-outline" class="w-5 h-5 text-muted" />
          <h2 class="text-lg font-semibold">Appearance</h2>
        </div>
      </UButton>

      <template #content>
        <div class="pt-4 px-2 pb-6">
          <div class="space-y-8">
            <div class="flex items-center justify-between">
                <h3 class="text-sm font-medium text-gray-500 dark:text-gray-400">
                  Visual Settings
                </h3>
                <UButton
                  label="Reset"
                  color="error"
                  variant="outline"
                  size="xs"
                  @click="settingsStore.resetAppearanceSettings()"
                />
              </div>

              <div class="grid grid-cols-1 lg:grid-cols-2 gap-8 items-stretch">

              <!-- Group: Colors -->
              <section aria-label="Colors" class="space-y-6 order-1">
                <div>
                  <h3 class="text-sm font-medium text-gray-900 dark:text-gray-100">Colors</h3>
                  <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                    Accent color, background and backdrop image.
                  </p>
                </div>

                <!-- Accent color swatches -->
                <div class="sm:hidden">
                  <div class="flex p-2 overflow-x-auto gap-3 pb-2 -mx-1 px-1">
                    <button
                      v-for="color in settingsStore.AVAILABLE_COLORS"
                      :key="color.name"
                      @click="selectedColor = color.name"
                      class="flex-none w-12 h-12 rounded-full transition-all relative"
                      :class="
                        selectedColor === color.name
                          ? 'ring-2 ring-primary ring-offset-2 scale-105 shadow-md'
                          : 'hover:scale-105 hover:shadow-sm'
                      "
                      :style="{ backgroundColor: `rgb(${color.value})` }"
                      :title="color.name.charAt(0).toUpperCase() + color.name.slice(1)"
                    >
                      <span
                        v-if="selectedColor === color.name"
                        class="absolute inset-0 flex items-center justify-center text-white"
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
                  <!-- Selected color name, fades in/out on change -->
                  <Transition
                    enter-active-class="transition-all duration-200 ease-out"
                    enter-from-class="opacity-0 translate-y-1"
                    enter-to-class="opacity-100 translate-y-0"
                    leave-active-class="transition-all duration-150 ease-in"
                    leave-from-class="opacity-100 translate-y-0"
                    leave-to-class="opacity-0 translate-y-1"
                  >
                    <p
                      v-if="selectedColorLabel"
                      class="text-xs text-center text-gray-500 dark:text-gray-400 mt-1"
                    >
                      {{ selectedColorLabel }}
                    </p>
                  </Transition>
                </div>

                <div class="hidden sm:grid grid-cols-5 sm:grid-cols-6 gap-3">
                  <button
                    v-for="color in settingsStore.AVAILABLE_COLORS"
                    :key="color.name"
                    @click="selectedColor = color.name"
                    class="h-10 rounded-md transition-all relative"
                    :class="[
                      selectedColor === color.name
                        ? 'ring-2 ring-primary ring-offset-2 scale-105 shadow-md'
                        : 'hover:scale-105 hover:shadow-sm',
                    ]"
                    :style="{ backgroundColor: `rgb(${color.value})` }"
                    :title="color.name.charAt(0).toUpperCase() + color.name.slice(1)"
                  >
                    <span
                      v-if="selectedColor === color.name"
                      class="absolute inset-0 flex items-center justify-center text-white"
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

                  <div class="grid grid-cols-3 sm:grid-cols-4 gap-2 mt-1">
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
                              imageVisibilityWord(imageOpacity) === opt.label
                                ? 'primary'
                                : 'neutral'
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
              </section>

              <!-- Group: Glass look -->
              <section aria-label="Glass look" class="space-y-6 order-3 flex flex-col">
                <div>
                  <h3 class="text-sm font-medium text-gray-900 dark:text-gray-100">Glass look</h3>
                  <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                    Background blur and see-through panels.
                  </p>
                </div>

                <div class="grid grid-cols-1 @lg:grid-cols-2 auto-rows-fr gap-x-6 gap-y-8 flex-1">
                  <!-- Frosted glass -->
                  <div
                    class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 p-4 space-y-4"
                  >
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
                      <div class="flex items-center justify-between gap-6">
                        <p class="text-sm text-gray-700 dark:text-gray-300">Blur amount</p>
                        <div
                          class="inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-0.5 shrink-0"
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

                  <!-- Surface transparency -->
                  <div
                    class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 p-4 space-y-4"
                  >
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
                      <div class="flex items-center justify-between gap-6">
                        <p class="text-sm text-gray-700 dark:text-gray-300">Panel clarity</p>
                        <div
                          class="inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-0.5 shrink-0"
                        >
                          <UButton
                            v-for="opt in opacityOptions"
                            :key="opt.label"
                            :label="opt.label"
                            size="xs"
                            :color="
                              opacityWord(surfaceOpacity) === opt.label ? 'primary' : 'neutral'
                            "
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
              <section aria-label="File browsing" class="space-y-6 order-4 flex flex-col">
                <div>
                  <h3 class="text-sm font-medium text-gray-900 dark:text-gray-100">
                    File browsing
                  </h3>
                  <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                    How files look in the explorer.
                  </p>
                </div>

                <div class="grid grid-cols-1 @lg:grid-cols-2 auto-rows-fr gap-x-6 gap-y-8 flex-1">
                  <div
                    class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 p-4 space-y-4"
                  >
                    <div class="flex items-center justify-between gap-6">
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
                  </div>

                  <div
                    class="rounded-xl h-s border border-gray-200/70 dark:border-gray-700/70 p-4 space-y-4"
                  >
                    <p class="text-sm font-medium text-gray-900 dark:text-gray-100">Icon sizes</p>

                    <div class="flex items-center justify-between gap-6">
                      <p class="text-sm text-gray-700 dark:text-gray-300">Grid tile size</p>
                      <div
                        class="inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-0.5 shrink-0"
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

                    <div class="flex items-center justify-between gap-6">
                      <p class="text-sm text-gray-700 dark:text-gray-300">List row size</p>
                      <div
                        class="inline-flex rounded-lg border border-gray-200 dark:border-gray-700 p-0.5 shrink-0"
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
                </div>
              </section>

            <!-- Live preview -->
            <section aria-label="Live preview" class="space-y-4 order-2">
              <div>
                <h3 class="text-sm font-medium text-gray-900 dark:text-gray-100">Live preview</h3>
                <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                  Watch your settings apply the moment you change them.
                </p>
              </div>

              <div class="flex flex-col gap-4">
                <div
                  class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 p-3 frosted-glass glass-surface"
                >
                  <p
                    class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wide mb-2"
                  >
                    Grid
                  </p>
                  <div class="flex justify-center">
                    <div class="max-w-40 min-w-36 max-h-40">
                      <!-- Doesn't really need to be real. It is a stub anyway. -->
                      <!-- @vue-expect-error -->
                      <FileItem :data="exampleFile" :is-selected="false" view-mode="grid" />
                    </div>
                  </div>
                </div>

                <div
                  class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 p-3 frosted-glass glass-surface"
                >
                  <p
                    class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wide mb-2"
                  >
                    List
                  </p>
                  <div class="min-h-12">
                    <!-- Doesn't really need to be real. It is a stub anyway. -->
                    <!-- @vue-expect-error -->
                    <FileItem :data="exampleFile" :is-selected="false" view-mode="list" />
                  </div>
                </div>
              </div>
            </section>
            </div>
          </div>
        </div>
      </template>
    </UCollapsible>
  </UCard>
</template>
