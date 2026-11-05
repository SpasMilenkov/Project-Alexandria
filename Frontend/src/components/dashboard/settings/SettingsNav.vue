<template>
  <nav aria-label="Settings sections">
    <ul :class="variant === 'column' ? 'flex flex-col gap-0.5' : 'flex flex-wrap gap-1.5'">
      <li v-for="item in items" :key="item.id">
        <RouterLink
          :to="{ hash: `#${item.id}` }"
          :aria-current="item.id === activeId ? 'location' : undefined"
          :class="linkClass(item.id)"
          @click="emit('navigate', item.id)"
        >
          <Icon
            :icon="item.icon"
            :class="variant === 'column' ? 'h-5 w-5' : 'h-4 w-4'"
            class="shrink-0"
          />
          <span class="min-w-0 flex-1 truncate">{{ item.title }}</span>
          <span
            v-if="item.modified"
            class="h-1.5 w-1.5 shrink-0 rounded-full bg-primary"
            title="Changed from default"
          />
        </RouterLink>
      </li>
    </ul>
  </nav>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";

export interface SettingsNavItem {
  id: string;
  title: string;
  icon: string;
  modified: boolean;
}

const {
  items,
  activeId,
  variant = "column",
} = defineProps<{
  items: SettingsNavItem[];
  activeId: string;
  variant?: "column" | "row";
}>();

// Emitted on every click, including one on the hash already in the URL, which the router
// treats as a no-op.
const emit = defineEmits<{
  navigate: [id: SettingsNavItem["id"]];
}>();

const BASE_CLASSES = {
  column: "flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm",
  row: "flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs",
};

const linkClass = (id: string) => {
  const base = `${BASE_CLASSES[variant]} transition-colors duration-150 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary`;

  if (id === activeId) {
    return `${base} border-primary bg-primary/10 font-medium text-gray-900 dark:text-gray-100`;
  }

  return `${base} border-gray-200/70 text-gray-600 hover:bg-gray-900/5 hover:text-gray-900 dark:border-gray-700/70 dark:text-gray-400 dark:hover:bg-white/5 dark:hover:text-gray-100`;
};
</script>
