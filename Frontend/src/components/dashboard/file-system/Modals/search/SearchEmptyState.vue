<script setup lang="ts">
import { computed } from "vue";

import { type SearchFilterKey, searchFilterIcon } from "@/features/explorer/search-filters";

type SearchEmptyKind = "conflict" | "idle" | "no-results";

interface EmptyAction {
  id: string;
  label: string;
  run: () => void;
}

const {
  kind,
  query = "",
  hasFilters = false,
} = defineProps<{
  kind: SearchEmptyKind;
  query?: string;
  hasFilters?: boolean;
}>();

const emit = defineEmits<{
  clearFilters: [];
  clearText: [];
  filter: [key: SearchFilterKey];
}>();

// The filters worth meeting first; the rest show up in the filter list.
const STARTER_FILTERS: SearchFilterKey[] = ["in", "tag", "type", "uploaded"];

const COPY: Record<SearchEmptyKind, { description: string; eyebrow: string; icon: string }> = {
  conflict: {
    description: "Some filters only fit files and others only folders. Clear them to start over.",
    eyebrow: "Filters",
    icon: "i-lucide-filter",
  },
  idle: {
    description: "Type a name, or start with a filter.",
    eyebrow: "Search",
    icon: "i-lucide-search",
  },
  "no-results": {
    description: "Check the spelling, try fewer words, or loosen a filter.",
    eyebrow: "No matches",
    icon: "i-lucide-folder-x",
  },
};

const copy = computed(() => COPY[kind]);

const title = computed(() => {
  if (kind === "idle") {
    return "Find files and folders";
  }
  if (kind === "conflict") {
    return "These filters don't combine";
  }
  if (query) {
    return `Nothing matches "${query}"`;
  }
  return "Nothing matches these filters";
});

const actions = computed<EmptyAction[]>(() => {
  const list: EmptyAction[] = [];

  if (kind === "no-results" && query) {
    list.push({ id: "text", label: "Clear search", run: () => emit("clearText") });
  }
  if (kind !== "idle" && hasFilters) {
    list.push({ id: "filters", label: "Clear filters", run: () => emit("clearFilters") });
  }
  return list;
});
</script>

<template>
  <section
    aria-live="polite"
    class="flex h-full flex-col items-center justify-center gap-3 p-6 text-center"
  >
    <span
      class="flex size-12 items-center justify-center rounded-2xl bg-elevated text-gray-900 shadow-sm dark:text-gray-100"
    >
      <UIcon :name="copy.icon" class="size-6" aria-hidden="true" />
    </span>

    <div class="flex flex-col gap-1">
      <p class="text-xs font-semibold uppercase tracking-widest text-gray-600 dark:text-gray-400">
        {{ copy.eyebrow }}
      </p>
      <h4
        class="text-xl font-bold leading-tight tracking-tight wrap-break-word text-gray-900 dark:text-gray-100"
      >
        {{ title }}
      </h4>
      <p class="text-sm leading-relaxed text-gray-600 dark:text-gray-400">
        {{ copy.description }}
      </p>
    </div>

    <div v-if="kind === 'idle'" class="mt-1 flex flex-wrap justify-center gap-2">
      <UButton
        v-for="key in STARTER_FILTERS"
        :key="key"
        :icon="searchFilterIcon(key)"
        color="neutral"
        variant="outline"
        size="sm"
        class="rounded-full px-3"
        @click="emit('filter', key)"
      >
        {{ key }}:
      </UButton>
    </div>

    <div v-else-if="actions.length > 0" class="mt-1 flex flex-wrap justify-center gap-2">
      <UButton
        v-for="(action, index) in actions"
        :key="action.id"
        :color="index === 0 ? 'primary' : 'neutral'"
        :variant="index === 0 ? 'solid' : 'outline'"
        size="sm"
        class="rounded-full px-4"
        @click="action.run"
      >
        {{ action.label }}
      </UButton>
    </div>
  </section>
</template>
