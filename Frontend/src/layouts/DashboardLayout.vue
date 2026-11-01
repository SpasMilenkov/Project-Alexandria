<template>
  <UDashboardGroup storage-key="alexandria-sidebar" unit="rem">
    <UDashboardSidebar
      v-model:collapsed="isCollapsed"
      collapsible
      resizable
      :min-size="13"
      :max-size="20"
      :default-size="14"
      :ui="{
        root: 'frosted-glass glass-surface',
        footer: 'border-t border-default py-2',
      }"
      mode="modal"
      toggle-side="right"
    >
      <template #header="{ collapsed }">
        <LogoComponent v-if="!collapsed" class="h-5 w-auto shrink-0" />
        <UDashboardSidebarCollapse :class="collapsed ? 'mx-auto' : 'ms-auto'" />
      </template>

      <template #default="{ collapsed }">
        <div class="hidden lg:flex lg:flex-col lg:flex-1 gap-1 min-w-0 overflow-x-hidden">
          <SidebarSection
            v-for="section in sidebarSections"
            :key="section.id"
            :section="section"
            :collapsed="collapsed"
            :open="isSectionOpen(section.id)"
            @update:open="(value) => setSectionOpen(section.id, value)"
          />
        </div>

        <div class="flex flex-col flex-1 min-w-0 lg:hidden overflow-y-auto overflow-x-hidden">
          <template v-for="section in sidebarSections" :key="section.id">
            <div v-if="section.id !== 'library'" class="px-3 pt-6 pb-1">
              <p class="text-[10px] font-semibold uppercase tracking-widest text-dimmed px-2 mb-1">
                {{ section.label }}
              </p>
            </div>

            <nav
              class="flex flex-col gap-0.5 px-3"
              :class="{ 'pt-5': section.id === 'library' }"
              :aria-label="`${section.label} navigation`"
            >
              <MobileNavItem
                v-for="item in section.items"
                :key="item.to"
                :icon="item.icon"
                :label="item.label"
                :to="item.to"
                :active="isSidebarItemActive(item, route.name)"
              />
            </nav>
          </template>

          <div class="px-3 pt-6 pb-1">
            <p class="text-[10px] font-semibold uppercase tracking-widest text-dimmed px-2 mb-1">
              Account
            </p>
          </div>
          <nav class="flex flex-col gap-0.5 px-3 pb-3 min-w-0" aria-label="Account navigation">
            <MobileNavItem
              v-for="item in mobileAccountItems"
              :key="item.label"
              :icon="item.icon"
              :label="item.label"
              :to="item.to"
              :action="item.action"
            />
          </nav>
        </div>
      </template>

      <template #footer="{ collapsed }">
        <div class="hidden w-full min-w-0 lg:block">
          <SidebarUserMenu
            :collapsed="collapsed"
            @logout="handleLogout"
            @report-problem="openReportProblemModal"
          />
        </div>

        <UButton
          label="Log out"
          icon="i-lucide-log-out"
          color="neutral"
          variant="link"
          size="sm"
          block
          class="justify-start text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-100 lg:hidden"
          @click="handleLogout"
        />
      </template>
    </UDashboardSidebar>

    <UDashboardPanel :ui="{ body: 'sm:p-0 p-0 gap-0 overflow-hidden' }">
      <template #header>
        <UDashboardNavbar toggle-side="right" :ui="{ root: 'glass-surface frosted-glass', left: 'flex-1 min-w-0' }">
          <template #left>
            <UButton
              v-if="player.playerDismissed && player.hasActiveFile && player.isAudio"
              label="Resume music"
              icon="mdi:music-note"
              color="primary"
              variant="soft"
              size="sm"
              @click="player.resumePlayer()"
            />
            <MobilePlayerIsland
              v-if="
                MobilePlayerIsland &&
                streamingEnabled &&
                player.isAudio &&
                player.hasActiveFile &&
                !player.playerDismissed
              "
            />
          </template>
          <template #right>
            <div id="dashboard-navbar-actions" class="flex items-center gap-2 empty:hidden" />

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
        <div class="flex flex-col flex-1 min-h-0 min-w-0">
          <div class="flex min-h-0 flex-1 flex-row min-w-0 overflow-hidden">
            <div class="flex-1 min-w-0 overflow-y-auto h-full">
              <slot />
            </div>
            <div class="hidden md:contents">
              <LyricsPanel v-model:open="player.lyricsOpen" />
            </div>
          </div>

          <template v-if="streamingEnabled">
            <component :is="AudioEngineHost" v-if="AudioEngineHost" />
            <component
              :is="AudioSkin"
              class="shrink-0"
              v-if="AudioSkin && player.isAudio && player.hasActiveFile && !player.playerDismissed"
              :style="{ visibility: player.hasActiveFile ? 'visible' : 'hidden' }"
            />
          </template>
        </div>
      </template>
    </UDashboardPanel>
  </UDashboardGroup>
