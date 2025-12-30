<template>
  <UHeader>
    <template #title>Alexandria</template>
    <UNavigationMenu :items="items" />
    <template #right>
      <UColorModeButton />
    </template>
    <template #body>
      <UNavigationMenu :items="items" orientation="vertical" class="-mx-2.5" />
    </template>
  </UHeader>

  <UMain>
    <RouterView />
  </UMain>
  <UFooter />
</template>
<script setup lang="ts">
import type { NavigationMenuItem } from "@nuxt/ui";
import { computed } from "vue";
import { useRoute } from "vue-router";
import { useAuthStore } from "@/stores/auth";

const authStore = useAuthStore();
const route = useRoute();

const items = computed<NavigationMenuItem[]>(() => {
  const itemArray = [
    {
      label: "Log in",
      to: "/auth",
      active: route.path.startsWith("/auth"),
    },
  ];
  if (authStore.isAuthenticated)
    itemArray.push({
      label: "Dashboard",
      to: "/dashboard",
      active: route.path.startsWith("/dashboard"),
    });
  return itemArray;
});
</script>
<style scoped></style>
