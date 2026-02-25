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
        <!-- Desktop navigation -->
        <div class="hidden sm:flex sm:flex-col sm:flex-1">
          <UNavigationMenu
            :collapsed="collapsed"
            :items="mainMenuItems"
            orientation="vertical"
          />

          <!-- Admin section, only visible to admins -->
          <template v-if="authStore.isAdmin">
            <UNavigationMenu
              :collapsed="collapsed"
              :items="adminMenuItems"
              orientation="vertical"
            />
          </template>

          <StorageInfoWidget class="mt-auto" />
          <UNavigationMenu
            :collapsed="collapsed"
            :items="settingsMenuItems"
            orientation="vertical"
            class="mt-2"
          />
        </div>

        <!-- Mobile navigation (large touch targets) -->
        <div class="flex flex-col flex-1 sm:hidden overflow-y-auto">
          <!-- Section: Library -->
          <div class="px-3 pt-5 pb-1">
            <p
              class="text-[10px] font-semibold uppercase tracking-widest text-muted px-2 mb-1"
            >
              Your Library
            </p>
          </div>
          <nav class="flex flex-col gap-0.5 px-3">
            <MobileNavItem
              v-for="item in mobileMainItems"
              :key="item.to"
              :icon="item.icon"
              :label="item.label"
              :to="item.to"
              :active="route.path === item.to"
            />
          </nav>

          <template v-if="authStore.isAdmin">
            <div class="px-3 pt-6 pb-1">
              <p
                class="text-[10px] font-semibold uppercase tracking-widest text-muted px-2 mb-1"
              >
                Admin
              </p>
            </div>
            <nav class="flex flex-col gap-0.5 px-3">
              <MobileNavItem
                v-for="item in mobileAdminItems"
                :key="item.to"
                :icon="item.icon"
                :label="item.label"
                :to="item.to"
                :active="route.path === item.to"
              />
            </nav>
          </template>

          <!-- Section: Settings -->
          <div class="px-3 pt-6 pb-1">
            <p
              class="text-[10px] font-semibold uppercase tracking-widest text-muted px-2 mb-1"
            >
              Account
            </p>
          </div>
          <nav class="flex flex-col gap-0.5 px-3">
            <MobileNavItem
              v-for="item in mobileSettingsItems"
              :key="item.to"
              :icon="item.icon"
              :label="item.label"
              :to="item.to"
              :active="route.path === item.to"
            />
          </nav>

          <!-- Storage widget -->
          <div class="mt-auto px-3 pb-3 pt-4">
            <StorageInfoWidget />
          </div>
        </div>
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
            <UTooltip text="Show app shortcuts">
              <UButton
                icon="material-symbols-light:keyboard-outline-rounded"
                size="xl"
                @click="openShortCutsModal"
                variant="ghost"
                color="neutral"
              />
            </UTooltip>
            <UTooltip text="Toggle light and dark mode">
              <UColorModeButton />
            </UTooltip>
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
import KeyboardShortcutsModal from "@/components/modals/KeyboardShortcutsModal.vue";
import { useAuthStore } from "@/stores/auth";
import router from "@/router";
import StorageInfoWidget from "@/components/dashboard/metrics/StorageInfoWidget.vue";
import MobileNavItem from "@/components/dashboard/MobileNavItem.vue";
import { useSettingsSync } from "@/composables/useSettingsSync";

useSettingsSync();

const authStore = useAuthStore();
const route = useRoute();

const overlay = useOverlay();
const shortcutsModal = overlay.create(KeyboardShortcutsModal);

const openShortCutsModal = async () => {
  const instance = shortcutsModal.open();
  await instance.result;
};

defineShortcuts({
  meta_k: openShortCutsModal,
});

const pageTitle = computed(() => {
  return (route.meta.title as string) || "Dashboard";
});

//Desktop navigation trees

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
        {
          label: "Trash",
          icon: "i-heroicons-trash",
          to: "/dashboard/trash",
        },
      ],
    },
  ],
];

const adminMenuItems = computed<NavigationMenuItem[][]>(() => [
  [
    {
      label: "Admin",
      icon: "i-heroicons-shield-check",
      defaultOpen: true,
      children: [
        {
          label: "Admin Dashboard",
          icon: "i-heroicons-chart-bar",
          to: "/dashboard/admin",
        },
        {
          label: "User Registry",
          icon: "i-heroicons-users",
          to: "/dashboard/admin/user-registry",
        },
        {
          label: "System Vitals",
          icon: "material-symbols:vitals",
          to: "/dashboard/admin/service-status",
        },
      ],
    },
  ],
]);

const mobileAdminItems = [
  {
    label: "Admin Dashboard",
    icon: "i-heroicons-chart-bar",
    to: "/dashboard/admin",
  },
  {
    label: "User Registry",
    icon: "i-heroicons-users",
    to: "/dashboard/admin/user-registry",
  },
];

const settingsMenuItems: NavigationMenuItem[][] = [
  [
    {
      label: "Settings",
      icon: "i-heroicons-cog-6-tooth",
      to: "/settings",
    },
    {
      label: "My Account",
      icon: "i-heroicons-user-circle",
      to: "/account",
    },
  ],
];

// Mobile flat item lists (same routes, no nesting)

const mobileMainItems = [
  { label: "File Explorer", icon: "i-heroicons-folder", to: "/dashboard" },
  {
    label: "Tags and Categories",
    icon: "i-heroicons-tag",
    to: "/dashboard/tags",
  },
  { label: "Access History", icon: "i-heroicons-clock", to: "/access-history" },
  { label: "Trash", icon: "i-heroicons-trash", to: "/dashboard/trash" },
];

const mobileSettingsItems = [
  { label: "Settings", icon: "i-heroicons-cog-6-tooth", to: "/settings" },
  { label: "My Account", icon: "i-heroicons-user-circle", to: "/account" },
];

const handleLogout = async () => {
  await authStore.logout();
  router.push("/auth");
};
</script>
