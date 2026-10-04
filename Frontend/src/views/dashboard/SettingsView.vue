<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useMediaQuery } from "@vueuse/core";
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";

import AppearanceSection from "@/components/dashboard/settings/AppearanceSection.vue";
import AutoPlaylistsSection from "@/components/dashboard/settings/AutoPlaylistsSection.vue";
import AutoTaggingSection from "@/components/dashboard/settings/AutoTaggingSection.vue";
import BehaviorSection from "@/components/dashboard/settings/BehaviorSection.vue";
import { useSettingsSearch } from "@/composables/useSettingsSearch";
import { useSettingsSync } from "@/composables/useSettingsSync";
import { useSettingsStore } from "@/stores/settings";

const settingsStore = useSettingsStore();
const { isSavingAppearance, isSavingBehavior } = useSettingsSync();
const route = useRoute();
const router = useRouter();

const autoTaggingEnabled = import.meta.env.VITE_AUTOTAGGING_ENABLED === "true";

const isSaving = computed(() => isSavingAppearance.value || isSavingBehavior.value);

const allOpen = computed(
  () =>
    settingsStore.isAppearanceSectionOpen &&
    settingsStore.isBehaviorSectionOpen &&
    settingsStore.isAutoTaggingSectionOpen &&
    settingsStore.isAutoPlaylistsSectionOpen,
);

const toggleAll = () => {
  const next = !allOpen.value;
  settingsStore.setAppearanceSectionOpen(next);
  settingsStore.setBehaviorSectionOpen(next);
  settingsStore.setAutoTaggingSectionOpen(next);
  settingsStore.setAutoPlaylistsSectionOpen(next);
};

const handleResetAll = () => {
  settingsStore.resetSettings();
};

const SECTION_IDS = ["appearance", "behavior", "auto-tagging", "auto-playlists"] as const;

type SectionId = (typeof SECTION_IDS)[number];

const isMobile = useMediaQuery("(max-width: 767px)");
const scrollContainer = ref<HTMLDivElement | null>(null);

const visibleSections = computed<SectionId[]>(() =>
  autoTaggingEnabled ? [...SECTION_IDS] : SECTION_IDS.filter((id) => id !== "auto-tagging"),
);

// On phones the page becomes overview → single-section detail. Native history does the
// back-stack: every detail arrival is a router hash push, so back returns to overview.
const mobileDetail = computed<SectionId | null>(() => {
  if (!isMobile.value) {
    return null;
  }
  const hash = route.hash.slice(1);
  const found = visibleSections.value.find((id) => id === hash);
  return found ?? null;
});

const setSectionOpen: Record<SectionId, (open: boolean) => void> = {
  appearance: (open) => settingsStore.setAppearanceSectionOpen(open),
  "auto-playlists": (open) => settingsStore.setAutoPlaylistsSectionOpen(open),
  "auto-tagging": (open) => settingsStore.setAutoTaggingSectionOpen(open),
  behavior: (open) => settingsStore.setBehaviorSectionOpen(open),
};

// Scrollspy publishes the visible section to the hash. The observer band is
// viewport-relative, so resizes alone can shift it across sections: publishes only
// count when they follow a real scroll, and never while a programmatic scroll runs.
let spyLockUntil = 0;
let lastScrollAt = 0;
let observer: IntersectionObserver | null = null;

const onSettingsScroll = () => {
  lastScrollAt = Date.now();
};

watch(
  () => route.hash,
  (hash) => {
    if (!hash || Date.now() < spyLockUntil) {
      return;
    }
    spyLockUntil = Date.now() + 900;
    nextTick().then(() => {
      document.getElementById(hash.slice(1))?.scrollIntoView({ behavior: "smooth" });
    });
  },
  { immediate: true },
);

