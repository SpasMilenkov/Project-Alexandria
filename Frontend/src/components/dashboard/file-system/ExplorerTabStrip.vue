<script setup lang="ts">
import { useResizeObserver } from "@vueuse/core";
import { onScopeDispose, ref, watch } from "vue";

import type { ExplorerTab } from "@/types/explorer-tab";

const {
  tabs,
  activeTabId = null,
  panelId,
} = defineProps<{
  tabs: ExplorerTab[];
  activeTabId?: string | null;
  panelId: string;
}>();

const emit = defineEmits<{
  activate: [id: string];
  close: [id: string];
  closeAll: [];
  create: [];
}>();

const viewport = ref<HTMLElement | null>(null);
const rail = ref<HTMLElement | null>(null);
let frame: number | null = null;
let restoreFocus = false;

const activeButton = () => rail.value?.querySelector<HTMLButtonElement>('[aria-selected="true"]');

const revealActive = () => {
  const container = viewport.value;
  const group = activeButton()?.parentElement;
  if (!container || !group) return;
  const bounds = container.getBoundingClientRect();
  const item = group.getBoundingClientRect();
  const scale = container.offsetWidth ? bounds.width / container.offsetWidth : 1;
  const left = bounds.left + container.clientLeft * scale;
  const right = left + container.clientWidth * scale;
  let delta = 0;
  if (item.left < left) delta = item.left - left;
  else if (item.right > right) delta = item.right - right;
  if (delta) container.scrollLeft += delta / scale;
};

const scheduleReveal = () => {
  if (frame !== null) return;
  frame = requestAnimationFrame(() => {
    frame = null;
    if (restoreFocus && document.activeElement === document.body) {
      activeButton()?.focus({ preventScroll: true });
    }
    restoreFocus = false;
    revealActive();
  });
};

// Inspect focus before Vue removes a closed tab; restore only if that removal loses focus.
watch(
  [() => activeTabId, () => tabs.map((tab) => [tab.id, tab.title])],
  () => {
    const focused = document.activeElement;
    if (focused instanceof HTMLElement && rail.value?.contains(focused)) {
      const id = focused.closest<HTMLElement>("[data-tab-id]")?.dataset.tabId;
      if (id && !tabs.some((tab) => tab.id === id)) restoreFocus = true;
    }
    scheduleReveal();
  },
  { immediate: true },
);

useResizeObserver([viewport, rail], scheduleReveal);

onScopeDispose(() => {
  if (frame !== null) cancelAnimationFrame(frame);
});

const activateWithFocus = (id: string) => {
  const button = [...(rail.value?.querySelectorAll<HTMLButtonElement>('[role="tab"]') ?? [])].find(
    (element) => element.parentElement?.dataset.tabId === id,
  );
  button?.focus({ preventScroll: true });
  emit("activate", id);
  scheduleReveal();
};

const handleKeydown = (event: KeyboardEvent) => {
  if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey) return;
  if (!(event.target instanceof HTMLElement)) return;
  const id = event.target.closest<HTMLElement>("[data-tab-id]")?.dataset.tabId;
  const index = tabs.findIndex((tab) => tab.id === id);
  if (index < 0) return;

  if (event.key === "Delete") {
    event.preventDefault();
    event.stopPropagation();
    if (tabs.length > 1 && id) emit("close", id);
    return;
  }

  const destinations: Record<string, number> = {
    ArrowLeft: (index - 1 + tabs.length) % tabs.length,
    ArrowRight: (index + 1) % tabs.length,
    End: tabs.length - 1,
    Home: 0,
  };
  const destination = destinations[event.key];
  if (destination === undefined) return;
  const tab = tabs[destination];
  if (!tab) return;
  event.preventDefault();
  event.stopPropagation();
  activateWithFocus(tab.id);
};
</script>

<template>
  <div
    data-explorer-tab-strip
    class="sticky top-0 z-10 flex h-14 min-w-0 w-full shrink-0 items-start border-b border-gray-200/70 dark:border-gray-700/70 frosted-glass glass-surface"
  >
    <div class="flex h-10 w-10 shrink-0 items-center justify-center mt-1">
      <UButton
        v-if="tabs.length > 1"
        icon="i-heroicons-folder-minus"
        variant="ghost"
        color="error"
        aria-label="Close all tabs"
        title="Close all tabs"
        @click="emit('closeAll')"
      />
    </div>
    <div
      ref="viewport"
      data-tab-viewport
      role="tablist"
      aria-label="File explorer tabs"
      aria-orientation="horizontal"
      class="h-full flex-1 min-w-0 overflow-x-scroll overflow-y-hidden @container:inline-size"
      @keydown="handleKeydown"
    >
      <div ref="rail" class="mt-1 flex h-10 w-max min-w-full flex-nowrap">
        <div
          v-for="tab in tabs"
          :key="tab.id"
          :data-tab-id="tab.id"
          role="presentation"
          class="flex h-10 w-48 max-w-[100cqw] shrink-0 items-center border-b-2 transition-colors hover:bg-gray-800/10 dark:hover:bg-gray-500/20"
          :class="tab.id === activeTabId ? 'border-primary bg-primary/10' : 'border-transparent'"
        >
          <button
            :id="`${panelId}-tab-${tab.id}`"
            type="button"
            role="tab"
            :aria-selected="tab.id === activeTabId"
            :aria-controls="tab.id === activeTabId ? panelId : undefined"
            :tabindex="tab.id === activeTabId ? 0 : -1"
            :title="tab.title"
            class="flex h-full min-w-0 flex-1 items-center gap-2 px-2 text-sm font-medium outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary"
            :class="tab.id === activeTabId ? 'text-primary' : 'text-gray-600 dark:text-gray-400'"
            @click="emit('activate', tab.id)"
          >
            <UIcon name="i-heroicons-folder" class="size-4 shrink-0" />
            <span class="min-w-0 truncate">{{ tab.title }}</span>
          </button>
          <div class="flex w-8 shrink-0 items-center justify-center">
            <button
              v-if="tabs.length > 1"
              type="button"
              :aria-label="`Close ${tab.title}`"
              :tabindex="tab.id === activeTabId ? 0 : -1"
              title="Close tab"
              class="flex size-8 items-center justify-center rounded text-gray-500 dark:text-gray-400 hover:bg-gray-100/60 dark:hover:bg-gray-800/60 hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary"
              @click="emit('close', tab.id)"
            >
              <UIcon name="i-heroicons-x-mark" class="size-4" />
            </button>
          </div>
        </div>
      </div>
    </div>
    <div class="flex h-10 w-10 shrink-0 items-center justify-center mt-1">
      <UButton
        icon="i-heroicons-plus"
        variant="ghost"
        color="neutral"
        aria-label="New tab"
        title="New tab (⌘⇧N)"
        @click="emit('create')"
      />
    </div>
  </div>
</template>
