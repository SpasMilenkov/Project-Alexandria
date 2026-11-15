import { refDebounced } from "@vueuse/core";
import { computed, ref, shallowRef } from "vue";

import type { SearchChip } from "@/types/search-chip";

import {
  type SearchFilter,
  findSearchTrigger,
  removeSearchFilter,
  searchFilterIcon,
  searchFilterIdentity,
  searchFilterValueLabel,
  searchNameText,
  stripSearchTrigger,
  upsertSearchFilter,
} from "@/features/explorer/search-filters";

const NAME_DEBOUNCE_MS = 200;

// What the user has typed and the filter chips built from it. These are plain
// state changes; moving focus and opening the suggestion list are left to the caller.
export const useSearchQuery = () => {
  const text = ref("");
  // Shallow because CalendarDate's private fields do not survive deep unwrapping.
  const filters = shallowRef<SearchFilter[]>([]);

  const trigger = computed(() => findSearchTrigger(text.value));
  const nameText = computed(() => searchNameText(text.value));
  const debouncedName = refDebounced(nameText, NAME_DEBOUNCE_MS);
  const hasCriteria = computed(() => nameText.value.length > 0 || filters.value.length > 0);

  const chips = computed<SearchChip[]>(() =>
    filters.value.map((filter) => ({
      filter,
      icon: searchFilterIcon(filter.key),
      id: searchFilterIdentity(filter),
      key: filter.key,
      value: searchFilterValueLabel(filter),
    })),
  );

  const placeholder = computed(() => {
    if (filters.value.length > 0) {
      return "Add a name or another filter";
    }
    return "Search by name, or type in: tag: uploaded:";
  });

  // The text before an unfinished `key:`, which is what remains once it is
  // committed or dropped.
  const textWithoutTrigger = () => {
    const active = trigger.value;
    return active ? stripSearchTrigger(text.value, active) : text.value;
  };

  // The same text with a trailing space, so the next word or key starts cleanly.
  const textForNextToken = () => {
    const base = textWithoutTrigger();
    const separator = base && !base.endsWith(" ") ? " " : "";
    return `${base}${separator}`;
  };

  const dropTrigger = () => {
    text.value = textWithoutTrigger();
  };

  const addFilter = (filter: SearchFilter) => {
    filters.value = upsertSearchFilter(filters.value, filter);
  };

  const removeFilter = (filter: SearchFilter) => {
    filters.value = removeSearchFilter(filters.value, filter);
  };

  // Reports whether a chip was there to remove.
  const removeLastFilter = () => {
    const last = filters.value.at(-1);
    if (!last) {
      return false;
    }
    removeFilter(last);
    return true;
  };

  const clearFilters = () => {
    filters.value = [];
  };

  return {
    addFilter,
    chips,
    clearFilters,
    debouncedName,
    dropTrigger,
    filters,
    hasCriteria,
    nameText,
    placeholder,
    removeFilter,
    removeLastFilter,
    text,
    textForNextToken,
    textWithoutTrigger,
    trigger,
  };
};