onMounted(() => {
  const visible = autoTaggingEnabled
    ? [...SECTION_IDS]
    : SECTION_IDS.filter((id) => id !== "auto-tagging");
  observer = new IntersectionObserver(
    (entries) => {
      if (Date.now() < spyLockUntil || Date.now() - lastScrollAt > 500) {
        return;
      }
      const hit = entries.find((entry) => entry.isIntersecting);
      if (!hit || !(hit.target instanceof HTMLElement)) {
        return;
      }
      const next = `#${hit.target.id}`;
      if (route.hash !== next) {
        spyLockUntil = Date.now() + 900;
        void router.replace({ hash: next });
      }
    },
    { rootMargin: "-30% 0px -60% 0px" },
  );
  visible.forEach((id) => {
    const el = document.getElementById(id);
    if (el) {
      observer?.observe(el);
    }
  });
  scrollContainer.value?.addEventListener("scroll", onSettingsScroll, { passive: true });
});

onBeforeUnmount(() => {
  observer?.disconnect();
  observer = null;
  scrollContainer.value?.removeEventListener("scroll", onSettingsScroll);
});

interface SectionMeta {
  id: SectionId;
  icon: string;
  title: string;
  sub: string;
}

const sectionMetaList: SectionMeta[] = [
  {
    icon: "mdi:palette-outline",
    id: "appearance",
    sub: "Colors, glass, file tiles",
    title: "Appearance",
  },
  { icon: "mdi:cog-outline", id: "behavior", sub: "Prompts and notifications", title: "Behavior" },
  {
    icon: "mdi:tag-multiple",
    id: "auto-tagging",
    sub: "Rules for re-running tags",
    title: "Auto-tagging",
  },
  {
    icon: "mdi:playlist-music",
    id: "auto-playlists",
    sub: "When playlists get made",
    title: "Auto-playlists",
  },
];

const mobileGroups = computed(() => [
  { ids: ["appearance"] as SectionId[], label: "Personalize" },
  { ids: ["behavior"] as SectionId[], label: "Workflow" },
  {
    ids: (autoTaggingEnabled
      ? ["auto-tagging", "auto-playlists"]
      : ["auto-playlists"]) as SectionId[],
    label: "Library automation",
  },
]);

const metaOf = (id: SectionId): SectionMeta =>
  sectionMetaList.find((meta) => meta.id === id) ?? sectionMetaList[0];

const isModified = (id: SectionId): boolean => {
  if (id === "appearance") {
    return settingsStore.isAppearanceModified;
  }
  if (id === "behavior") {
    return settingsStore.isBehaviorModified;
  }
  if (id === "auto-tagging") {
    return settingsStore.isAutoTaggingModified;
  }
  return settingsStore.isAutoPlaylistsModified;
};

const nextTarget = computed<{ id: SectionId | null; label: string }>(() => {
  const order = visibleSections.value;
  const current = mobileDetail.value;
  if (!current) {
    return { id: order[0] ?? null, label: "Appearance" };
  }
  const next = order[order.indexOf(current) + 1] ?? null;
  if (!next) {
    return { id: null, label: "All settings" };
  }
  return { id: next, label: metaOf(next).title };
});

const goNext = () => {
  const target = nextTarget.value;
  if (target.id) {
    void router.push({ hash: `#${target.id}` });
  } else {
    void router.push({ path: "/settings" });
  }
};

watch(mobileDetail, () => {
  scrollContainer.value?.scrollTo({ top: 0 });
});

const {
  goToEntry,
  highlight,
  onSearchKeydown,
  resultsStyle,
  searchActive,
  searchInput,
  searchOpen,
  searchQuery,
  searchResults,
} = useSettingsSearch();
</script>

