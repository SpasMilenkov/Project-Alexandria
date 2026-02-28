<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { MAX_BACKGROUND_IMAGE_BYTES, useSettingsStore } from "@/stores/settings";
import { useTheme } from "@/composables/useTheme";
import { useBackgroundImageSync } from "@/composables/useBackgroundImageSync";
import { useSettingsSync } from "@/composables/useSettingsSync";
import { Icon } from "@iconify/vue";
import { useDebounceFn } from "@vueuse/core";
import { logger } from "@/utils/logger";

const settingsStore = useSettingsStore();
const { isDark } = useTheme();
const { uploadBackgroundImage, deleteBackgroundImage } = useBackgroundImageSync();
const { saveAppearance } = useSettingsSync();

const viewMode = ref<"grid" | "list">("grid");
const imageError = ref<string | null>(null);
const isUploading = ref(false);
const fileInputRef = ref<HTMLInputElement | null>(null);

//  Computed store bindings
const selectedColor = computed({
  get: () => settingsStore.accentColor,
  set: (v: string) => settingsStore.setAccentColor(v),
});
const selectedBackground = computed({
  get: () => settingsStore.backgroundColor,
  set: (v: string) => settingsStore.setBackgroundColor(v),
});
const imageOpacity = computed({
  get: () => settingsStore.backgroundImageOpacity,
  set: (v: number) => settingsStore.setBackgroundImageOpacity(v),
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

const colorOptions = settingsStore.AVAILABLE_COLORS.map((color) => ({
  label: color.name.charAt(0).toUpperCase() + color.name.slice(1),
  value: color.name,
}));

const persistAppearance = useDebounceFn(async () => {
  await saveAppearance({
    accentColor: settingsStore.accentColor,
    backgroundColor: settingsStore.backgroundColor,
    backgroundImageKey: settingsStore.backgroundImageKey,
    backgroundImageOpacity: settingsStore.backgroundImageOpacity,
    backgroundImageUpdatedAt: settingsStore.backgroundImageUpdatedAt,
    gridIconSize: settingsStore.gridIconSize,
    listIconSize: settingsStore.listIconSize,
  });
}, 600);

// Watch every field that should trigger a save
watch(
  () => [
    settingsStore.accentColor,
    settingsStore.backgroundColor,
    settingsStore.backgroundImageOpacity,
    settingsStore.gridIconSize,
    settingsStore.listIconSize,
  ],
  persistAppearance,
);

const visibleBackgrounds = computed(() =>
  settingsStore.AVAILABLE_BACKGROUNDS.filter((bg) => {
    if (bg.mode === "both") {
      return true;
    }
    return isDark.value ? bg.mode === "dark" : bg.mode === "light";
  }),
);

watch(isDark, (dark) => {
  const current = settingsStore.AVAILABLE_BACKGROUNDS.find(
    (b) => b.name === settingsStore.backgroundColor,
  );
  if (!current || current.mode === "both") {
    return;
  }
  if (dark && current.mode === "light") {
    settingsStore.setBackgroundColor("system");
  }
  if (!dark && current.mode === "dark") {
    settingsStore.setBackgroundColor("system");
  }
});

const swatchFor = (bg: (typeof settingsStore.AVAILABLE_BACKGROUNDS)[number]) =>
  isDark.value ? bg.darkSwatch : bg.lightSwatch;

const modeLabel = computed(() => (isDark.value ? "dark" : "light"));

// Image upload — S3 flow
const triggerFileInput = () => fileInputRef.value?.click();

const handleFileChange = async (event: Event) => {
  imageError.value = null;
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) {
    return;
  }

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
    // Request presigned URL → upload to S3 → confirm → seed SW cache
    await uploadBackgroundImage(file);
  } catch (err) {
    imageError.value = "Upload failed. Please try again.";
    logger.error(err);
  } finally {
    isUploading.value = false;
    if (fileInputRef.value) {
      fileInputRef.value.value = "";
    }
  }
};

const clearImage = async () => {
  imageError.value = null;
  await deleteBackgroundImage();
  // Persist the cleared key to server
  await persistAppearance();
};

