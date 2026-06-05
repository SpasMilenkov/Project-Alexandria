<template>
  <UDashboardGroup storage-key="alexandria-sidebar">
    <UDashboardSidebar
      v-model:collapsed="isCollapsed"
      collapsible
      resizable
      :min-size="15"
      :max-size="24"
      :default-size="15"
      :ui="{
        footer: 'border-t border-default py-2',
      }"
      mode="modal"
      toggle-side="right"
    >
      <template #header="{ collapsed }">
        <LogoComponent v-if="!collapsed" class="h-5 w-auto shrink-0" />
        <UDashboardSidebarCollapse class="ms-auto" />
      </template>

      <template #default="{ collapsed }">
        <div class="hidden lg:flex lg:flex-col lg:flex-1 gap-1">
          <p
            v-if="!collapsed"
            class="text-[10px] font-semibold uppercase tracking-widest text-dimmed px-2 pt-1 pb-0.5 select-none"
          >
            Your Library
          </p>
          <UNavigationMenu
            :collapsed="collapsed"
            :items="libraryMenuItems"
            orientation="vertical"
          />

          <USeparator class="my-1" />
          <p
            v-if="!collapsed"
            class="text-[10px] font-semibold uppercase tracking-widest text-dimmed px-2 pt-1 pb-0.5 select-none"
          >
            Media Streaming
          </p>
          <UNavigationMenu
            :collapsed="collapsed"
            :items="streamingMenuItems"
            orientation="vertical"
          />

          <template v-if="authStore.isAdmin">
            <USeparator class="my-1" />
            <p
              v-if="!collapsed"
              class="text-[10px] font-semibold uppercase tracking-widest text-dimmed px-2 pt-1 pb-0.5 select-none"
            >
              Admin
            </p>
            <UNavigationMenu
              :collapsed="collapsed"
              :items="adminMenuItems"
              orientation="vertical"
            />
          </template>

          <StorageInfoWidget class="mt-auto" />
          <USeparator class="my-1" />
          <UNavigationMenu
            :collapsed="collapsed"
            :items="settingsMenuItems"
            orientation="vertical"
          />
        </div>

        <div class="flex flex-col flex-1 lg:hidden overflow-y-auto">
          <div class="px-3 pt-5 pb-1">
            <p class="text-[10px] font-semibold uppercase tracking-widest text-dimmed px-2 mb-1">
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

          <template v-if="streamingEnabled">
            <div class="px-3 pt-6 pb-1">
              <p class="text-[10px] font-semibold uppercase tracking-widest text-dimmed px-2 mb-1">
                Streaming
              </p>
            </div>
            <nav class="flex flex-col gap-0.5 px-3">
              <MobileNavItem
                v-for="item in mobileStreamingItems"
                :key="item.to"
                :icon="item.icon"
                :label="item.label"
                :to="item.to"
                :active="route.path === item.to"
              />
            </nav>
          </template>

          <template v-if="authStore.isAdmin">
            <div class="px-3 pt-6 pb-1">
              <p class="text-[10px] font-semibold uppercase tracking-widest text-dimmed px-2 mb-1">
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

          <div class="px-3 pt-6 pb-1">
            <p class="text-[10px] font-semibold uppercase tracking-widest text-dimmed px-2 mb-1">
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

          <div class="mt-auto px-3 pb-3 pt-4">
            <StorageInfoWidget />
          </div>
        </div>
      </template>

      <template #footer="{ collapsed }">
        <UButton
          :label="collapsed ? undefined : 'Log out'"
          icon="i-heroicons-arrow-right-on-rectangle"
          color="neutral"
          variant="outline"
          :square="collapsed"
          block
          class="hover:text-error"
          @click="handleLogout"
        />
      </template>
    </UDashboardSidebar>

    <UDashboardPanel :ui="{ body: 'sm:p-0 p-0' }">
      <template #header>
        <UDashboardNavbar toggle-side="right">
          <template #right>
            <OnlineStatusIndicator />
            <UTooltip text="Show app shortcuts">
              <UButton
                class="md:block hidden"
                icon="material-symbols:keyboard-outline-rounded"
                size="xl"
                variant="ghost"
                color="neutral"
                @click="openShortCutsModal"
              />
            </UTooltip>
            <UTooltip text="Toggle light and dark mode">
              <UColorModeButton />
            </UTooltip>
          </template>
        </UDashboardNavbar>
      </template>

      <template #body>
        <div class="flex flex-col h-full">
          <div class=" flex-1 min-h-0 overflow-y-auto h-full">
            <slot />
          </div>

          <template v-if="streamingEnabled">
            <component
              :is="AudioSkin"
              v-if="
                AudioSkin &&
                !player.activeFile?.isVideo &&
                player.hasActiveFile &&
                !route.path.includes('streaming/videos')
              "
              :style="{ visibility: player.hasActiveFile ? 'visible' : 'hidden' }"
            />
          </template>
        </div>
      </template>
    </UDashboardPanel>
  </UDashboardGroup>
