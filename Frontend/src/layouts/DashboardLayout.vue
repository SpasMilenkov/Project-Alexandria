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
        <UIcon v-else name="i-heroicons-home" class="size-5 text-primary mx-auto" />
      </template>

      <template #default="{ collapsed }">
        <!-- Desktop navigation -->
        <div class="hidden lg:flex lg:flex-col lg:flex-1">
          <UNavigationMenu :collapsed="collapsed" :items="mainMenuItems" orientation="vertical" />

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
        <div class="flex flex-col flex-1 lg:hidden overflow-y-auto">
          <!-- Section: Library -->
          <div class="px-3 pt-5 pb-1">
            <p class="text-[10px] font-semibold uppercase tracking-widest text-muted px-2 mb-1">
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
              <p class="text-[10px] font-semibold uppercase tracking-widest text-muted px-2 mb-1">
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
            <p class="text-[10px] font-semibold uppercase tracking-widest text-muted px-2 mb-1">
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
          variant="outline"
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
            <OnlineStatusIndicator />
            <UTooltip text="Show app shortcuts">
              <UButton
                class="md:block hidden"
                icon="material-symbols:keyboard-outline-rounded"
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
import { useOnboardingGuard } from "@/composables/useOnboardingGuard";
import { OnboardingStep } from "@/enums";

useSettingsSync();

useOnboardingGuard(OnboardingStep.Done);

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

const pageTitle = computed(() => (route.meta.title as string) || "Dashboard");

//Desktop navigation trees

const mainMenuItems: NavigationMenuItem[][] = [
  [
    {
      children: [
        {
          icon: "i-heroicons-folder",
          label: "File Explorer",
          to: "/dashboard",
        },
        {
          icon: "i-heroicons-tag",
          label: "Tags and Categories",
          to: "/dashboard/tags",
        },
        {
          icon: "i-heroicons-clock",
          label: "Access History",
          to: "/access-history",
        },
        {
          icon: "i-heroicons-trash",
          label: "Trash",
          to: "/dashboard/trash",
        },
      ],
      defaultOpen: true,
      icon: "i-heroicons-book-open",
      label: "Your Library",
    },
  ],
];

const adminMenuItems = computed<NavigationMenuItem[][]>(() => [
  [
    {
      children: [
        {
          icon: "i-heroicons-chart-bar",
          label: "Admin Dashboard",
          to: "/dashboard/admin",
        },
        {
          icon: "i-heroicons-users",
          label: "User Registry",
          to: "/dashboard/admin/user-registry",
        },
        {
          icon: "material-symbols:vitals",
          label: "System Vitals",
          to: "/dashboard/admin/service-status",
        },
      ],
      defaultOpen: true,
      icon: "i-heroicons-shield-check",
      label: "Admin",
    },
  ],
]);

const mobileAdminItems = [
  {
    icon: "i-heroicons-chart-bar",
    label: "Admin Dashboard",
    to: "/dashboard/admin",
  },
  {
    icon: "i-heroicons-users",
    label: "User Registry",
    to: "/dashboard/admin/user-registry",
  },
];

const settingsMenuItems: NavigationMenuItem[][] = [
  [
    {
      icon: "i-heroicons-cog-6-tooth",
      label: "Settings",
      to: "/settings",
    },
    {
      icon: "i-heroicons-user-circle",
      label: "My Account",
      to: "/account",
    },
  ],
];

// Mobile flat item lists (same routes, no nesting)

const mobileMainItems = [
  { icon: "i-heroicons-folder", label: "File Explorer", to: "/dashboard" },
  {
    icon: "i-heroicons-tag",
    label: "Tags and Categories",
    to: "/dashboard/tags",
  },
  { icon: "i-heroicons-clock", label: "Access History", to: "/access-history" },
  { icon: "i-heroicons-trash", label: "Trash", to: "/dashboard/trash" },
];

const mobileSettingsItems = [
  { icon: "i-heroicons-cog-6-tooth", label: "Settings", to: "/settings" },
  { icon: "i-heroicons-user-circle", label: "My Account", to: "/account" },
];

const handleLogout = async () => {
  await authStore.logout();
  router.push("/auth");
};
</script>
