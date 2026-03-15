<script setup lang="ts">
import { useSettingsStore } from "@/stores/settings";
import AppearanceSection from "@/components/dashboard/settings/AppearanceSection.vue";
import BehaviorSection from "@/components/dashboard/settings/BehaviorSection.vue";

const settingsStore = useSettingsStore();
const handleResetAll = () => {
  settingsStore.resetSettings();
};
</script>

<template>
  <div class="w-full h-full overflow-y-auto">
    <div class="max-w-7xl mx-auto p-6 space-y-6">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div class="space-y-2">
          <h1 class="text-2xl font-bold">Settings</h1>
          <p class="text-gray-600 dark:text-gray-400">
            Customize your experience. Changes are saved automatically.
          </p>
        </div>
        <UButton
          label="Reset all settings"
          color="error"
          variant="outline"
          icon="i-heroicons-arrow-path"
          @click="handleResetAll"
        />
      </div>

      <!-- Settings Sections -->
      <div class="space-y-6">
        <AppearanceSection />
        <BehaviorSection />
      </div>
    </div>
  </div>
</template>
