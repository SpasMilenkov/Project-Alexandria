<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { breakpointsTailwind, useBreakpoints, useEventListener } from "@vueuse/core";
import { computed, nextTick, onBeforeUnmount, ref, watch } from "vue";

import { glassModalContent } from "@/utils/modalUi";

const emit = defineEmits<{ close: [] }>();

interface Shortcut {
  keys: string[];
  description: string;
}
interface ShortcutSection {
  id: string;
  title: string;
  icon: string;
  shortcuts: Shortcut[];
}
interface TextPart {
  match: boolean;
  text: string;
}

const shortcutSections: ShortcutSection[] = [
  {
    icon: "mdi:folder-outline",
    id: "file-explorer",
    shortcuts: [
      { description: "Copy selected files and folders", keys: ["meta", "C"] },
      { description: "Cut selected files and folders", keys: ["meta", "X"] },
      { description: "Paste copied or cut items", keys: ["meta", "V"] },
      { description: "Delete selected items", keys: ["Delete"] },
      { description: "Rename selected item", keys: ["R"] },
      { description: "Rename selected item", keys: ["F2"] },
      { description: "Download selected items", keys: ["D"] },
      { description: "Create new folder", keys: ["N"] },
      { description: "Open details for selected item", keys: ["alt", "Enter"] },
      { description: "Go back", keys: ["alt", "ArrowLeft"] },
      { description: "Go forward", keys: ["alt", "ArrowRight"] },
      { description: "Clear selection / cancel cut", keys: ["Escape"] },
    ],
    title: "File Explorer",
  },
  {
    icon: "mdi:magnify",
    id: "search",
    shortcuts: [
      { description: "Quick search", keys: ["shift", "K"] },
      { description: "Quick search", keys: ["meta", "/"] },
      { description: "Advanced search", keys: ["shift", "L"] },
    ],
    title: "Search",
  },
  {
    icon: "mdi:tab",
    id: "tabs",
    shortcuts: [
      { description: "Create new tab", keys: ["meta", "shift", "N"] },
      { description: "Close active tab", keys: ["meta", "shift", "Q"] },
    ],
    title: "Tabs",
  },
  {
    icon: "mdi:tag-outline",
    id: "tags",
    shortcuts: [
      { description: "Select all tags", keys: ["meta", "A"] },
      { description: "Clear selection", keys: ["Escape"] },
      { description: "Delete selected tags", keys: ["Delete"] },
    ],
    title: "Tags",
  },
  {
    icon: "mdi:keyboard-outline",
    id: "general",
    shortcuts: [{ description: "Show keyboard shortcuts", keys: ["meta", "K"] }],
    title: "General",
  },
];

const macKeys: Record<string, string> = {
  ArrowLeft: "\u2190",
  ArrowRight: "\u2192",
  Delete: "\u232B",
  Enter: "\u23CE",
  Escape: "Esc",
  alt: "\u2325",
  meta: "\u2318",
  shift: "\u21E7",
};
const windowsKeys: Record<string, string> = {
  ArrowLeft: "←",
  ArrowRight: "→",
  Delete: "Del",
  Enter: "Enter",
  Escape: "Esc",
  alt: "Alt",
  meta: "Ctrl",
  shift: "Shift",
};

const detectMac = () =>
  typeof navigator !== "undefined" && /Mac|iPhone|iPad/.test(navigator.userAgent);

const query = ref("");
const activeSection = ref(shortcutSections[0].id);
const isMac = ref(detectMac());
const contentRef = ref<HTMLElement | null>(null);
const searchInput = ref<{ inputRef: HTMLInputElement | null } | null>(null);

const isMobile = useBreakpoints(breakpointsTailwind).smaller("md");

const normalizedQuery = computed(() => query.value.trim().toLowerCase());
const isFiltering = computed(() => normalizedQuery.value.length > 0);
const totalShortcuts = computed(() =>
  shortcutSections.reduce((sum, s) => sum + s.shortcuts.length, 0),
);

const keyLabel = (key: string) => {
  const map = isMac.value ? macKeys : windowsKeys;
  return map[key] ?? key;
};

const matchesQuery = (shortcut: Shortcut) => {
  const q = normalizedQuery.value;
  const inDescription = shortcut.description.toLowerCase().includes(q);
  const inKeys = shortcut.keys.some(
    (k) => k.toLowerCase().includes(q) || keyLabel(k).toLowerCase().includes(q),
  );
  return inDescription || inKeys;
};

const filteredSections = computed(() => {
  if (!isFiltering.value) {
    return shortcutSections;
  }
  return shortcutSections
    .map((section) => ({ ...section, shortcuts: section.shortcuts.filter(matchesQuery) }))
    .filter((section) => section.shortcuts.length > 0);
});

