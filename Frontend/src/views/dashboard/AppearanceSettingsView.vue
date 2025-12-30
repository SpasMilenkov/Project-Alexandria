<script setup lang="ts">
import { computed, ref } from "vue";
import { useSettingsStore } from "@/stores/settings";
import { Icon } from "@iconify/vue";
const settingsStore = useSettingsStore();

const exampleFile = {
  fileId: "file-123e4567-e89b-12d3-a456-426614174000",
  fileName: "project-specification.pdf",
  mimeType: "application/pdf",
  createdAt: "2025-01-10T09:15:30.000Z",
  updatedAt: "2025-02-01T14:42:10.000Z",
  deletedAt: null,
  currentVersion: {
    id: "version-1",
    size: "1048576", // 1 MB
    mimeType: "application/pdf",
    versionNumber: 3,
  },
  tags: [
    {
      id: "tag-1",
      name: "documentation",
      userId: "user-1",
      createdAt: "2025-01-10T09:20:00.000Z",
      updatedAt: null,
    },
    {
      id: "tag-2",
      name: "important",
      userId: "user-1",
      createdAt: "2025-01-11T08:00:00.000Z",
      updatedAt: "2025-01-15T10:30:00.000Z",
    },
  ],
  owner: {
    id: "user-1",
    name: "Jane Doe",
    email: "jane.doe@example.com",
  },
};

const exampleDir = {
  id: "dir-987e6543-e21b-65d3-a456-426614174999",
  name: "Project Documents",
  parentId: "dir-root",
  createdAt: "2024-12-01T08:00:00.000Z",
  updatedAt: "2025-01-20T16:45:00.000Z",
  ownerUserDto: {
    id: "user-1",
    name: "Jane Doe",
    email: "jane.doe@example.com",
  },
};

const viewMode = ref<"grid" | "list">("grid");

// Use computed with getter/setter for two-way binding
const selectedColor = computed({
  get: () => settingsStore.accentColor,
  set: (value: string) => settingsStore.setAccentColor(value),
});

const gridIconSize = computed({
  get: () => settingsStore.gridIconSize,
  set: (value: number) => settingsStore.setGridIconSize(value),
});

const listIconSize = computed({
  get: () => settingsStore.listIconSize,
  set: (value: number) => settingsStore.setListIconSize(value),
});

// Color options for the select
const colorOptions = settingsStore.AVAILABLE_COLORS.map((color) => ({
  label: color.name.charAt(0).toUpperCase() + color.name.slice(1),
  value: color.name,
  color: `rgb(${color.value})`,
}));

const handleReset = () => {
  settingsStore.resetSettings();
};
</script>

