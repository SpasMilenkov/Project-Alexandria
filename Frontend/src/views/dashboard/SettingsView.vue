<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useMediaQuery } from "@vueuse/core";
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import AppearanceSection from "@/components/dashboard/settings/AppearanceSection.vue";
import AutoPlaylistsSection from "@/components/dashboard/settings/AutoPlaylistsSection.vue";
import AutoTaggingSection from "@/components/dashboard/settings/AutoTaggingSection.vue";
import BehaviorSection from "@/components/dashboard/settings/BehaviorSection.vue";
import ExplorerPreview from "@/components/dashboard/settings/ExplorerPreview.vue";
import SettingsNav, { type SettingsNavItem } from "@/components/dashboard/settings/SettingsNav.vue";
import { useBackgroundImageSync } from "@/composables/useBackgroundImageSync";
import { useSettingsSearch } from "@/composables/useSettingsSearch";
import { useSettingsSync } from "@/composables/useSettingsSync";
import { useSettingsStore } from "@/stores/settings";
import { logger } from "@/utils/logger";

const settingsStore = useSettingsStore();
const { deleteBackgroundImage } = useBackgroundImageSync();
const { isSavingAppearance, isSavingBehavior } = useSettingsSync();
const route = useRoute();
const router = useRouter();

const autoTaggingEnabled = import.meta.env.VITE_AUTOTAGGING_ENABLED === "true";

const isSaving = computed(() => isSavingAppearance.value || isSavingBehavior.value);

const saveStatusLabel = computed(() => {
  if (isSaving.value) return "Saving...";

  return "All changes saved";
});

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

const handleResetAll = async () => {
  if (settingsStore.hasBackgroundImage) {
    try {
      await deleteBackgroundImage();
    } catch (err) {
      logger.error(err);
    }
  }

  settingsStore.resetSettings();
};

const SECTION_IDS = ["appearance", "behavior", "auto-tagging", "auto-playlists"] as const;

type SectionId = (typeof SECTION_IDS)[number];

const isMobile = useMediaQuery("(max-width: 767px)");

// Wide screens get a side column with section navigation and a larger live preview.
const isWide = useMediaQuery("(min-width: 1280px)");
const scrollContainer = ref<HTMLDivElement | null>(null);

const visibleSections = computed<SectionId[]>(() =>
  autoTaggingEnabled ? [...SECTION_IDS] : SECTION_IDS.filter((id) => id !== "auto-tagging"),
);

const scrolledSection = ref<SectionId | null>(null);

const activeSectionId = computed<SectionId>(() => {
  if (scrolledSection.value) return scrolledSection.value;

  const hash = route.hash.slice(1);

  return visibleSections.value.find((id) => id === hash) ?? visibleSections.value[0];
});

// The side panel owns the preview on wide screens; phones only show it on the Appearance page.
const inlinePreviewVisible = computed(() => {
  if (isWide.value) return false;

  return !isMobile.value || mobileDetail.value === "appearance";
});

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

// The active section is the last one whose top has passed a line 30% down the scroller,
// measured from layout on every frame of scrolling. It stays local state: on phones the
// hash means "open this section", so the spy must never write it.
const ACTIVE_LINE_RATIO = 0.3;
const SCROLL_SETTLE_MS = 150;

let navigationTarget: SectionId | null = null;
let settleTimer = 0;
let frame = 0;

const detectSection = () => {
  const container = scrollContainer.value;

  if (!container || navigationTarget) return;

  const ids = visibleSections.value;
  const canScroll = container.scrollHeight > container.clientHeight + 4;
  const atBottom = container.scrollTop + container.clientHeight >= container.scrollHeight - 4;

  // Short trailing sections can never reach the line, so the end of the page selects the last.
  if (canScroll && container.scrollTop > 0 && atBottom) {
    scrolledSection.value = ids[ids.length - 1];
    return;
  }

  const line = container.getBoundingClientRect().top + container.clientHeight * ACTIVE_LINE_RATIO;
  let current = ids[0];

  for (const id of ids) {
    const element = document.getElementById(id);

    // Hidden sections (phone detail view) have no layout box.
    const isLaidOut = element !== null && element.offsetParent !== null;

    if (isLaidOut && element.getBoundingClientRect().top <= line) current = id;
  }

  scrolledSection.value = current;
};

// A navigation holds its target until the smooth scroll stops, so the highlight does not
// flicker through every section passed on the way.
const releaseNavigation = (delay = SCROLL_SETTLE_MS) => {
  window.clearTimeout(settleTimer);

  settleTimer = window.setTimeout(() => {
    navigationTarget = null;
  }, delay);
};

const onSettingsScroll = () => {
  if (navigationTarget) {
    releaseNavigation();
    return;
  }

  if (frame) return;

  frame = window.requestAnimationFrame(() => {
    frame = 0;
    detectSection();
  });
};

const goToSection = (target: string) => {
  const id = visibleSections.value.find((section) => section === target);

  if (!id) return;

  navigationTarget = id;
  scrolledSection.value = id;

  // Covers a target that is already in place, where no scroll event ever fires.
  releaseNavigation(SCROLL_SETTLE_MS * 3);

  void nextTick().then(() => {
    document.getElementById(id)?.scrollIntoView({ behavior: "smooth" });
  });
};