<template>
  <div ref="scrollContainer" class="w-full h-full overflow-y-auto">
    <div class="settings-content max-w-7xl mx-auto p-6 space-y-6">
      <!-- Header -->
      <UCard
        v-show="!isMobile || !mobileDetail"
        class="frosted-glass glass-surface"
        :ui="{ body: 'p-4 sm:p-5 space-y-4' }"
      >
        <div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
          <div class="space-y-1">
            <h1 class="text-2xl font-bold">Settings</h1>
            <p class="text-gray-600 dark:text-gray-400">
              Customize your experience. Changes are saved automatically.
            </p>
          </div>
          <div class="flex items-center gap-2 shrink-0">
            <span
              class="inline-flex items-center gap-1.5 text-xs text-gray-600 dark:text-gray-400"
              role="status"
              aria-live="polite"
            >
              <UIcon
                v-if="isSaving"
                name="i-lucide-loader-circle"
                class="size-4 animate-spin text-primary"
              />
              <UIcon
                v-else
                name="i-lucide-circle-check"
                class="size-4 text-green-600 dark:text-green-500"
              />
              {{ isSaving ? "Saving..." : "All changes saved" }}
            </span>
            <UTooltip :text="allOpen ? 'Collapse all' : 'Expand all'">
              <UButton
                :icon="allOpen ? 'i-lucide-chevrons-down-up' : 'i-lucide-chevrons-up-down'"
                color="neutral"
                variant="ghost"
                :aria-label="allOpen ? 'Collapse all' : 'Expand all'"
                :aria-expanded="allOpen"
                @click="toggleAll"
              />
            </UTooltip>
            <UButton
              label="Reset all settings"
              color="error"
              variant="outline"
              icon="i-heroicons-arrow-path"
              @click="handleResetAll"
            />
          </div>
        </div>

        <div class="relative" role="search">
          <UInput
            ref="searchInput"
            v-model="searchQuery"
            icon="i-lucide-search"
            placeholder="Search settings"
            aria-label="Search settings"
            role="combobox"
            class="settings-search-input w-full"
            :aria-expanded="String(searchOpen && searchQuery.trim() !== '')"
            aria-controls="settings-search-results"
            :aria-activedescendant="`settings-result-${searchActive}`"
            autocomplete="off"
            @focus="searchOpen = searchQuery.trim() !== ''"
            @blur="searchOpen = false"
            @keydown="onSearchKeydown"
          >
            <template v-if="searchQuery" #trailing>
              <UButton
                icon="i-lucide-x"
                color="neutral"
                variant="ghost"
                size="xs"
                aria-label="Clear search"
                @mousedown.prevent="searchQuery = ''"
              />
            </template>
            <template v-else #trailing>
              <kbd
                class="px-1.5 py-0.5 rounded border border-gray-200 dark:border-gray-700 text-[11px] text-gray-500 dark:text-gray-500"
                aria-hidden="true"
                >/</kbd
              >
            </template>
          </UInput>

          <Teleport to="body">
            <div
              v-if="searchOpen && searchQuery.trim() !== ''"
              id="settings-search-results"
              role="listbox"
              aria-label="Search results"
              class="fixed z-[70] max-h-105 overflow-y-auto rounded-xl border border-gray-200 dark:border-gray-700 bg-default shadow-lg p-1.5"
              :style="resultsStyle"
            >
            <div
              v-if="searchResults.length === 0"
              class="px-3 py-6 text-center text-sm text-gray-600 dark:text-gray-400"
            >
              No settings match that search
            </div>
            <button
              v-for="(entry, index) in searchResults"
              :id="`settings-result-${index}`"
              :key="`${entry.section}-${entry.title}`"
              type="button"
              role="option"
              :aria-selected="index === searchActive"
              class="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left transition-colors"
              :class="
                index === searchActive
                  ? 'bg-gray-100 dark:bg-neutral-800 ring-1 ring-primary'
                  : 'hover:bg-gray-100 dark:hover:bg-neutral-800'
              "
              @mousedown.prevent="goToEntry(entry)"
              @mousemove="searchActive = index"
            >
              <span
                class="text-sm font-medium text-gray-900 dark:text-gray-100"
                v-html="highlight(entry.title)"
              />
              <span class="text-xs text-gray-500 dark:text-gray-400 truncate">{{
                entry.desc
              }}</span>
              <span
                v-if="entry.value()"
                class="ml-auto shrink-0 text-xs text-gray-600 dark:text-gray-400"
                >{{ entry.value() }}</span
              >
            </button>
            </div>
          </Teleport>
        </div>
      </UCard>

      <!-- Phone overview: section list first, one section at a time after -->
      <div v-if="isMobile && !mobileDetail" class="space-y-5">
        <div v-for="group in mobileGroups" :key="group.label">
          <p class="text-xs font-semibold text-gray-600 dark:text-gray-400 mb-2 px-1">
            {{ group.label }}
          </p>
          <UCard class="frosted-glass glass-surface" :ui="{ body: 'p-2' }">
            <button
              v-for="id in group.ids"
              :key="id"
              type="button"
              class="flex w-full items-center gap-3 rounded-xl px-3 py-3 text-left transition-colors active:bg-gray-100 dark:active:bg-neutral-800"
              @click="void router.push({ hash: `#${id}` })"
            >
              <span
                class="w-9 h-9 flex-none grid place-items-center rounded-[10px] border border-gray-200 dark:border-gray-700 text-gray-500 dark:text-gray-400"
              >
                <Icon :icon="metaOf(id).icon" class="w-5 h-5" />
              </span>
              <span class="flex-1 min-w-0 flex flex-col">
                <span class="text-[15px] font-semibold text-gray-900 dark:text-gray-100">{{
                  metaOf(id).title
                }}</span>
                <span class="text-xs text-gray-500 dark:text-gray-400">{{ metaOf(id).sub }}</span>
              </span>
              <span
                v-if="isModified(id)"
                class="w-1.5 h-1.5 flex-none rounded-full bg-primary"
                title="Changed from default"
              />
              <UIcon name="i-lucide-chevron-right" class="size-4.5 flex-none text-muted" />
            </button>
          </UCard>
        </div>
      </div>

      <!-- Phone detail bar -->
      <div
        v-if="isMobile && mobileDetail"
        class="sticky top-0 z-20 -mx-6 px-4 py-2 frosted-glass glass-surface border-b border-default flex items-center gap-2"
      >
        <UButton
          icon="i-lucide-chevron-left"
          color="neutral"
          variant="ghost"
          aria-label="Back to all settings"
          @click="void router.push({ path: '/settings' })"
        />
        <span class="text-[15px] font-semibold text-gray-900 dark:text-gray-100">{{
          metaOf(mobileDetail).title
        }}</span>
      </div>

      <!-- Settings Sections -->
      <div class="space-y-6" :class="{ 'settings-mobile-detail': isMobile && mobileDetail }">
        <section
          v-show="!isMobile || mobileDetail === 'appearance'"
          id="appearance"
          class="scroll-mt-4"
        >
          <AppearanceSection :preview-visible="!isMobile || mobileDetail === 'appearance'" />
        </section>
        <section
          v-show="!isMobile || mobileDetail === 'behavior'"
          id="behavior"
          class="scroll-mt-4"
        >
          <BehaviorSection />
        </section>
        <section
          v-if="autoTaggingEnabled"
          v-show="!isMobile || mobileDetail === 'auto-tagging'"
          id="auto-tagging"
          class="scroll-mt-4"
        >
          <AutoTaggingSection />
        </section>
        <section
          v-show="!isMobile || mobileDetail === 'auto-playlists'"
          id="auto-playlists"
          class="scroll-mt-4"
        >
          <AutoPlaylistsSection />
        </section>
        <div v-if="isMobile && mobileDetail">
          <UButton
            block
            color="neutral"
            variant="outline"
            icon="i-lucide-chevron-right"
            @click="goNext()"
          >
            {{
              mobileDetail && nextTarget.id ? `Next: ${nextTarget.label}` : "Back to all settings"
            }}
          </UButton>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.settings-content {
  padding-bottom: calc(1.5rem + var(--preview-dock-h, 0px));
}

.settings-flash {
  border-radius: 0.75rem;
  animation: settings-flash 1.6s ease-out;
}

@keyframes settings-slide-in {
  from {
    opacity: 0;
    transform: translateX(12px);
  }
  to {
    opacity: 1;
    transform: none;
  }
}

@media (max-width: 767px) {
  .settings-search-input :deep(input) {
    font-size: 16px;
  }
  .settings-mobile-detail > section {
    animation: settings-slide-in 200ms ease-out;
  }
}

@media (prefers-reduced-motion: reduce) {
  .settings-mobile-detail > section {
    animation: none;
  }
}

.settings-flash :deep(mark),
mark {
  padding: 0 1px;
  border-radius: 3px;
  background: rgb(var(--ui-primary) / 0.28);
  color: inherit;
}

@keyframes settings-flash {
  0% {
    background: rgb(var(--ui-primary) / 0.25);
    box-shadow: 0 0 0 8px rgb(var(--ui-primary) / 0.25);
  }
  100% {
    background: transparent;
    box-shadow: 0 0 0 8px transparent;
  }
}
</style>
