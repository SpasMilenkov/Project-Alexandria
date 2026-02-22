<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { ref, computed, onMounted, onUnmounted } from "vue";
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

const shortcutSections: ShortcutSection[] = [
  {
    id: "file-explorer",
    title: "File Explorer",
    icon: "mdi:folder-outline",
    shortcuts: [
      { keys: ["meta", "C"], description: "Copy selected files/folders" },
      { keys: ["meta", "X"], description: "Cut selected files/folders" },
      { keys: ["meta", "V"], description: "Paste copied/cut items" },
      { keys: ["Delete"], description: "Delete selected items" },
    ],
  },
  {
    id: "search",
    title: "Search",
    icon: "mdi:magnify",
    shortcuts: [
      { keys: ["shift", "K"], description: "Quick search" },
      { keys: ["shift", "L"], description: "Advanced search" },
    ],
  },
  {
    id: "tabs",
    title: "Tabs",
    icon: "mdi:tab",
    shortcuts: [
      { keys: ["meta", "shift", "N"], description: "Create new tab" },
      { keys: ["meta", "shift", "Q"], description: "Close active tab" },
    ],
  },
  {
    id: "tags",
    title: "Tags",
    icon: "mdi:tag-outline",
    shortcuts: [
      { keys: ["meta", "A"], description: "Select all tags" },
      { keys: ["Escape"], description: "Clear selection" },
      { keys: ["Delete"], description: "Delete selected tags" },
    ],
  },
];

const query = ref("");
const activeSection = ref(shortcutSections[0].id);

const filteredSections = computed(() => {
  const q = query.value.trim().toLowerCase();
  if (!q) return shortcutSections;
  return shortcutSections
    .map((section) => ({
      ...section,
      shortcuts: section.shortcuts.filter(
        (s) =>
          s.description.toLowerCase().includes(q) ||
          s.keys.some((k) => k.toLowerCase().includes(q)),
      ),
    }))
    .filter((section) => section.shortcuts.length > 0);
});

const isFiltering = computed(() => query.value.trim().length > 0);

function scrollToSection(id: string) {
  const el = document.getElementById(`section-${id}`);
  el?.scrollIntoView({ behavior: "smooth", block: "start" });
  activeSection.value = id;
}

// Intersection observer to track active section while scrolling
onMounted(() => {
  const observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          activeSection.value = entry.target.id.replace("section-", "");
        }
      }
    },
    { threshold: 0.4 },
  );

  shortcutSections.forEach((s) => {
    const el = document.getElementById(`section-${s.id}`);
    if (el) observer.observe(el);
  });

  onUnmounted(() => observer.disconnect());
});
</script>

