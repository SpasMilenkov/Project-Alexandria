<script setup lang="ts">
import { computed, ref } from "vue";
import { useSettingsStore } from "@/stores/settings";
import { Icon } from "@iconify/vue";

const settingsStore = useSettingsStore();

const viewMode = ref<"grid" | "list">("grid");

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

const isOpen = computed({
  get: () => settingsStore.isAppearanceSectionOpen,
  set: (value: boolean) => settingsStore.setAppearanceSectionOpen(value),
});

const colorOptions = settingsStore.AVAILABLE_COLORS.map((color) => ({
  label: color.name.charAt(0).toUpperCase() + color.name.slice(1),
  value: color.name,
  color: `rgb(${color.value})`,
}));

const handleResetAppearance = () => {
  settingsStore.resetAppearanceSettings();
};

const exampleFile = {
  fileId: "file-123e4567-e89b-12d3-a456-426614174000",
  fileName: "project-specification.pdf",
  mimeType: "application/pdf",
  directoryId: null,
  createdAt: "2025-01-10T09:15:30.000Z",
  updatedAt: "2025-02-01T14:42:10.000Z",
  deletedAt: null,
  currentVersion: {
    id: "version-1",
    size: "1048576",
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
</script>

<template>
  <UCard class="overflow-hidden" :ui="{body: 'p-2 sm:p-2'}">
    <UCollapsible v-model:open="isOpen" >
      <UButton
        variant="ghost"
        color="neutral"
        block
        class="justify-between"
        :trailing-icon="
          isOpen ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'
        "
      >
        <div class="flex items-center gap-2">
          <Icon icon="mdi:palette" class="w-5 h-5 text-primary" />
          <h2 class="text-lg font-semibold">Appearance</h2>
        </div>
      </UButton>

      <template #content>
        <div class="pt-4  px-2 space-y-6">
          <!-- Responsive Grid: Visual Settings + Preview -->
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Left Column: Visual Settings -->
            <div class="space-y-6">
              <div class="flex items-center justify-between">
                <h3
                  class="text-sm font-medium text-gray-500 dark:text-gray-400"
                >
                  Visual Settings
                </h3>
                <UButton
                  label="Reset"
                  color="gray"
                  variant="ghost"
                  size="xs"
                  @click="handleResetAppearance"
                />
              </div>

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

            <!-- Right Column: Preview -->
            <div class="space-y-6">
              <h3 class="text-sm font-medium text-gray-500 dark:text-gray-400">
                Preview
              </h3>

              <div class="space-y-4">
                <p class="text-sm text-gray-600 dark:text-gray-400">
                  See how your theme looks with different components
                </p>

                <h4 class="text-sm font-semibold">Colors</h4>
                <div class="flex flex-wrap gap-2">
                  <UButton color="primary" label="Primary Button" />
                  <UButton color="primary" variant="outline" label="Outline" />
                  <UButton color="primary" variant="soft" label="Soft" />
                  <UButton color="primary" variant="ghost" label="Ghost" />
                </div>

                <div class="flex flex-wrap gap-2">
                  <UBadge color="primary" label="Badge" />
                  <UBadge
                    color="primary"
                    variant="subtle"
                    label="Subtle Badge"
                  />
                </div>

                <UAlert
                  color="primary"
                  title="Preview Alert"
                  description="This is how alerts will look with your selected color"
                />

                <div class="space-y-4">
                  <h4 class="text-sm font-semibold">Icon size</h4>

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
                      :class="[
                        'max-h-40',
                        viewMode === 'list' ? '' : 'max-w-40',
                      ]"
                    >
                      <FileItem
                        :data="exampleFile"
                        :is-selected="false"
                        :view-mode="viewMode"
                      />
                    </div>

                    <div
                      :class="[
                        'max-h-40',
                        viewMode === 'list' ? '' : 'max-w-40',
                      ]"
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
            </div>
          </div>
        </div>
      </template>
    </UCollapsible>
  </UCard>
</template>
