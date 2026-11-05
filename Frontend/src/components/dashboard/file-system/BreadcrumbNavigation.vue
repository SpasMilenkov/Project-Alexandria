<script setup lang="ts">
import type { DropdownMenuItem } from "@nuxt/ui";

import { Icon } from "@iconify/vue";
import { computed, ref, watch } from "vue";

import type { NavItem } from "@/types/nav-item";

import { toDropId, useExplorerDragSession } from "@/composables/useExplorerDragSession";
import { glassDrawerContent } from "@/utils/modal-ui";

interface EllipsisItem {
  key: "__ellipsis__";
  label: "…";
  hidden: NavItem[];
}

type VisibleItem = NavItem | EllipsisItem;

const COLLAPSE_THRESHOLD = 6;

const props = defineProps<{
  items: NavItem[];
}>();

const emit = defineEmits<{
  navigate: [key: string | null];
}>();

const isDrawerOpen = ref(false);

watch(
  () => props.items,
  () => {
    isDrawerOpen.value = false;
  },
);

const isEllipsis = (item: VisibleItem): item is EllipsisItem => item.key === "__ellipsis__";

const currentItem = computed(() => props.items[props.items.length - 1]);

const { isDragging } = useExplorerDragSession();

// Compact layouts cannot open the path drawer mid-drag, so a drag shows the
// parent folder as its own drop chip.
const parentItem = computed(() => {
  if (props.items.length < 2) return null;

  return props.items[props.items.length - 2];
});

const visibleItems = computed((): VisibleItem[] => {
  if (props.items.length <= COLLAPSE_THRESHOLD) return props.items;

  const first = props.items[0];
  const tail = props.items.slice(-2);
  const hidden = props.items.slice(1, -2);

  return [first, { hidden, key: "__ellipsis__", label: "…" }, ...tail];
});

const ellipsisMenuItems = (hidden: NavItem[]): DropdownMenuItem[] =>
  hidden.map((item) => ({
    icon: item.icon ?? "i-lucide-folder",
    label: item.label,
    onSelect: () => emit("navigate", item.key),
  }));

const handleNavigate = (key: string | null) => {
  isDrawerOpen.value = false;
  emit("navigate", key);
};
</script>

<template>
  <div class="flex items-center min-w-0 flex-1">
    <div class="flex items-center min-w-0 flex-1 @3xl/explorer:hidden">
      <div
        v-if="isDragging && parentItem"
        class="mr-1 flex shrink-0 items-center gap-1 rounded-md border border-dashed border-gray-300/70 px-2 py-1 text-xs text-gray-600 data-[drop-active=valid]:border-solid data-[drop-active=valid]:border-primary data-[drop-active=valid]:bg-primary/10 dark:border-gray-600/70 dark:text-gray-400"
        :data-drop-dir-id="toDropId(parentItem.key)"
        :data-drop-dir-name="parentItem.label"
        data-drop-kind="breadcrumb"
      >
        <Icon icon="mdi:arrow-up" class="h-3.5 w-3.5 shrink-0" />
        <span class="max-w-24 truncate">{{ parentItem.label }}</span>
      </div>

      <button
        class="flex items-center gap-2 min-w-0 flex-1 px-2 py-1.5 rounded-md hover:bg-gray-500/5 dark:hover:bg-white/5 transition-colors text-left data-[drop-active=valid]:bg-primary/10 data-[drop-active=valid]:ring-1 data-[drop-active=valid]:ring-primary"
        :data-drop-dir-id="toDropId(currentItem?.key ?? null)"
        :data-drop-dir-name="currentItem?.label ?? 'Home'"
        data-drop-kind="breadcrumb"
        @click="isDrawerOpen = true"
      >
        <UIcon name="i-lucide-folder" class="w-4 h-4 text-gray-500 dark:text-gray-400 shrink-0" />
        <span class="text-sm font-semibold truncate text-gray-900 dark:text-gray-100">
          {{ currentItem?.label ?? "Home" }}
        </span>
        <UIcon
          name="i-lucide-chevron-right"
          class="w-4 h-4 text-gray-400 dark:text-gray-500 shrink-0 ml-auto"
        />
      </button>

      <UDrawer
        v-model:open="isDrawerOpen"
        direction="bottom"
        :ui="{
          content: `${glassDrawerContent} rounded-t-2xl border-t border-gray-200/70 dark:border-gray-700/70`,
        }"
      >
        <template #content>
          <div class="px-4 pt-2 pb-3">
            <p
              class="text-xs font-medium uppercase tracking-widest text-gray-400 dark:text-gray-600"
            >
              Navigate to
            </p>
          </div>

          <ul
            class="px-2 space-y-1"
            :style="{ paddingBottom: 'max(1rem, env(safe-area-inset-bottom))' }"
          >
            <li v-for="(item, index) in items" :key="String(item.key)">
              <button
                class="flex items-center gap-4 w-full px-4 py-3.5 rounded-xl min-h-14 transition-colors text-left"
                :class="
                  index === items.length - 1
                    ? 'pointer-events-none'
                    : 'hover:bg-gray-100/60 dark:hover:bg-gray-800/60'
                "
                :disabled="index === items.length - 1"
                @click="handleNavigate(item.key)"
              >
                <div
                  class="flex items-center justify-center w-9 h-9 rounded-xl frosted-glass glass-surface border border-gray-200/70 dark:border-gray-700/70 shrink-0"
                >
                  <UIcon
                    :name="item.icon ?? 'i-lucide-folder'"
                    class="w-5 h-5"
                    :class="
                      index === items.length - 1
                        ? 'text-primary'
                        : 'text-gray-500 dark:text-gray-400'
                    "
                  />
                </div>
                <span
                  class="text-sm"
                  :class="
                    index === items.length - 1
                      ? 'font-medium text-primary'
                      : 'text-gray-800 dark:text-gray-100'
                  "
                >
                  {{ item.label }}
                </span>
              </button>
            </li>
          </ul>
        </template>
      </UDrawer>
    </div>

    <div class="hidden @3xl/explorer:flex items-center gap-0.5 min-w-0 flex-1 overflow-x-auto">
      <template v-for="(item, index) in visibleItems" :key="String(item.key)">
        <UDropdownMenu
          v-if="isEllipsis(item)"
          :items="ellipsisMenuItems(item.hidden)"
          :ui="{ content: 'w-48' }"
        >
          <UButton
            color="neutral"
            variant="ghost"
            size="xs"
            class="shrink-0 rounded-md px-1.5 text-gray-600 dark:text-gray-400"
          >
            …
          </UButton>
        </UDropdownMenu>

        <UButton
          v-else
          :icon="item.icon"
          :label="item.label"
          color="neutral"
          variant="ghost"
          size="sm"
          class="shrink-0 rounded-md px-1.5 data-[drop-active=valid]:bg-primary/10 data-[drop-active=valid]:ring-1 data-[drop-active=valid]:ring-primary"
          :data-drop-dir-id="toDropId(item.key)"
          :data-drop-dir-name="item.label"
          data-drop-kind="breadcrumb"
          :class="
            index === visibleItems.length - 1
              ? 'font-semibold text-gray-900 dark:text-gray-100'
              : 'font-normal text-gray-600 dark:text-gray-300 hover:text-gray-900 dark:hover:text-gray-100'
          "
          @click="handleNavigate(item.key)"
        />

        <span
          v-if="index < visibleItems.length - 1"
          class="mx-0.5 shrink-0 select-none text-xs text-gray-400 dark:text-gray-500"
        >
          /
        </span>
      </template>
    </div>
  </div>
</template>
