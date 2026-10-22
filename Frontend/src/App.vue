<script setup lang="ts">
import { PiniaColadaDevtools } from "@pinia/colada-devtools";
import { computed } from "vue";
import { useRoute } from "vue-router";

import DashboardLayout from "@/layouts/DashboardLayout.vue";
import DefaultLayout from "@/layouts/DefaultLayout.vue";
import OnboardingLayout from "@/layouts/OnboardingLayout.vue";
import { useAuthStore } from "@/stores/auth";

import { useTheme } from "./composables/useTheme";

const route = useRoute();
useTheme();

// The public status page uses the default shell for anonymous visitors but the
// dashboard shell only after the cookie-backed identity has been verified.
const authStore = useAuthStore();

if (authStore.isAuthenticated && !authStore.isSessionVerified) {
  void authStore.ensureSessionVerified();
}

type LayoutName = "dashboard" | "default" | "onboarding";

const layouts = {
  dashboard: DashboardLayout,
  default: DefaultLayout,
  onboarding: OnboardingLayout,
};

const layoutName = computed<LayoutName>(() => {
  if (route.name === "public-status" && authStore.isSessionVerified) return "dashboard";
  return (route.meta.layout as LayoutName) || "default";
});
</script>

<template>
  <UApp>
    <component :is="layouts[layoutName]">
      <RouterView />
    </component>
    <UFooter />
  </UApp>
  <!-- <PiniaColadaDevtools /> -->
</template>

<style scoped></style>