</template>

<script setup lang="ts">
import { computed, defineAsyncComponent, ref } from "vue";
import { useRoute } from "vue-router";

import MobileNavItem from "@/components/dashboard/MobileNavItem.vue";
import SidebarSection from "@/components/dashboard/SidebarSection.vue";
import SidebarUserMenu from "@/components/dashboard/SidebarUserMenu.vue";
import KeyboardShortcutsModal from "@/components/modals/KeyboardShortcutsModal.vue";
import ReportProblemModal from "@/components/modals/ReportProblemModal.vue";
import LyricsPanel from "@/components/streaming/LyricsPanel.vue";
import { useOnboardingGuard } from "@/composables/useOnboardingGuard";
import { useSettingsSync } from "@/composables/useSettingsSync";
import { useStreamingMediaContext } from "@/composables/useStreamingMediaContext";
import { OnboardingStep } from "@/enums";
import router from "@/router";
import { useAuthStore } from "@/stores/auth";
import { useSettingsStore } from "@/stores/settings";
import { usePlayerStore } from "@/stores/stream-player";
import {
  type SidebarSectionId,
  buildSidebarSections,
  isSidebarItemActive,
} from "@/utils/sidebar-navigation";

const streamingEnabled = import.meta.env.VITE_STREAMING_ENABLED === "true";

// Add after the existing composable calls (useSettingsSync, useOnboardingGuard)
// The guard matches the streamingEnabled flag already used in the template.
if (streamingEnabled) {
  useStreamingMediaContext();
}

const AudioSkin = streamingEnabled
  ? defineAsyncComponent(() => import("@/components/streaming/AudioPlayerSkin.vue"))
  : null;
const AudioEngineHost = streamingEnabled
  ? defineAsyncComponent(() => import("@/components/streaming/AudioEngineHost.vue"))
  : null;
const MobilePlayerIsland = streamingEnabled
  ? defineAsyncComponent(() => import("@/components/streaming/MobilePlayerIsland.vue"))
  : null;

const player = usePlayerStore();

useSettingsSync();
useOnboardingGuard(OnboardingStep.Done);

const authStore = useAuthStore();
const settingsStore = useSettingsStore();
const route = useRoute();

const isCollapsed = ref(false);

const overlay = useOverlay();
const shortcutsModal = overlay.create(KeyboardShortcutsModal);

const openShortCutsModal = async () => {
  const instance = shortcutsModal.open();
  await instance.result;
};

const reportProblemModal = overlay.create(ReportProblemModal);

const openReportProblemModal = async () => {
  const instance = reportProblemModal.open();
  await instance.result;
};

defineShortcuts({
  meta_k: openShortCutsModal,
});

const sidebarSections = computed(() =>
  buildSidebarSections({ isAdmin: authStore.isAdmin, streamingEnabled }),
);

const isSectionOpen = (id: SidebarSectionId) => {
  if (id === "media") return settingsStore.isMediaSectionOpen;
  if (id === "admin") return settingsStore.isAdminSectionOpen;

  return true;
};

const setSectionOpen = (id: SidebarSectionId, value: boolean) => {
  if (id === "media") settingsStore.setMediaSectionOpen(value);
  if (id === "admin") settingsStore.setAdminSectionOpen(value);
};

interface MobileAccountItem {
  icon: string;
  label: string;
  to?: string;
  action?: () => void;
}

const mobileAccountItems: MobileAccountItem[] = [
  { icon: "i-lucide-user-round", label: "My account", to: "/account" },
  { icon: "i-lucide-settings", label: "Settings", to: "/settings" },
  { icon: "i-lucide-hard-drive", label: "My storage", to: "/my-storage" },
  {
    icon: "i-lucide-life-buoy",
    label: "Report a problem",
    action: () => {
      void openReportProblemModal();
    },
  },
];

const handleLogout = async () => {
  const logout = authStore.logout();

  await router.push("/auth");
  await logout;
};
</script>
