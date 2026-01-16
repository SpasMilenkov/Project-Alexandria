<template>
  <UDashboardGroup>
    <UDashboardSidebar
      collapsible
      :ui="{
        footer: 'border-t border-default',
      }"
      class="w-60"
      mode="modal"
      toggle-side="right"
    >
      <template #header="{ collapsed }">
        <LogoComponent v-if="!collapsed" class="h-5 w-auto shrink-0" />
        <UIcon
          v-else
          name="i-heroicons-home"
          class="size-5 text-primary mx-auto"
        />
      </template>
      <template #default="{ collapsed }">
        <!-- Main Navigation -->
        <UNavigationMenu
          :collapsed="collapsed"
          :items="mainMenuItems"
          orientation="vertical"
        />
        <!-- Bottom Navigation (Settings) -->
        <UNavigationMenu
          :collapsed="collapsed"
          :items="settingsMenuItems"
          orientation="vertical"
          class="mt-auto"
        />
      </template>
      <template #footer="{ collapsed }">
        <UButton
          :label="collapsed ? undefined : 'Log out'"
          icon="i-heroicons-arrow-right-on-rectangle"
          color="error"
          variant="soft"
          block
          :square="collapsed"
          @click="handleLogout"
        />
      </template>
    </UDashboardSidebar>
    <UDashboardPanel :ui="{ body: 'sm:p-0 p-0' }">
      <template #header>
        <UDashboardNavbar :title="pageTitle" toggle-side="right">
          <template #right>
            <UColorModeButton />
          </template>
        </UDashboardNavbar>
      </template>
      <template #body>
        <slot />
      </template>
    </UDashboardPanel>
  </UDashboardGroup>
</template>

<script setup lang="ts">
import type { NavigationMenuItem } from "@nuxt/ui";
import { useRoute } from "vue-router";
import { computed } from "vue";

const route = useRoute();

// Dynamic page title based on route
const pageTitle = computed(() => {
  return (route.meta.title as string) || "Dashboard";
});

const mainMenuItems: NavigationMenuItem[][] = [
  [
    {
      label: "Your Library",
      icon: "i-heroicons-book-open",
      defaultOpen: true,
      children: [
        {
          label: "File Explorer",
          icon: "i-heroicons-folder",
          to: "/dashboard",
        },
        {
          label: "Tags and Categories",
          icon: "i-heroicons-tag",
          to: "/dashboard/tags",
        },
        {
          label: "Access History",
          icon: "i-heroicons-clock",
          to: "/access-history",
        },
      ],
    },
  ],
];

const settingsMenuItems: NavigationMenuItem[][] = [
  [
    {
      label: "Settings",
      icon: "i-heroicons-cog-6-tooth",
      defaultOpen: false,
      children: [
        {
          label: "Look and Feel",
          icon: "i-heroicons-paint-brush",
          to: "/appearance",
        },
        {
          label: "My Account",
          icon: "i-heroicons-user-circle",
          to: "/account",
        },
      ],
    },
  ],
];

const handleLogout = () => {
  // Add your logout logic here
  console.log("Logging out...");
};
</script>
