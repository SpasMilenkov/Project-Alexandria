<script setup lang="ts">
import { computed, ref, watch } from "vue";
import type { DropdownMenuItem } from "@nuxt/ui";

interface NavItem {
  key: string | null;
  label: string;
  icon?: string;
}

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

const currentItem = computed(() => props.items.at(-1));

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
    <div class="flex items-center min-w-0 flex-1 md:hidden">
      <button
        class="flex items-center gap-2 min-w-0 flex-1 px-1 py-1 rounded-md hover:bg-gray-100/60 dark:hover:bg-gray-800/60 transition-colors text-left"
        @click="isDrawerOpen = true"
      >
        <UIcon name="i-lucide-folder" class="w-4 h-4 text-muted shrink-0" />
        <span class="text-sm font-medium truncate text-gray-700 dark:text-gray-200">
          {{ currentItem?.label ?? "Home" }}
        </span>
        <UIcon name="i-lucide-chevron-right" class="w-4 h-4 text-muted shrink-0 ml-auto" />
      </button>

      <UDrawer
        v-model:open="isDrawerOpen"
        direction="bottom"
        class="md:hidden"
        :ui="{
          content: 'rounded-t-2xl border-t border-gray-200/70 dark:border-gray-700/70',
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
                  class="flex items-center justify-center w-9 h-9 rounded-xl bg-white/40 dark:bg-white/5 border border-gray-200/70 dark:border-gray-700/70 shrink-0"
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

    <div class="hidden md:flex items-center min-w-0 flex-1">
      <template v-for="(item, index) in visibleItems" :key="item.key">
        <UDropdownMenu
          v-if="isEllipsis(item)"
          :items="ellipsisMenuItems(item.hidden)"
          :ui="{ content: 'w-48' }"
        >
          <UButton
            color="neutral"
            variant="ghost"
            size="xs"
            class="px-1 text-gray-400 dark:text-gray-500"
          >
            …
          </UButton>
        </UDropdownMenu>

        <UButton
          v-else
          :icon="item.icon"
          :label="item.label"
          color="neutral"
          variant="link"
          size="md"
          class="p-0.5 shrink-0"
          :class="
            index === visibleItems.length - 1
              ? 'font-medium text-gray-700 dark:text-gray-200'
              : 'font-normal text-gray-400 dark:text-gray-500'
          "
          @click="handleNavigate(item.key)"
        />

        <span
          v-if="index < visibleItems.length - 1"
          class="mx-1 text-gray-300 dark:text-gray-600 select-none shrink-0"
        >
          /
        </span>
      </template>
    </div>
  </div>
</template>
