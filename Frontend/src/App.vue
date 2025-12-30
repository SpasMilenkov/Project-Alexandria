<script setup lang="ts">
import { useRoute } from "vue-router";
import { useSettingsStore } from "@/stores/settings";
import DefaultLayout from "@/layouts/DefaultLayout.vue";
import DashboardLayout from "@/layouts/DashboardLayout.vue";

const route = useRoute();

// Initialize the settings store immediately
// This will trigger the theme application via the store's watchers
const settingsStore = useSettingsStore();

const layouts = {
  default: DefaultLayout,
  dashboard: DashboardLayout,
};
</script>

<template>
  <UApp>
    <component :is="layouts[route.meta.layout || 'default']">
      <RouterView />
    </component>
    <UFooter />
  </UApp>
</template>

<style scoped></style>
