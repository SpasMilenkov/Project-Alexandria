<script setup lang="ts">
import { nextTick, watch } from "vue";
import { useRoute } from "vue-router";

import AppearanceSection from "@/components/dashboard/settings/AppearanceSection.vue";
import AutoTaggingSection from "@/components/dashboard/settings/AutoTaggingSection.vue";
import BehaviorSection from "@/components/dashboard/settings/BehaviorSection.vue";
import { useSettingsStore } from "@/stores/settings";

const settingsStore = useSettingsStore();
const route = useRoute();
const handleResetAll = () => {
  settingsStore.resetSettings();
};

const autoTaggingEnabled = import.meta.env.VITE_AUTOTAGGING_ENABLED === "true";

// Sidebar sub-navigation lands here via /settings#section links.
watch(
  () => route.hash,
  (hash) => {
    if (!hash) {
      return;
    }
    nextTick(() => {
      document.getElementById(hash.slice(1))?.scrollIntoView({ behavior: "smooth" });
    });
  },
  { immediate: true },
);
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
        <section id="appearance" class="scroll-mt-4">
          <AppearanceSection />
        </section>
        <section id="behavior" class="scroll-mt-4">
          <BehaviorSection />
        </section>
        <section v-if="autoTaggingEnabled" id="auto-tagging" class="scroll-mt-4">
          <AutoTaggingSection />
        </section>
      </div>
    </div>
  </div>
</template>