<template>
  <div class="w-full h-full overflow-y-auto">
    <div class="max-w-7xl mx-auto p-6 space-y-6">
      <!-- Header -->
      <div class="space-y-2">
        <h1 class="text-2xl font-bold">Settings</h1>
        <p class="text-gray-600 dark:text-gray-400">
          Customize your experience. Changes are saved automatically.
        </p>
      </div>

      <!-- Main Grid Layout -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Left Column: Settings -->
        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Appearance</h2>
          </template>

          <div class="space-y-6">
            <!-- Accent Color Selection -->
            <UFormField
              label="Accent Color"
              description="Choose your preferred theme color"
            >
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

            <!-- Color Preview Grid -->
            <div class="grid grid-cols-5 sm:grid-cols-6 gap-3">
              <button
                v-for="color in settingsStore.AVAILABLE_COLORS"
                :key="color.name"
                @click="selectedColor = color.name"
                :class="[
                  'h-12 rounded-lg transition-all relative group',
                  selectedColor === color.name
                    ? 'ring-2 ring-offset-2 ring-gray-900 dark:ring-white scale-105'
                    : 'hover:scale-105',
                ]"
                :style="{ backgroundColor: `rgb(${color.value})` }"
                :title="
                  color.name.charAt(0).toUpperCase() + color.name.slice(1)
                "
              >
                <!-- Checkmark for selected color -->
                <span
                  v-if="selectedColor === color.name"
                  class="absolute inset-0 flex items-center justify-center text-white"
                >
                  <svg
                    class="w-6 h-6"
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
              </button>
            </div>

            <!-- Icon Size -->
            <UFormField
              label="Grid Icon Size"
              description="Adjust the size of the file explorer grid icons"
            >
              <div class="space-y-4">
                <div class="flex items-center gap-4">
                  <UInput
                    type="number"
                    v-model.number="gridIconSize"
                    :min="12"
                    :max="64"
                    class="w-24"
                  />
                  <span class="text-sm text-gray-600 dark:text-gray-400"
                    >{{ gridIconSize }}px</span
                  >
                </div>
              </div>
            </UFormField>

            <UFormField
              label="List Icon Size"
              description="Adjust the size of the file explorer list icons"
            >
              <div class="space-y-4">
                <div class="flex items-center gap-4">
                  <UInput
                    type="number"
                    v-model.number="listIconSize"
                    :min="12"
                    :max="64"
                    class="w-24"
                  />
                  <span class="text-sm text-gray-600 dark:text-gray-400"
                    >{{ listIconSize }}px</span
                  >
                </div>
              </div>
            </UFormField>
          </div>

          <template #footer>
            <div
              class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3"
            >
              <p class="text-sm text-gray-500">
                Settings are saved automatically
              </p>
              <UButton
                label="Reset to defaults"
                color="gray"
                variant="ghost"
                @click="handleReset"
              />
            </div>
          </template>
        </UCard>

        <!-- Right Column: Preview -->
        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Preview</h2>
          </template>

          <div class="space-y-4">
            <p class="text-sm text-gray-600 dark:text-gray-400">
              See how your theme looks with different components
            </p>
            <h3>Colors</h3>
            <div class="flex flex-wrap gap-2">
              <UButton color="primary" label="Primary Button" />
              <UButton color="primary" variant="outline" label="Outline" />
              <UButton color="primary" variant="soft" label="Soft" />
              <UButton color="primary" variant="ghost" label="Ghost" />
            </div>

            <div class="flex flex-wrap gap-2">
              <UBadge color="primary" label="Badge" />
              <UBadge color="primary" variant="subtle" label="Subtle Badge" />
            </div>

            <UAlert
              color="primary"
              title="Preview Alert"
              description="This is how alerts will look with your selected color"
            />
            <div class="space-y-4">
              <h3>Icon size</h3>

              <div class="flex items-center gap-1 ml-auto md:ml-2">
                <UButton
                  :variant="viewMode === 'grid' ? 'solid' : 'ghost'"
                  size="sm"
                  @click="viewMode = 'grid'"
                  aria-label="Grid view"
                >
                  <Icon icon="mdi:view-grid" class="w-4 h-4" />
                </UButton>
                <UButton
                  :variant="viewMode === 'list' ? 'solid' : 'ghost'"
                  size="sm"
                  @click="viewMode = 'list'"
                  aria-label="List view"
                >
                  <Icon icon="mdi:view-list" class="w-4 h-4" />
                </UButton>
              </div>

              <div
                :class="[
                  'flex gap-2',
                  viewMode === 'list' ? 'flex-col' : 'flex-row',
                ]"
              >
                <div
                  :class="['max-h-40', viewMode === 'list' ? '' : 'max-w-40']"
                >
                  <FileItem
                    :data="exampleFile"
                    :is-selected="false"
                    :view-mode="viewMode"
                  />
                </div>

                <div
                  :class="['max-h-40', viewMode === 'list' ? '' : 'max-w-40']"
                >
                  <DirectoryItem
                    :data="exampleDir"
                    :is-selected="true"
                    :view-mode="viewMode"
                  />
                </div>
              </div>
            </div>
          </div>
        </UCard>
      </div>
    </div>
  </div>
</template>

<style scoped>
/* Custom styling for range slider to use primary color */
input[type="range"]::-webkit-slider-thumb {
  appearance: none;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: rgb(var(--color-primary-500));
  cursor: pointer;
}

input[type="range"]::-moz-range-thumb {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: rgb(var(--color-primary-500));
  cursor: pointer;
  border: none;
}
</style>