const platformOptions = computed(() => [
  { icon: "mdi:apple", id: "mac", isActive: isMac.value, label: "Mac" },
  { icon: "mdi:microsoft-windows", id: "windows", isActive: !isMac.value, label: "Windows" },
]);

const setPlatform = (id: string) => {
  isMac.value = id === "mac";
};

const splitMatch = (text: string): TextPart[] => {
  const q = normalizedQuery.value;
  const at = q ? text.toLowerCase().indexOf(q) : -1;
  if (at < 0) {
    return [{ match: false, text }];
  }
  return [
    { match: false, text: text.slice(0, at) },
    { match: true, text: text.slice(at, at + q.length) },
    { match: false, text: text.slice(at + q.length) },
  ];
};

const clearQuery = () => {
  query.value = "";
  searchInput.value?.inputRef?.focus();
};

const scrollToSection = (id: string) => {
  const el = contentRef.value?.querySelector(`#section-${id}`);
  el?.scrollIntoView({ behavior: "smooth", block: "start" });
  activeSection.value = id;
};

// Slash focuses the search field unless the user is already typing somewhere
const handleSlash = (event: KeyboardEvent) => {
  const target = event.target as HTMLElement | null;
  const isTyping =
    target?.tagName === "INPUT" || target?.tagName === "TEXTAREA" || target?.isContentEditable;
  if (event.key !== "/" || isTyping) {
    return;
  }
  event.preventDefault();
  searchInput.value?.inputRef?.focus();
};
useEventListener(window, "keydown", handleSlash);

// Scroll spy: keeps the sidebar highlight in sync with the visible section
let observer: IntersectionObserver | null = null;

const handleIntersect = (entries: IntersectionObserverEntry[]) => {
  entries.forEach((entry) => {
    if (entry.isIntersecting) {
      activeSection.value = entry.target.id.replace("section-", "");
    }
  });
};

const observeSections = () => {
  observer?.disconnect();
  if (!contentRef.value) {
    return;
  }
  observer = new IntersectionObserver(handleIntersect, { root: contentRef.value, threshold: 0.4 });
  contentRef.value
    .querySelectorAll("section[id^='section-']")
    .forEach((el) => observer?.observe(el));
};

watch(contentRef, observeSections, { flush: "post" });
watch(filteredSections, async () => {
  await nextTick();
  observeSections();
});
onBeforeUnmount(() => observer?.disconnect());
</script>

