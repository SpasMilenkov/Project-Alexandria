import { computed, nextTick, ref, watch } from "vue";

import type { SearchSuggestion } from "@/types/search-suggestion";

import {
  type SearchFilter,
  type SearchFilterKey,
  insertSearchKey,
  isDateFilterKey,
  isMultiValueSearchKey,
  searchFilterOptions,
  suggestSearchKeys,
} from "@/features/explorer/search-filters";

import { useSearchDatePicker } from "./useSearchDatePicker";
import { useSearchPickerOptions } from "./useSearchPickerOptions";
import { type useSearchQuery } from "./useSearchQuery";

const panelTitles: Record<SearchFilterKey, string> = {
  deleted: "Deleted",
  has: "Folders that have",
  in: "Directly inside",
  is: "Show only",
  modified: "Last modified",
  size: "Size, or type >10mb, <1gb or 10mb-1gb",
  tag: "Tagged with",
  type: "Show only",
  uploaded: "Uploaded",
};

// The suggestion list under the search input: filter keys while a word is
// typed, a key's values after `key:`, and the date picker. It also owns the
// edits that come from picking something, since those move focus and decide
// whether the list stays open.
export const useSearchSuggestions = ({
  currentDirectory,
  focusInput,
  query,
}: {
  currentDirectory: () => { id: string; name: string } | null;
  focusInput: (caret?: number) => void;
  query: ReturnType<typeof useSearchQuery>;
}) => {
  const { filters, text, trigger } = query;

  const isFocused = ref(false);
  const isDismissed = ref(false);
  const isFilterListForced = ref(false);
  const highlighted = ref(0);

  // Editing

  // Text set by a pick rather than typed reopens the list and keeps the caret at the end.
  const setText = async (value: string) => {
    text.value = value;
    isDismissed.value = false;
    await nextTick();
    focusInput(value.length);
  };

  const commitFilter = (filter: SearchFilter) => {
    query.addFilter(filter);
    setText(query.textWithoutTrigger());
    isDismissed.value = true;
  };

  const chooseKey = (key: SearchFilterKey) => {
    isFilterListForced.value = false;
    setText(insertSearchKey(text.value, key));
  };

  // Reopens the picker for that key. A single-value chip stays until a new value
  // replaces it; a chip of a key that holds several values is removed so its
  // value is offered again.
  const editFilter = (filter: SearchFilter) => {
    if (isMultiValueSearchKey(filter.key)) {
      query.removeFilter(filter);
    }
    setText(`${query.textForNextToken()}${filter.key}:`);
  };

  const datePicker = useSearchDatePicker(commitFilter);

  // Options

  const { emptyText: emptySuggestionText, filterContext } = useSearchPickerOptions({
    currentDirectory,
    filters,
    today: datePicker.todayDate,
    trigger,
  });

  const keySuggestions = computed(() =>
    suggestSearchKeys(text.value, filterContext.value, isFilterListForced.value),
  );

  const valueSuggestions = computed<SearchSuggestion[]>(() => {
    const active = trigger.value;
    if (!active) {
      return [];
    }

    const items = searchFilterOptions(
      active.key,
      active.query,
      filterContext.value,
    ).map<SearchSuggestion>((option) => ({
      apply: () => commitFilter(option.filter),
      icon: option.icon,
      id: option.id,
      label: option.label,
    }));

    const dateKey = active.key;
    if (isDateFilterKey(dateKey)) {
      items.push({
        apply: () => datePicker.openCalendar(dateKey),
        icon: "i-lucide-calendar-range",
        id: "custom",
        label: "Pick dates…",
      });
    }
    return items;
  });

  const suggestions = computed<SearchSuggestion[]>(() => {
    if (trigger.value) {
      return valueSuggestions.value;
    }
    return keySuggestions.value.map((def) => ({
      apply: () => chooseKey(def.key),
      hint: def.description,
      icon: def.icon,
      id: def.key,
      label: `${def.key}:`,
    }));
  });

  watch(
    () => suggestions.value.map((item) => item.id).join("|"),
    () => {
      highlighted.value = 0;
    },
  );

  // Panel state

  const isPanelOpen = computed(() => {
    if (datePicker.isCalendarOpen.value) {
      return true;
    }
    if (!isFocused.value || isDismissed.value) {
      return false;
    }
    return trigger.value !== null || keySuggestions.value.length > 0;
  });

  const panelTitle = computed(() => {
    const key = datePicker.calendarKey.value ?? trigger.value?.key;
    if (!key) {
      return "Filters";
    }
    return panelTitles[key];
  });

  // The highlighted option, or -1 while no option list is showing.
  const activeSuggestionIndex = computed(() => {
    const showsOptions = isPanelOpen.value && !datePicker.isCalendarOpen.value;
    if (!showsOptions || !suggestions.value[highlighted.value]) {
      return -1;
    }
    return highlighted.value;
  });

  const onInput = () => {
    isDismissed.value = false;
    isFilterListForced.value = false;
  };

  const dismissPanel = () => {
    query.dropTrigger();
    isDismissed.value = true;
    isFilterListForced.value = false;
  };

  const closeCalendar = () => {
    datePicker.closeCalendar();
    focusInput();
  };

  // The filter button keeps the input focused on mousedown, otherwise the blur
  // would close the list just before the click and every press would reopen it.
  const toggleFilterList = () => {
    if (isPanelOpen.value) {
      datePicker.closeCalendar();
      dismissPanel();
      focusInput();
      return;
    }

    isFilterListForced.value = true;
    setText(query.textForNextToken());
  };

  const moveHighlight = (step: number) => {
    const count = suggestions.value.length;
    if (count === 0) {
      return;
    }
    highlighted.value = (highlighted.value + step + count) % count;
  };

  // Handles the keys the list owns and reports whether it used the key, so the
  // caller can hand the rest to the results.
  const handleKey = (event: KeyboardEvent) => {
    if (datePicker.isCalendarOpen.value) {
      if (event.key === "Escape") {
        // The modal closes on an unhandled Escape, so this one only closes the calendar.
        event.preventDefault();
        closeCalendar();
      }
      return true;
    }
    if (!isPanelOpen.value) {
      return false;
    }

    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      moveHighlight(event.key === "ArrowDown" ? 1 : -1);
      return true;
    }
    if (event.key === "Enter") {
      const item = suggestions.value[highlighted.value];
      if (item) {
        event.preventDefault();
        item.apply();
      }
      return true;
    }
    if (event.key === "Escape") {
      // The first Escape only closes the list, the next one closes the modal.
      event.preventDefault();
      dismissPanel();
      return true;
    }
    return false;
  };

  return {
    activeSuggestionIndex,
    applyCalendar: datePicker.applyCalendar,
    calendarRange: datePicker.calendarRange,
    canApplyRange: datePicker.canApplyRange,
    chooseKey,
    closeCalendar,
    editFilter,
    emptySuggestionText,
    handleKey,
    highlighted,
    isCalendarOpen: datePicker.isCalendarOpen,
    isFocused,
    isPanelOpen,
    onInput,
    panelTitle,
    setText,
    suggestions,
    toggleFilterList,
    todayDate: datePicker.todayDate,
  };
};
