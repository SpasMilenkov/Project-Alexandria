<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";

import RouteErrorBoundary from "@/components/common/RouteErrorBoundary.vue";
import { useColorScheduleRunner } from "@/composables/useColorSchedule";
import { useSiteIcon } from "@/composables/useSiteIcon";
import { useTheme } from "@/composables/useTheme";
import DashboardLayout from "@/layouts/DashboardLayout.vue";
import DefaultLayout from "@/layouts/DefaultLayout.vue";
import OnboardingLayout from "@/layouts/OnboardingLayout.vue";
import { useAuthStore } from "@/stores/auth";

const route = useRoute();
useTheme();
useColorScheduleRunner();
useSiteIcon();

// These pages use the default shell for anonymous visitors but the dashboard shell
// only after the cookie-backed identity has been verified.
const SESSION_AWARE_ROUTES = new Set(["public-status", "not-found"]);

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
  if (SESSION_AWARE_ROUTES.has(String(route.name)) && authStore.isSessionVerified) {
    return "dashboard";
  }
  return (route.meta.layout as LayoutName) || "default";
});
</script>

<template>
  <UApp>
    <component :is="layouts[layoutName]">
      <RouteErrorBoundary v-slot="{ attempt }">
        <RouterView :key="attempt" />
      </RouteErrorBoundary>
    </component>
    <UFooter />
  </UApp>
</template>

<style scoped></style>