<template>
  <UModal
    :fullscreen="isMobile"
    class="sm:max-w-3xl sm:h-[36rem]"
    :close="{ onClick: () => emit('close') }"
    :ui="{
      header: 'border-b border-gray-200/70 dark:border-gray-700/70',
      body: 'p-0 flex-1 min-h-0 overflow-hidden flex flex-col',
      footer: 'justify-between',
      content: glassModalContent,
    }"
  >
    <template #header>
      <div class="flex flex-col gap-4 flex-1 min-w-0">
        <div class="flex items-center gap-4">
          <div class="p-2 rounded-lg bg-gray-100 dark:bg-gray-800 shrink-0">
            <Icon icon="mdi:keyboard-outline" class="w-5 h-5 text-gray-600 dark:text-gray-400" />
          </div>
          <div class="flex-1 min-w-0">
            <h2 class="text-base font-semibold text-gray-900 dark:text-gray-100 leading-tight">
              Keyboard shortcuts
            </h2>
            <p class="text-xs text-gray-500 dark:text-gray-500 mt-0.5">
              {{ totalShortcuts }} shortcuts in {{ shortcutSections.length }} categories
            </p>
          </div>
          <UButton
            icon="i-heroicons-x-mark"
            size="sm"
            variant="ghost"
            color="neutral"
            aria-label="Close"
            @click="emit('close')"
          />
        </div>

        <UInput
          ref="searchInput"
          v-model="query"
          placeholder="Search by action or key"
          icon="i-lucide-search"
          size="lg"
          class="w-full"
          autofocus
        >
          <template #trailing>
            <UButton
              v-if="isFiltering"
              icon="i-heroicons-x-mark"
              size="xs"
              variant="ghost"
              color="neutral"
              aria-label="Clear search"
              @click="clearQuery"
            />
            <UKbd v-else value="/" size="sm" />
          </template>
        </UInput>
      </div>
    </template>

    <template #body>
      <div class="flex flex-col md:flex-row flex-1 min-h-0">
        <Transition
          enter-active-class="transition-all duration-200 ease-out"
          leave-active-class="transition-all duration-150 ease-in"
          enter-from-class="opacity-0 -translate-x-2"
          leave-to-class="opacity-0 -translate-x-2"
        >
          <nav
            v-if="!isFiltering"
            aria-label="Categories"
            class="shrink-0 flex flex-row md:flex-col gap-2 md:gap-1 p-2 md:p-4 md:w-52 overflow-x-auto md:overflow-y-auto border-b md:border-b-0 md:border-r border-gray-200/70 dark:border-gray-700/70"
          >
            <button
              v-for="section in shortcutSections"
              :key="section.id"
              type="button"
              class="flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium transition-all duration-150 text-left whitespace-nowrap md:w-full"
              :class="
                activeSection === section.id
                  ? 'bg-primary/10 text-primary'
                  : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-white/5 hover:text-gray-900 dark:hover:text-gray-100'
              "
              :aria-current="activeSection === section.id"
              @click="scrollToSection(section.id)"
            >
              <Icon :icon="section.icon" class="w-4 h-4 shrink-0" />
              {{ section.title }}
              <span
                class="ml-auto text-xs font-normal tabular-nums"
                :class="
                  activeSection === section.id ? 'text-primary' : 'text-gray-500 dark:text-gray-500'
                "
              >
                {{ section.shortcuts.length }}
              </span>
            </button>
          </nav>
        </Transition>

        <div ref="contentRef" class="flex-1 min-h-0 overflow-y-auto p-4 md:p-6 space-y-8">
          <div
            v-if="filteredSections.length === 0"
            class="flex flex-col items-center justify-center h-full gap-2 text-center"
          >
            <Icon
              icon="mdi:keyboard-off-outline"
              class="w-12 h-12 text-gray-400 dark:text-gray-600"
            />
            <h3 class="text-base font-semibold text-gray-900 dark:text-gray-100">
              No shortcuts found
            </h3>
            <p class="text-sm text-gray-600 dark:text-gray-400">
              Nothing matches "{{ query.trim() }}". Try an action name or a key.
            </p>
            <UButton class="mt-2" variant="outline" color="neutral" size="sm" @click="clearQuery">
              Clear search
            </UButton>
          </div>

          <section
            v-for="section in filteredSections"
            :id="`section-${section.id}`"
            :key="section.id"
            class="scroll-mt-2"
          >
            <h3
              class="flex items-center gap-2 mb-2 text-sm font-semibold text-gray-900 dark:text-gray-100"
            >
              <Icon :icon="section.icon" class="w-4 h-4 text-gray-500 dark:text-gray-500" />
              {{ section.title }}
            </h3>

            <div
              class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 overflow-hidden divide-y divide-gray-100/80 dark:divide-gray-700/40"
            >
              <div
                v-for="(shortcut, i) in section.shortcuts"
                :key="i"
                class="flex items-center justify-between gap-4 md:gap-6 px-4 py-3 transition-colors hover:bg-gray-50 dark:hover:bg-white/5"
              >
                <span class="text-sm text-gray-900 dark:text-gray-100">
                  <template v-for="(part, pi) in splitMatch(shortcut.description)" :key="pi">
                    <mark
                      v-if="part.match"
                      class="rounded-sm bg-primary/20 px-0.5 text-gray-900 dark:text-gray-100"
                      >{{ part.text }}</mark
                    >
                    <template v-else>{{ part.text }}</template>
                  </template>
                </span>

                <div class="flex items-center gap-1 shrink-0">
                  <template v-for="(key, ki) in shortcut.keys" :key="ki">
                    <UKbd size="md">{{ keyLabel(key) }}</UKbd>
                    <span
                      v-if="ki < shortcut.keys.length - 1"
                      class="text-xs text-gray-500 dark:text-gray-500 select-none"
                      >+</span
                    >
                  </template>
                </div>
              </div>
            </div>
          </section>
        </div>
      </div>
    </template>

    <template #footer>
      <p class="text-xs text-gray-500 dark:text-gray-500">Show keys for</p>
      <div
        class="inline-flex gap-1 p-0.5 rounded-lg bg-gray-100 dark:bg-gray-800"
        role="group"
        aria-label="Platform"
      >
        <UButton
          v-for="option in platformOptions"
          :key="option.id"
          size="xs"
          :variant="option.isActive ? 'subtle' : 'ghost'"
          :color="option.isActive ? 'primary' : 'neutral'"
          :aria-pressed="option.isActive"
          @click="setPlatform(option.id)"
        >
          <Icon :icon="option.icon" class="w-3.5 h-3.5" />
          {{ option.label }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