// Derive a display name from the S3 key e.g. "background_images/user-id" → "user-id"
const imageName = computed(() => {
  if (!settingsStore.backgroundImageKey) {
    return null;
  }
  return settingsStore.backgroundImageKey.split("/").pop() ?? "background";
});

const exampleFile = {
  createdAt: "2025-01-10T09:15:30.000Z",
  currentVersion: {
    id: "version-1",
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
      createdAt: "2025-01-10T09:20:00.000Z",
      id: "tag-1",
      name: "documentation",
      updatedAt: null,
      userId: "user-1",
    },
    {
      createdAt: "2025-01-11T08:00:00.000Z",
      id: "tag-2",
      name: "important",
      updatedAt: "2025-01-15T10:30:00.000Z",
      userId: "user-1",
    },
  ],
  updatedAt: "2025-02-01T14:42:10.000Z",
};

const exampleDir = {
  createdAt: "2024-12-01T08:00:00.000Z",
  id: "dir-987e6543-e21b-65d3-a456-426614174999",
  name: "Project Documents",
  ownerUserDto: {
    email: "jane.doe@example.com",
    id: "user-1",
    name: "Jane Doe",
  },
  parentId: "dir-root",
  updatedAt: "2025-01-20T16:45:00.000Z",
};
</script>