<template>
  <UModal
    fullscreen
    :close="{ onClick: () => emit('close') }"
    :ui="{
      header: 'border-b border-gray-200/70 dark:border-gray-700/70',
      body: 'p-0 flex-1 overflow-hidden flex flex-col',
    }"
  >
    <template #header>
      <div class="flex items-center gap-3 flex-1 min-w-0">
        <div class="p-1.5 rounded-md bg-primary/10">
          <Icon icon="mdi:keyboard-outline" class="w-5 h-5 text-primary" />
        </div>
        <div class="flex-1 min-w-0">
          <h2 class="text-base font-semibold text-highlighted leading-tight">
            Keyboard Shortcuts
          </h2>
          <p class="text-xs text-muted mt-0.5">
            {{ shortcutSections.reduce((a, s) => a + s.shortcuts.length, 0) }}
            shortcuts across {{ shortcutSections.length }} categories
          </p>
        </div>
        <!-- Search inline in header -->
        <UInput
          v-model="query"
          placeholder="Search shortcuts…"
          icon="i-lucide-search"
          size="sm"
          class="w-56 shrink-0"
          :ui="{ base: 'bg-white/60 dark:bg-white/5 backdrop-blur-sm' }"
        />
      </div>
    </template>

    <template #body>
      <div class="flex flex-1 overflow-hidden h-full">
        <!-- Sidebar -->
        <Transition
          enter-active-class="transition-all duration-200 ease-out"
          leave-active-class="transition-all duration-150 ease-in"
          enter-from-class="opacity-0 -translate-x-2"
          leave-to-class="opacity-0 -translate-x-2"
        >
          <nav
            v-if="!isFiltering"
            class="w-52 shrink-0 border-r border-gray-200/70 dark:border-gray-700/70 bg-white/30 dark:bg-white/3 backdrop-blur-sm flex flex-col gap-1 p-3 overflow-y-auto"
          >
            <p
              class="text-[10px] font-semibold uppercase tracking-widest text-muted px-2 mb-1"
            >
              Categories
            </p>
            <button
              v-for="section in shortcutSections"
              :key="section.id"
              class="flex items-center gap-2.5 px-2.5 py-2 rounded-lg text-sm font-medium transition-all duration-150 text-left w-full"
              :class="
                activeSection === section.id
                  ? 'bg-primary/10 text-primary'
                  : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100/60 dark:hover:bg-white/5 hover:text-highlighted'
              "
              @click="scrollToSection(section.id)"
            >
              <Icon
                :icon="section.icon"
                class="w-4 h-4 shrink-0"
                :class="
                  activeSection === section.id ? 'text-primary' : 'text-muted'
                "
              />
              {{ section.title }}
              <span
                class="ml-auto text-[11px] font-normal tabular-nums"
                :class="
                  activeSection === section.id
                    ? 'text-primary/70'
                    : 'text-muted'
                "
              >
                {{ section.shortcuts.length }}
              </span>
            </button>
          </nav>
        </Transition>

        <!-- Main content -->
        <div class="flex-1 overflow-y-auto p-6 space-y-10">
          <!-- Empty state -->
          <div
            v-if="filteredSections.length === 0"
            class="flex flex-col items-center justify-center h-48 gap-3"
          >
            <Icon
              icon="mdi:keyboard-off-outline"
              class="w-10 h-10 text-muted opacity-40"
            />
            <p class="text-sm text-muted">
              No shortcuts match
              <span class="font-medium text-highlighted">"{{ query }}"</span>
            </p>
          </div>

          <!-- Sections -->
          <section
            v-for="section in filteredSections"
            :id="`section-${section.id}`"
            :key="section.id"
            class="scroll-mt-6"
          >
            <!-- Section heading -->
            <div class="flex items-center gap-2.5 mb-4">
              <div class="p-1.5 rounded-md bg-primary/10 shrink-0">
                <Icon :icon="section.icon" class="w-4 h-4 text-primary" />
              </div>
              <h3 class="text-sm font-semibold text-highlighted">
                {{ section.title }}
              </h3>
              <div
                class="flex-1 h-px bg-gray-200/70 dark:bg-gray-700/50 ml-1"
              />
            </div>

            <!-- Shortcut rows -->
            <div
              class="rounded-xl border border-gray-200/70 dark:border-gray-700/70 bg-white/60 dark:bg-white/5 backdrop-blur-sm overflow-hidden"
            >
              <div
                v-for="(shortcut, i) in section.shortcuts"
                :key="i"
                class="flex items-center justify-between gap-6 px-5 py-3.5 transition-colors hover:bg-primary/5"
                :class="
                  i > 0 && 'border-t border-gray-100/80 dark:border-gray-700/40'
                "
              >
                <!-- Description -->
                <span class="text-sm text-default">{{
                  shortcut.description
                }}</span>

                <!-- Key combo -->
                <div class="flex items-center gap-1 shrink-0">
                  <template v-for="(key, ki) in shortcut.keys" :key="ki">
                    <UKbd :value="key" size="md" />
                    <span
                      v-if="ki < shortcut.keys.length - 1"
                      class="text-xs text-muted font-medium select-none"
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
  </UModal>
</template>
