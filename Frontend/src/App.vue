<script setup lang="ts">
import { useRoute } from "vue-router";
import DefaultLayout from "@/layouts/DefaultLayout.vue";
import DashboardLayout from "@/layouts/DashboardLayout.vue";
import OnboardingLayout from "@/layouts/OnboardingLayout.vue";
import { PiniaColadaDevtools } from "@pinia/colada-devtools";
import { useTheme } from "./composables/useTheme";

const route = useRoute();
useTheme();

const layouts = {
  dashboard: DashboardLayout,
  default: DefaultLayout,
  onboarding: OnboardingLayout,
};
</script>

<template>
  <UApp>
    <component :is="layouts[route.meta.layout || 'default']">
      <RouterView />
    </component>
    <UFooter />
  </UApp>
  <PiniaColadaDevtools />
</template>

<style scoped></style>