<template>
  <UCard class="overflow-hidden" :ui="{ body: 'p-2 sm:p-2' }">
    <UCollapsible v-model:open="isOpen">
      <UButton
        variant="ghost"
        color="neutral"
        block
        class="justify-between"
        :trailing-icon="isOpen ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'"
      >
        <div class="flex items-center gap-2">
          <Icon icon="mdi:palette" class="w-5 h-5 text-primary" />
          <h2 class="text-lg font-semibold">Appearance</h2>
        </div>
      </UButton>

      <template #content>
        <div class="pt-4 px-2 space-y-6">
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Left: Controls -->
            <div class="space-y-6">
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

              <!-- Accent color -->
              <UFormField label="Accent Color" description="Choose your preferred theme color">
                <USelectMenu
                  v-model="selectedColor"
                  :items="colorOptions"
                  value-key="value"
                  value-attribute="value"
                  placeholder="Select a color"
                >
                  <template #leading="{ modelValue }">
                    <UChip v-if="modelValue" inset standalone color="primary" />
                  </template>
                </USelectMenu>
              </UFormField>

              <!-- Color swatches -->
              <div class="grid grid-cols-5 sm:grid-cols-6 gap-3">
                <button
                  v-for="color in settingsStore.AVAILABLE_COLORS"
                  :key="color.name"
                  @click="selectedColor = color.name"
                  :class="[
                    'h-10 rounded-md transition-all relative',
                    selectedColor === color.name
                      ? 'ring-2 ring-offset-2 ring-gray-900 dark:ring-white scale-105 shadow-md'
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
                  backgrounds — switch modes to see the other set
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
                <!-- Hidden native input -->
                <input
                  ref="fileInputRef"
                  type="file"
                  accept="image/*"
                  class="hidden"
                  @change="handleFileChange"
                />

                <div class="space-y-3 mt-1">
                  <!-- Upload / replace / clear row -->
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

                    <!-- Current image filename chip -->
                    <span
                      v-if="imageName"
                      class="inline-flex items-center gap-1.5 text-xs bg-gray-100 dark:bg-neutral-800 rounded-full px-2.5 py-1 truncate max-w-50"
                      :title="imageName"
                    >
                      <Icon icon="mdi:image" class="w-3.5 h-3.5 shrink-0" />
                      {{ imageName }}
                    </span>
                  </div>

                  <!-- Error -->
                  <p v-if="imageError" class="text-xs text-red-500 flex items-center gap-1">
                    <Icon icon="mdi:alert-circle" class="w-3.5 h-3.5 shrink-0" />
                    {{ imageError }}
                  </p>

                  <!-- Opacity slider — only shown when an image is loaded -->
                  <Transition
                    enter-active-class="transition-all duration-200 ease-out"
                    enter-from-class="opacity-0 -translate-y-1"
                    enter-to-class="opacity-100 translate-y-0"
                    leave-active-class="transition-all duration-150 ease-in"
                    leave-from-class="opacity-100 translate-y-0"
                    leave-to-class="opacity-0 -translate-y-1"
                  >
                    <div v-if="settingsStore.hasBackgroundImage" class="space-y-1.5 pt-1">
                      <div class="flex items-center justify-between">
                        <label class="text-xs font-medium text-gray-600 dark:text-gray-400">
                          Image visibility
                        </label>
                        <span class="text-xs tabular-nums text-gray-500 dark:text-gray-400">
                          {{ Math.round(imageOpacity * 100) }}%
                        </span>
                      </div>

                      <!-- Slider rendered as a native range for reactivity; styled with Tailwind -->
                      <div class="relative flex items-center gap-2">
                        <Icon icon="mdi:image-outline" class="w-3.5 h-3.5 text-gray-400 shrink-0" />
                        <input
                          type="range"
                          :min="0.1"
                          :max="0.65"
                          :step="0.05"
                          :value="imageOpacity"
                          @input="
                            imageOpacity = parseFloat(($event.target as HTMLInputElement).value)
                          "
                          class="w-full accent-primary h-1.5 rounded-full cursor-pointer"
                        />
                        <Icon icon="mdi:image" class="w-4 h-4 text-gray-500 shrink-0" />
                      </div>

                      <p class="text-[11px] text-gray-400 dark:text-gray-500 leading-snug">
                        Capped at 65% to keep text readable. The color overlay above blends with the
                        image.
                      </p>
                    </div>
                  </Transition>
                </div>
              </UFormField>

              <!-- Icon sizes -->
              <UFormField
                label="Grid Icon Size"
                description="Adjust the size of the file explorer grid icons"
              >
                <div class="flex items-center gap-4">
                  <UInput
                    type="number"
                    v-model.number="gridIconSize"
                    :min="12"
                    :max="64"
                    class="w-24"
                  />
                  <span class="text-sm text-gray-600 dark:text-gray-400">{{ gridIconSize }}px</span>
                </div>
              </UFormField>

              <UFormField
                label="List Icon Size"
                description="Adjust the size of the file explorer list icons"
              >
                <div class="flex items-center gap-4">
                  <UInput
                    type="number"
                    v-model.number="listIconSize"
                    :min="12"
                    :max="64"
                    class="w-24"
                  />
                  <span class="text-sm text-gray-600 dark:text-gray-400">{{ listIconSize }}px</span>
                </div>
              </UFormField>
            </div>

            <!-- Right: Preview -->
            <div class="space-y-4">
              <h3 class="text-sm font-medium text-gray-500 dark:text-gray-400">Preview</h3>
              <!-- Icon size preview -->
              <div class="space-y-2">
                <div class="flex items-center justify-between">
                  <h4
                    class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wide"
                  >
                    Icon size
                  </h4>
                  <div class="flex gap-1">
                    <UButton
                      :variant="viewMode === 'grid' ? 'solid' : 'ghost'"
                      size="xs"
                      @click="viewMode = 'grid'"
                      aria-label="Grid view"
                    >
                      <Icon icon="mdi:view-grid" class="w-3.5 h-3.5" />
                    </UButton>
                    <UButton
                      :variant="viewMode === 'list' ? 'solid' : 'ghost'"
                      size="xs"
                      @click="viewMode = 'list'"
                      aria-label="List view"
                    >
                      <Icon icon="mdi:view-list" class="w-3.5 h-3.5" />
                    </UButton>
                  </div>
                </div>
                <div :class="['flex gap-2', viewMode === 'list' ? 'flex-col' : 'flex-row']">
                  <div :class="[viewMode === 'list' ? '' : 'max-w-40', 'max-h-40']">
                    <FileItem :data="exampleFile" :is-selected="false" :view-mode="viewMode" />
                  </div>
                  <div :class="[viewMode === 'list' ? 'min-h-12' : 'max-w-40', 'max-h-40']">
                    <DirectoryItem :data="exampleDir" :is-selected="true" :view-mode="viewMode" />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </template>
    </UCollapsible>
  </UCard>
</template>