watch(
  () => route.hash,
  (hash) => goToSection(hash.slice(1)),
  { immediate: true },
);

// Expanding or collapsing moves every later section without a scroll event.
watch(
  () => [
    settingsStore.isAppearanceSectionOpen,
    settingsStore.isBehaviorSectionOpen,
    settingsStore.isAutoTaggingSectionOpen,
    settingsStore.isAutoPlaylistsSectionOpen,
  ],
  () => window.setTimeout(detectSection, 400),
);

onMounted(() => {
  scrollContainer.value?.addEventListener("scroll", onSettingsScroll, { passive: true });

  if (!route.hash) detectSection();
});

onBeforeUnmount(() => {
  scrollContainer.value?.removeEventListener("scroll", onSettingsScroll);
  window.clearTimeout(settleTimer);
  window.cancelAnimationFrame(frame);
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

const navItems = computed<SettingsNavItem[]>(() =>
  visibleSections.value.map((id) => ({
    icon: metaOf(id).icon,
    id,
    modified: isModified(id),
    title: metaOf(id).title,
  })),
);

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
  <div class="flex h-full w-full flex-col gap-3 p-3">
    <!-- On phones a section's back bar takes the header's place. -->
    <header
      v-if="isMobile && mobileDetail"
      class="flex shrink-0 items-center gap-2 rounded-xl border border-gray-200/70 px-2 py-2 frosted-glass glass-surface-strong dark:border-gray-700/70"
    >
      <UButton
        icon="i-lucide-chevron-left"
        color="neutral"
        variant="ghost"
        square
        class="justify-center"
        aria-label="Back to all settings"
        @click="void router.push({ path: '/settings' })"
      />
      <h1 class="truncate text-base font-semibold text-gray-900 dark:text-gray-100">
        {{ metaOf(mobileDetail).title }}
      </h1>
    </header>

    <header
      v-else
      class="flex shrink-0 flex-col gap-3 rounded-xl border border-gray-200/70 px-4 py-3 frosted-glass glass-surface-strong md:px-5 dark:border-gray-700/70"
    >
      <div class="flex flex-wrap items-center gap-3 md:flex-nowrap">
        <div class="flex min-w-0 items-center gap-2.5">
          <UIcon
            name="i-lucide-settings"
            class="h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
          />
          <h1 class="truncate text-xl font-semibold text-gray-900 dark:text-gray-100">Settings</h1>
        </div>

        <div
          class="relative order-last w-full md:order-none md:ml-4 md:max-w-md md:flex-1"
          role="search"
        >
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
                square
                class="justify-center"
                aria-label="Clear search"
                @mousedown.prevent="searchQuery = ''"
              />
            </template>
            <template v-else #trailing>
              <kbd
                class="rounded border border-gray-200/70 px-1.5 py-0.5 text-[11px] text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
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
              class="fixed z-[70] max-h-105 overflow-y-auto rounded-xl border border-gray-200 bg-default p-1.5 shadow-lg dark:border-gray-700"
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
                    ? 'bg-gray-100 ring-1 ring-primary dark:bg-neutral-800'
                    : 'hover:bg-gray-100 dark:hover:bg-neutral-800'
                "
                @mousedown.prevent="goToEntry(entry)"
                @mousemove="searchActive = index"
              >
                <span
                  class="text-sm font-medium text-gray-900 dark:text-gray-100"
                  v-html="highlight(entry.title)"
                />
                <span class="truncate text-xs text-gray-600 dark:text-gray-400">{{
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

        <div class="ml-auto flex shrink-0 items-center gap-2">
          <span
            class="inline-flex items-center gap-1.5 text-xs text-gray-600 dark:text-gray-400"
            role="status"
            aria-live="polite"
          >
            <BlockSpinner v-if="isSaving" :size="14" aria-label="Saving" class="text-gray-500" />
            <UIcon
              v-else
              name="i-lucide-circle-check"
              class="size-4 text-green-600 dark:text-green-500"
            />
            <span :class="{ 'sr-only sm:not-sr-only': !isSaving }">{{ saveStatusLabel }}</span>
          </span>

          <div class="hidden h-5 w-px bg-gray-200/70 md:block dark:bg-gray-700/70" />

          <UTooltip :text="allOpen ? 'Collapse all' : 'Expand all'">
            <UButton
              :icon="allOpen ? 'i-lucide-chevrons-down-up' : 'i-lucide-chevrons-up-down'"
              color="neutral"
              variant="ghost"
              size="sm"
              square
              class="hidden justify-center md:inline-flex"
              :aria-label="allOpen ? 'Collapse all' : 'Expand all'"
              :aria-expanded="allOpen"
              @click="toggleAll"
            />
          </UTooltip>

          <UTooltip text="Reset all settings">
            <UButton
              color="error"
              variant="outline"
              size="sm"
              icon="i-heroicons-arrow-path"
              aria-label="Reset all settings"
              @click="handleResetAll"
            >
              <span class="hidden lg:inline">Reset all</span>
            </UButton>
          </UTooltip>
        </div>
      </div>

      <SettingsNav
        v-if="!isMobile && !isWide"
        variant="row"
        :items="navItems"
        :active-id="activeSectionId"
        @navigate="goToSection"
        class="border-t border-gray-200/70 pt-3 dark:border-gray-700/70"
      />
    </header>

    <div class="flex min-h-0 flex-1 gap-3">
      <div ref="scrollContainer" class="min-h-0 min-w-0 flex-1 overflow-y-auto">
        <div class="settings-content flex flex-col gap-3">
          <!-- Phone overview: section list first, one section at a time after -->
          <template v-if="isMobile && !mobileDetail">
            <section v-for="group in mobileGroups" :key="group.label" :aria-label="group.label">
              <h2
                class="mb-2 px-1 text-xs font-medium uppercase tracking-wider text-gray-600 dark:text-gray-400"
              >
                {{ group.label }}
              </h2>

              <ul
                class="divide-y divide-gray-200/70 overflow-hidden rounded-[14px] border border-gray-200/70 frosted-glass glass-surface dark:divide-gray-700/70 dark:border-gray-700/70"
              >
                <li v-for="id in group.ids" :key="id">
                  <button
                    type="button"
                    class="flex w-full items-center gap-3 px-4 py-3 text-left transition-colors active:bg-gray-900/5 dark:active:bg-white/5"
                    @click="void router.push({ hash: `#${id}` })"
                  >
                    <span
                      class="grid h-9 w-9 flex-none place-items-center rounded-lg border border-gray-200/70 text-gray-600 dark:border-gray-700/70 dark:text-gray-400"
                    >
                      <Icon :icon="metaOf(id).icon" class="h-5 w-5" />
                    </span>
                    <span class="flex min-w-0 flex-1 flex-col">
                      <span class="text-sm font-semibold text-gray-900 dark:text-gray-100">
                        {{ metaOf(id).title }}
                      </span>
                      <span class="text-xs text-gray-600 dark:text-gray-400">{{
                        metaOf(id).sub
                      }}</span>
                    </span>
                    <span
                      v-if="isModified(id)"
                      class="h-1.5 w-1.5 flex-none rounded-full bg-primary"
                      title="Changed from default"
                    />
                    <UIcon
                      name="i-lucide-chevron-right"
                      class="size-4.5 flex-none text-gray-400 dark:text-gray-500"
                    />
                  </button>
                </li>
              </ul>
            </section>
          </template>

          <div
            class="flex flex-col gap-3"
            :class="{ 'settings-mobile-detail': isMobile && mobileDetail }"
          >
            <section
              v-show="!isMobile || mobileDetail === 'appearance'"
              id="appearance"
              class="scroll-mt-3"
            >
              <AppearanceSection :preview-visible="inlinePreviewVisible" />
            </section>
            <section
              v-show="!isMobile || mobileDetail === 'behavior'"
              id="behavior"
              class="scroll-mt-3"
            >
              <BehaviorSection />
            </section>
            <section
              v-if="autoTaggingEnabled"
              v-show="!isMobile || mobileDetail === 'auto-tagging'"
              id="auto-tagging"
              class="scroll-mt-3"
            >
              <AutoTaggingSection />
            </section>
            <section
              v-show="!isMobile || mobileDetail === 'auto-playlists'"
              id="auto-playlists"
              class="scroll-mt-3"
            >
              <AutoPlaylistsSection />
            </section>
            <UButton
              v-if="isMobile && mobileDetail"
              block
              color="neutral"
              variant="outline"
              icon="i-lucide-chevron-right"
              @click="goNext()"
            >
              {{ nextTarget.id ? `Next: ${nextTarget.label}` : "Back to all settings" }}
            </UButton>
          </div>
        </div>
      </div>

      <!-- Outside the scroller, so it stays put while the sections scroll. -->
      <aside
        v-if="isWide"
        aria-label="Settings overview"
        class="flex w-88 shrink-0 flex-col gap-3 overflow-y-auto"
      >
        <div
          class="rounded-[14px] border border-gray-200/70 p-2 frosted-glass glass-surface dark:border-gray-700/70"
        >
          <SettingsNav :items="navItems" :active-id="activeSectionId" @navigate="goToSection" />
        </div>

        <section
          aria-labelledby="settings-preview-heading"
          class="rounded-[14px] border border-gray-200/70 p-4 frosted-glass glass-surface dark:border-gray-700/70"
        >
          <div class="mb-4">
            <h2
              id="settings-preview-heading"
              class="text-sm font-semibold text-gray-900 dark:text-gray-100"
            >
              Live preview
            </h2>
            <p class="text-xs text-gray-600 dark:text-gray-400">
              How the explorer looks with your appearance settings.
            </p>
          </div>

          <ExplorerPreview variant="panel" />
        </section>
      </aside>
    </div>
  </div>
</template>

<style scoped>
.settings-content {
  padding-bottom: var(--preview-dock-h, 0px);
}

.settings-flash {
  border-radius: 14px;
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
