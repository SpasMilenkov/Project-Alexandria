<template>
  <div v-if="collapsed" class="flex flex-col">
    <USeparator v-if="section.collapsible" class="px-2 py-2" />

    <UNavigationMenu
      collapsed
      tooltip
      :items="menuItems"
      :aria-label="navLabel"
      orientation="vertical"
    />
  </div>

  <div v-else class="flex flex-col" :class="{ 'pt-3': section.collapsible }">
    <button
      v-if="section.collapsible"
      type="button"
      class="flex h-7 w-full cursor-pointer items-center gap-1.5 rounded-md ps-2.5 pe-2 text-left text-[11px] font-semibold tracking-widest text-gray-600 uppercase transition-colors select-none hover:bg-black/4 hover:text-gray-900 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary dark:text-gray-400 dark:hover:bg-white/5 dark:hover:text-gray-100"
      :aria-expanded="open"
      :aria-controls="panelId"
      @click="open = !open"
    >
      <span class="truncate">{{ section.label }}</span>
      <UIcon
        name="i-lucide-chevron-down"
        class="ms-auto size-3.5 shrink-0 transition-transform duration-200 ease-out"
        :class="{ '-rotate-90': !open }"
      />
    </button>

    <div
      :id="panelId"
      class="grid transition-[grid-template-rows] duration-200 ease-out"
      :class="isPanelOpen ? 'grid-rows-[1fr]' : 'grid-rows-[0fr]'"
      :inert="!isPanelOpen"
    >
      <div class="min-h-0 overflow-hidden">
        <UNavigationMenu :items="menuItems" :aria-label="navLabel" orientation="vertical" />
      </div>
    </div>

    <UNavigationMenu
      v-if="peekItems.length > 0"
      :items="peekItems"
      :aria-label="`Current ${section.label} page`"
      orientation="vertical"
    />
  </div>
</template>

<script setup lang="ts">
import type { NavigationMenuItem } from "@nuxt/ui";

import { computed, useId } from "vue";
import { useRoute } from "vue-router";

import { type SidebarSection, isSidebarItemActive } from "@/utils/sidebar-navigation";

const { section, collapsed = false } = defineProps<{
  section: SidebarSection;
  collapsed?: boolean;
}>();

const open = defineModel<boolean>("open", { default: true });

const route = useRoute();
const panelId = useId();

const navLabel = computed(() => `${section.label} navigation`);
const isPanelOpen = computed(() => !section.collapsible || open.value);

const menuItems = computed<NavigationMenuItem[]>(() =>
  section.items.map((item) => ({
    active: isSidebarItemActive(item, route.name),
    icon: item.icon,
    label: item.label,
    to: item.to,
  })),
);

// A closed group keeps the current page visible so the active highlight never disappears.
const peekItems = computed(() => {
  if (isPanelOpen.value) return [];

  return menuItems.value.filter((item) => item.active);
});
</script>
