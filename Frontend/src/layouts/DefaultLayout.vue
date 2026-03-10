<template>
  <OfflineBanner />
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

  <UMain class="flex items-center justify-center">
    <RouterView />
  </UMain>
  <UFooter>
    <template #left>
      <div class="flex items-center gap-2">
        <UIcon name="heroicons:book-open" class="w-5 h-5 text-primary" />
        <span class="text-sm font-medium text-gray-900 dark:text-white"> Alexandria </span>
      </div>
    </template>

    <template #default>
      <p class="text-xs text-gray-500 dark:text-gray-400">
        © {{ currentYear }} Alexandria. Built with care.
      </p>
    </template>

    <template #right>
      <div class="flex items-center gap-4">
        <a
          href="https://github.com/SpasMilenkov/Project-Alexandria"
          target="_blank"
          rel="noopener noreferrer"
          class="text-gray-500 hover:text-primary dark:text-gray-400 dark:hover:text-primary transition-colors"
        >
          <UIcon name="mdi:github" class="w-5 h-5" />
        </a>
      </div>
    </template>
  </UFooter>
</template>
<script setup lang="ts">
import type { NavigationMenuItem } from "@nuxt/ui";
import { computed, ref } from "vue";
import { useRoute } from "vue-router";
import { useAuthStore } from "@/stores/auth";
import OfflineBanner from "@/components/common/OfflineBanner.vue";
const currentYear = ref(new Date().getFullYear());
const authStore = useAuthStore();
const route = useRoute();

const items = computed<NavigationMenuItem[]>(() => {
  const itemArray = [
    {
      active: route.path.startsWith("/auth"),
      label: "Log in",
      to: "/auth",
    },
  ];
  if (authStore.isAuthenticated) {
    itemArray.push({
      active: route.path.startsWith("/dashboard"),
      label: "Dashboard",
      to: "/dashboard",
    });
  }
  return itemArray;
});
</script>
<style scoped></style>