</template>

<script setup lang="ts">
import type { NavigationMenuItem } from "@nuxt/ui";

import { computed, defineAsyncComponent, ref } from "vue";
import { useRoute } from "vue-router";

import StorageInfoWidget from "@/components/dashboard/metrics/StorageInfoWidget.vue";
import MobileNavItem from "@/components/dashboard/MobileNavItem.vue";
import KeyboardShortcutsModal from "@/components/modals/KeyboardShortcutsModal.vue";
import { useOnboardingGuard } from "@/composables/useOnboardingGuard";
import { useSettingsSync } from "@/composables/useSettingsSync";
import { OnboardingStep } from "@/enums";
import router from "@/router";
import { useAuthStore } from "@/stores/auth";
import { usePlayerStore } from "@/stores/stream-player";

const streamingEnabled = import.meta.env.VITE_STREAMING_ENABLED === "true";

const AudioSkin = streamingEnabled
  ? defineAsyncComponent(() => import("@/components/streaming/AudioPlayerSkin.vue"))
  : null;



const player = usePlayerStore();

useSettingsSync();
useOnboardingGuard(OnboardingStep.Done);

const authStore = useAuthStore();
const route = useRoute();

const isCollapsed = ref(false);

const overlay = useOverlay();
const shortcutsModal = overlay.create(KeyboardShortcutsModal);

const openShortCutsModal = async () => {
  const instance = shortcutsModal.open();
  await instance.result;
};

defineShortcuts({
  meta_k: openShortCutsModal,
});

// Desktop navigation
const libraryMenuItems: NavigationMenuItem[] = [
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
];

const streamingMenuItems: NavigationMenuItem[] = [
  { icon: "mdi:music-note", label: "Music", to: "/streaming/music" },
  { icon: "mdi:playlist-play", label: "Playlists", to: "/streaming/playlists" },
  { icon: "mdi:film-open-outline", label: "Videos", to: "/streaming/videos" },
  { icon: "mdi:luggage", label: "Jobs", to: "/streaming/jobs" },
  { icon: "mdi:history", label: "History", to: "/streaming/history" },
];

const adminMenuItems = computed<NavigationMenuItem[]>(() => [
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
]);

const settingsMenuItems: NavigationMenuItem[] = [
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
];

// Mobile navigation

const mobileMainItems = [
  { icon: "i-heroicons-folder", label: "File Explorer", to: "/dashboard" },
  { icon: "i-heroicons-tag", label: "Tags and Categories", to: "/dashboard/tags" },
  { icon: "i-heroicons-clock", label: "Access History", to: "/access-history" },
  { icon: "i-heroicons-trash", label: "Trash", to: "/dashboard/trash" },
];

const mobileStreamingItems = [
  { icon: "mdi:music-note", label: "Music", to: "/streaming/music" },
  { icon: "mdi:playlist-play", label: "Playlists", to: "/streaming/playlists" },
  { icon: "mdi:film-open-outline", label: "Videos", to: "/streaming/videos" },
  { icon: "mdi:luggage", label: "Jobs", to: "/streaming/jobs" },
  { icon: "mdi:history", label: "History", to: "/streaming/history" },
];

const mobileAdminItems = [
  { icon: "i-heroicons-chart-bar", label: "Admin Dashboard", to: "/dashboard/admin" },
  { icon: "i-heroicons-users", label: "User Registry", to: "/dashboard/admin/user-registry" },
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
