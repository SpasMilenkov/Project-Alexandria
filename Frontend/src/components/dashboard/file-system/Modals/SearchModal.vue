<template>
  <UModal
    title="Search"
    description="Search files and folders by name, narrowed with filters"
    :ui="{ body: 'p-2 sm:p-2', content: modalContent, header: 'hidden' }"
  >
    <template #body>
      <div :class="layoutClass">
        <div class="min-w-0">
          <div
            class="flex flex-wrap items-center gap-1.5 rounded-md bg-default px-3 py-2 ring ring-inset ring-accented focus-within:ring-2 focus-within:ring-primary"
            @click="focusInput"
          >
            <UIcon
              name="i-lucide-search"
              class="w-5 h-5 shrink-0 text-gray-500 dark:text-gray-500"
            />

            <span
              v-for="chip in chips"
              :key="chip.id"
              class="inline-flex max-w-full items-center rounded-full bg-elevated text-sm"
            >
              <button
                type="button"
                class="inline-flex min-w-0 items-center gap-1 rounded-l-full py-0.5 pl-2.5 pr-1 hover:bg-accented"
                :aria-label="`Change filter ${chip.key}: ${chip.value}`"
                @click.stop="editFilter(chip.filter)"
              >
                <UIcon
                  :name="chip.icon"
                  class="w-3.5 h-3.5 shrink-0 text-gray-500 dark:text-gray-500"
                />
                <span class="text-gray-600 dark:text-gray-400">{{ chip.key }}:</span>
                <span class="max-w-40 truncate text-gray-900 dark:text-gray-100">{{
                  chip.value
                }}</span>
              </button>
              <button
                type="button"
                class="inline-flex items-center rounded-r-full py-1 pl-0.5 pr-2 text-gray-500 hover:bg-accented hover:text-gray-900 dark:text-gray-500 dark:hover:text-gray-100"
                :aria-label="`Remove filter ${chip.key}: ${chip.value}`"
                @click.stop="removeFilter(chip.filter)"
              >
                <UIcon name="i-lucide-x" class="w-3.5 h-3.5" />
              </button>
            </span>

            <input
              ref="inputRef"
              v-model="text"
              type="text"
              role="combobox"
              autocomplete="off"
              spellcheck="false"
              aria-label="Search by name or filter"
              :aria-controls="isPanelOpen ? 'search-suggestions' : 'search-results'"
              :aria-expanded="isPanelOpen || hasResults"
              :aria-activedescendant="activeOptionId"
              :placeholder="placeholder"
              class="min-w-32 flex-1 bg-transparent py-1 text-base text-gray-900 outline-none placeholder:text-gray-500 dark:text-gray-100 dark:placeholder:text-gray-500"
              @input="onInput"
              @keydown="onKeydown"
              @focus="isFocused = true"
              @blur="isFocused = false"
            />

            <UButton
              icon="i-lucide-sliders-horizontal"
              size="sm"
              color="neutral"
              :variant="isPanelOpen ? 'subtle' : 'ghost'"
              square
              class="justify-center"
              :aria-label="isPanelOpen ? 'Hide filters' : 'Show filters'"
              :aria-pressed="isPanelOpen"
              @mousedown.prevent
              @click.stop="toggleFilterList"
            />
          </div>

          <div class="relative mt-2 h-[min(28rem,60dvh)]">
            <div class="h-full overflow-y-auto">
              <SearchEmptyState v-if="!hasCriteria" kind="idle" @filter="chooseKey" />

              <SearchEmptyState
                v-else-if="scope === 'none'"
                kind="conflict"
                has-filters
                @clear-filters="clearFilters"
              />

              <BlockSpinner v-else-if="isWaiting" :size="32" />

              <SearchEmptyState
                v-else-if="!hasResults"
                kind="no-results"
                :query="nameText"
                :has-filters="filters.length > 0"
                @clear-filters="clearFilters"
                @clear-text="clearText"
              />

              <div
                v-else
                id="search-results"
                role="listbox"
                aria-label="Search results"
                class="space-y-6 py-4"
              >
                <div v-if="directoryResults.length > 0" role="group" aria-label="Folders">
                  <div class="mb-3 flex items-center gap-2 px-4">
                    <UIcon
                      name="i-lucide-folder"
                      class="w-4 h-4 text-gray-500 dark:text-gray-500"
                    />
                    <h4
                      class="text-xs font-semibold uppercase tracking-wider text-gray-600 dark:text-gray-400"
                    >
                      Folders ({{ directoryCountLabel }})
                    </h4>
                  </div>
                  <div class="space-y-1.5">
                    <div
                      v-for="(dir, index) in directoryResults"
                      :id="resultOptionId(index)"
                      :key="dir.id"
                      role="option"
                      :aria-selected="index === activeResultIndex"
                      @mousemove="setActiveResult(index)"
                    >
                      <DirectoryItem
                        :data="dir"
                        view-mode="list"
                        :is-selected="index === activeResultIndex"
                        @navigate="handleNavigate(dir.id)"
                        @click="handleNavigate(dir.id)"
                        @tooltip-enter="previewDirectory"
                      />
                    </div>
                  </div>
                </div>

                <div v-if="fileResults.length > 0" role="group" aria-label="Files">
                  <div class="mb-3 flex items-center gap-2 px-4">
                    <UIcon name="i-lucide-file" class="w-4 h-4 text-gray-500 dark:text-gray-500" />
                    <h4
                      class="text-xs font-semibold uppercase tracking-wider text-gray-600 dark:text-gray-400"
                    >
                      Files ({{ fileCountLabel }})
                    </h4>
                  </div>
                  <div class="space-y-1.5">
                    <div
                      v-for="(file, index) in fileResults"
                      :id="resultOptionId(directoryResults.length + index)"
                      :key="file.fileId"
                      role="option"
                      :aria-selected="directoryResults.length + index === activeResultIndex"
                      @mousemove="setActiveResult(directoryResults.length + index)"
                    >
                      <FileItem
                        :data="file"
                        :tags="file.tags"
                        view-mode="list"
                        :is-selected="directoryResults.length + index === activeResultIndex"
                        @click="handleNavigate(file.directoryId)"
                        @tooltip-enter="previewFile"
                      />
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Covers the results in place so closing the menu brings them back where they were. -->
            <Transition
              enter-active-class="transition-all duration-200 ease-out motion-reduce:transition-none"
              enter-from-class="-translate-y-1 opacity-0"
              leave-active-class="transition-all duration-150 ease-in motion-reduce:transition-none"
              leave-to-class="-translate-y-1 opacity-0"
            >
              <div
                v-if="isPanelOpen"
                id="search-suggestions"
                role="listbox"
                :aria-label="panelTitle"
                class="absolute inset-0 overflow-y-auto bg-default p-1"
                @mousedown.prevent
                @keydown.esc="onPanelEscape"
              >
                <p class="px-2 pb-1 pt-1.5 text-xs font-medium text-gray-600 dark:text-gray-400">
                  {{ panelTitle }}
                </p>

                <div v-if="calendarKey" class="space-y-2 p-2">
                  <UCalendar v-model="calendarRange" range :max-value="todayDate" />
                  <div class="flex justify-end gap-2">
                    <UButton size="sm" color="neutral" variant="outline" @click="closeCalendar">
                      Cancel
                    </UButton>
                    <UButton
                      size="sm"
                      color="primary"
                      :disabled="!canApplyRange"
                      @click="applyCalendar"
                    >
                      Apply
                    </UButton>
                  </div>
                </div>

                <template v-else>
                  <button
                    v-for="(item, index) in suggestions"
                    :id="`search-option-${index}`"
                    :key="item.id"
                    type="button"
                    role="option"
                    :aria-selected="index === highlighted"
                    class="flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm"
                    :class="index === highlighted ? 'bg-elevated' : ''"
                    @mouseenter="highlighted = index"
                    @click="item.apply()"
                  >
                    <UIcon
                      :name="item.icon"
                      class="w-4 h-4 shrink-0 text-gray-500 dark:text-gray-500"
                    />
                    <span class="truncate font-medium text-gray-900 dark:text-gray-100">
                      {{ item.label }}
                    </span>
                    <span
                      v-if="item.hint"
                      class="ml-auto shrink-0 pl-2 text-xs text-gray-500 dark:text-gray-500"
                    >
                      {{ item.hint }}
                    </span>
                  </button>

                  <p
                    v-if="suggestions.length === 0"
                    class="px-2 py-1.5 text-sm text-gray-600 dark:text-gray-400"
                  >
                    {{ emptySuggestionText }}
                  </p>
                </template>
              </div>
            </Transition>
          </div>
        </div>

        <!--
          The content sits out of flow at its full width, so a tall preview never
          stretches the modal and nothing inside reflows while the column opens.
        -->
        <div
          v-if="canPreview"
          class="relative overflow-hidden border-l transition-[border-color] duration-200 ease-[cubic-bezier(0.2,0,0,1)] motion-reduce:transition-none"
          :class="paneEdgeClass"
          :inert="!isPreviewOpen"
        >
          <div
            class="absolute inset-y-0 left-0 w-[21rem] overflow-y-auto pl-3 transition-[opacity,translate] ease-out motion-reduce:transition-none"
            :class="paneContentClass"
          >
            <SearchResultPreview v-if="shownPreview" :target="shownPreview" />
          </div>
        </div>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import {
  type CalendarDate,
  type DateValue,
  getLocalTimeZone,
  toCalendarDate,
  today,
} from "@internationalized/date";
import { useQuery } from "@pinia/colada";
import { refDebounced } from "@vueuse/core";
import { computed, nextTick, ref, shallowRef, watch } from "vue";

import type { SearchPreviewTarget } from "@/types/search-preview-target";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import SearchEmptyState from "@/components/dashboard/file-system/SearchEmptyState.vue";
import SearchResultPreview from "@/components/dashboard/file-system/SearchResultPreview.vue";
import { useListNavigation } from "@/composables/useListNavigation";
import { useModalBackGuard } from "@/composables/useModalBackGuard";
import { useSearchPreview } from "@/composables/useSearchPreview";
import {
  type DateFilterKey,
  type SearchFilter,
  type SearchFilterContext,
  type SearchFilterKey,
  buildSearchRequests,
  findSearchTrigger,
  insertSearchKey,
  isDateFilterKey,
  isMultiValueSearchKey,
  removeSearchFilter,
  resolveSearchScope,
  searchFilterIcon,
  searchFilterIdentity,
  searchFilterOptions,
  searchFilterValueLabel,
  searchNameText,
  stripSearchTrigger,
  suggestSearchKeys,
  upsertSearchFilter,
} from "@/features/explorer/search-filters";
import { searchDirectory } from "@/queries/directories";
import { searchFile } from "@/queries/files";
import { searchTag } from "@/queries/tags";
import { directorySearchApiSchema, fileSearchApiSchema } from "@/schemas/search";

interface SuggestionItem {
  apply: () => void;
  hint?: string;
  icon: string;
  id: string;
  label: string;
}

interface CalendarRange {
  end: DateValue | undefined;
  start: DateValue | undefined;
}

const { currentDirectoryId = null, currentDirectoryName = "" } = defineProps<{
  currentDirectoryId?: string | null;
  currentDirectoryName?: string;
}>();

const emit = defineEmits<{ close: [string | "root" | "close"] }>();

useModalBackGuard(() => emit("close", "close"));

const PAGE_SIZE = 20;
const PICKER_SUGGESTION_COUNT = 8;

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

const emptySuggestionTexts: Partial<Record<SearchFilterKey, string>> = {
  in: "No folders match",
  size: "Type a size like >10mb, <1gb or 10mb-1gb",
  tag: "No tags match",
};

const emptyFileQuery = fileSearchApiSchema.parse({});
const emptyDirectoryQuery = directorySearchApiSchema.parse({});

const inputRef = ref<HTMLInputElement | null>(null);
const text = ref("");
// Shallow because CalendarDate's private fields do not survive deep unwrapping.
const filters = shallowRef<SearchFilter[]>([]);
const isFocused = ref(false);
const isDismissed = ref(false);
const isFilterListForced = ref(false);
const highlighted = ref(0);
const calendarKey = ref<DateFilterKey | null>(null);
const calendarRange = shallowRef<CalendarRange | null>(null);
const todayDate = today(getLocalTimeZone());

const trigger = computed(() => findSearchTrigger(text.value));
const nameText = computed(() => searchNameText(text.value));
const debouncedName = refDebounced(nameText, 200);
const pickerQuery = computed(() => trigger.value?.query.trim() ?? "");
const debouncedPickerQuery = refDebounced(pickerQuery, 200);

const currentDirectory = computed(() => {
  if (!currentDirectoryId || !currentDirectoryName) {
    return null;
  }
  return { id: currentDirectoryId, name: currentDirectoryName };
});

const placeholder = computed(() => {
  if (filters.value.length > 0) {
    return "Add a name or another filter";
  }
  return "Search by name, or type in: tag: uploaded:";
});

// Results

const scope = computed(() => resolveSearchScope(filters.value));
const requests = computed(() => buildSearchRequests(debouncedName.value, filters.value, PAGE_SIZE));
const hasCriteria = computed(() => nameText.value.length > 0 || filters.value.length > 0);

const filesQuery = useQuery(() => ({
  ...searchFile(requests.value.files ?? emptyFileQuery),
  enabled: requests.value.files !== null,
  placeholderData: (prev) => prev,
}));

const directoriesQuery = useQuery(() => ({
  ...searchDirectory(requests.value.directories ?? emptyDirectoryQuery),
  enabled: requests.value.directories !== null,
  placeholderData: (prev) => prev,
}));

const fileResults = computed(() => {
  if (!requests.value.files) {
    return [];
  }
  return filesQuery.data.value?.items ?? [];
});

const directoryResults = computed(() => {
  if (!requests.value.directories) {
    return [];
  }
  return directoriesQuery.data.value?.items ?? [];
});

const hasResults = computed(() => fileResults.value.length + directoryResults.value.length > 0);

const isWaiting = computed(() => {
  const filesPending = requests.value.files !== null && filesQuery.isPending.value;
  const directoriesPending =
    requests.value.directories !== null && directoriesQuery.isPending.value;
  return !hasResults.value && (filesPending || directoriesPending);
});

const countLabel = (shown: number, total: number | undefined) => {
  if (total === undefined || total <= shown) {
    return `${shown}`;
  }
  return `${shown} of ${total}`;
};

const fileCountLabel = computed(() =>
  countLabel(fileResults.value.length, filesQuery.data.value?.totalCount),
);

const directoryCountLabel = computed(() =>
  countLabel(directoryResults.value.length, directoriesQuery.data.value?.totalCount),
);

// Preview

const {
  canPreview,
  isPreviewOpen,
  layoutClass,
  modalContent,
  paneContentClass,
  paneEdgeClass,
  previewDirectory,
  previewFile,
  shownPreview,
} = useSearchPreview(fileResults, directoryResults);

// Results, in the order they are listed, for moving through them from the input.
const results = computed<SearchPreviewTarget[]>(() => [
  ...directoryResults.value.map((directory) => ({ directory, kind: "directory" as const })),
  ...fileResults.value.map((file) => ({ file, kind: "file" as const })),
]);

const resultKey = (result: SearchPreviewTarget) =>
  result.kind === "file" ? `file:${result.file.fileId}` : `folder:${result.directory.id}`;

const {
  activeIndex: activeResultIndex,
  activeItem: activeResult,
  handleKey: handleResultKey,
  setActive: setActiveResult,
} = useListNavigation(() => results.value, resultKey);

const resultOptionId = (index: number) => `search-result-${index}`;

const openResult = (result: SearchPreviewTarget) => {
  if (result.kind === "file") {
    handleNavigate(result.file.directoryId);
  } else {
    handleNavigate(result.directory.id);
  }
};

watch(activeResult, async (result) => {
  if (!result) {
    return;
  }
  if (result.kind === "file") {
    previewFile(result.file);
  } else {
    previewDirectory(result.directory);
  }

  await nextTick();
  document
    .getElementById(resultOptionId(activeResultIndex.value))
    ?.scrollIntoView({ block: "nearest" });
});

// Suggestions

const folderSuggestionsQuery = useQuery(() => ({
  ...searchDirectory({
    isDeleted: false,
    nameContains: debouncedPickerQuery.value || null,
    pageSize: PICKER_SUGGESTION_COUNT,
  }),
  enabled: trigger.value?.key === "in",
  placeholderData: (prev) => prev,
}));

const tagSuggestionsQuery = useQuery(() => ({
  ...searchTag({
    nameContains: debouncedPickerQuery.value || null,
    ownerScope: "all",
    // Tag search pages from 1, unlike the file and folder searches.
    page: 1,
    pageSize: PICKER_SUGGESTION_COUNT,
  }),
  enabled: trigger.value?.key === "tag",
  placeholderData: (prev) => prev,
}));

const filterContext = computed<SearchFilterContext>(() => ({
  currentDirectory: currentDirectory.value,
  filters: filters.value,
  folders: folderSuggestionsQuery.data.value?.items.map((dir) => ({ id: dir.id, name: dir.name })),
  tags: tagSuggestionsQuery.data.value?.items.map((tag) => ({ id: tag.id, name: tag.name })),
  today: todayDate,
}));

const keySuggestions = computed(() =>
  suggestSearchKeys(text.value, filterContext.value, isFilterListForced.value),
);

const valueSuggestions = computed<SuggestionItem[]>(() => {
  const active = trigger.value;
  if (!active) {
    return [];
  }

  const items = searchFilterOptions(
    active.key,
    active.query,
    filterContext.value,
  ).map<SuggestionItem>((option) => ({
    apply: () => commitFilter(option.filter),
    icon: option.icon,
    id: option.id,
    label: option.label,
  }));

  const dateKey = active.key;
  if (isDateFilterKey(dateKey)) {
    items.push({
      apply: () => openCalendar(dateKey),
      icon: "i-lucide-calendar-range",
      id: "custom",
      label: "Pick dates…",
    });
  }
  return items;
});

const suggestions = computed<SuggestionItem[]>(() => {
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

const isPanelOpen = computed(() => {
  if (calendarKey.value) {
    return true;
  }
  if (!isFocused.value || isDismissed.value) {
    return false;
  }
  return trigger.value !== null || keySuggestions.value.length > 0;
});

const panelTitle = computed(() => {
  const key = calendarKey.value ?? trigger.value?.key;
  if (!key) {
    return "Filters";
  }
  return panelTitles[key];
});

const emptySuggestionText = computed(() => {
  const key = trigger.value?.key;
  if (key === "in" && folderSuggestionsQuery.isPending.value) {
    return "Searching folders";
  }
  if (key === "tag" && tagSuggestionsQuery.isPending.value) {
    return "Searching tags";
  }
  if (key && emptySuggestionTexts[key]) {
    return emptySuggestionTexts[key];
  }
  return "No matching options";
});

const activeOptionId = computed(() => {
  if (!isPanelOpen.value) {
    return activeResultIndex.value === -1 ? undefined : resultOptionId(activeResultIndex.value);
  }
  if (calendarKey.value || !suggestions.value[highlighted.value]) {
    return undefined;
  }
  return `search-option-${highlighted.value}`;
});

const chips = computed(() =>
  filters.value.map((filter) => ({
    filter,
    icon: searchFilterIcon(filter.key),
    id: searchFilterIdentity(filter),
    key: filter.key,
    value: searchFilterValueLabel(filter),
  })),
);

const canApplyRange = computed(() =>
  Boolean(calendarRange.value?.start && calendarRange.value?.end),
);

watch(
  () => suggestions.value.map((item) => item.id).join("|"),
  () => {
    highlighted.value = 0;
  },
);

// Actions

const focusInput = () => {
  inputRef.value?.focus();
};

const onInput = () => {
  isDismissed.value = false;
  isFilterListForced.value = false;
};

const setText = async (value: string) => {
  text.value = value;
  isDismissed.value = false;
  await nextTick();
  focusInput();
  inputRef.value?.setSelectionRange(value.length, value.length);
};

const chooseKey = (key: SearchFilterKey) => {
  isFilterListForced.value = false;
  setText(insertSearchKey(text.value, key));
};

const commitFilter = (filter: SearchFilter) => {
  filters.value = upsertSearchFilter(filters.value, filter);
  const active = trigger.value;
  setText(active ? stripSearchTrigger(text.value, active) : text.value);
  isDismissed.value = true;
};

const clearFilters = () => {
  filters.value = [];
  focusInput();
};

// Keeps the filter chips and drops only what was typed.
const clearText = () => {
  setText("");
};

const removeFilter = (filter: SearchFilter) => {
  filters.value = removeSearchFilter(filters.value, filter);
  focusInput();
};

// Reopens the picker for that key. A single-value chip stays until a new value
// replaces it; a chip of a key that holds several values is removed so its
// value is offered again.
const editFilter = (filter: SearchFilter) => {
  if (isMultiValueSearchKey(filter.key)) {
    filters.value = removeSearchFilter(filters.value, filter);
  }

  const active = trigger.value;
  const base = active ? stripSearchTrigger(text.value, active) : text.value;
  const separator = base && !base.endsWith(" ") ? " " : "";
  setText(`${base}${separator}${filter.key}:`);
};

// The button keeps the input focused on mousedown, otherwise the blur would
// close the list just before the click and every press would reopen it.
const toggleFilterList = () => {
  if (isPanelOpen.value) {
    if (calendarKey.value) {
      closeCalendar();
    }
    dismissPanel();
    focusInput();
    return;
  }

  const active = trigger.value;
  const base = active ? stripSearchTrigger(text.value, active) : text.value;
  const separator = base && !base.endsWith(" ") ? " " : "";
  isFilterListForced.value = true;
  setText(`${base}${separator}`);
};

const openCalendar = (key: DateFilterKey) => {
  calendarKey.value = key;
  calendarRange.value = null;
};

const closeCalendar = () => {
  calendarKey.value = null;
  calendarRange.value = null;
  focusInput();
};

const formatRange = (from: CalendarDate, to: CalendarDate) => {
  const formatter = new Intl.DateTimeFormat(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
  const zone = getLocalTimeZone();
  if (from.compare(to) === 0) {
    return formatter.format(from.toDate(zone));
  }
  return formatter.formatRange(from.toDate(zone), to.toDate(zone));
};

const applyCalendar = () => {
  const key = calendarKey.value;
  const start = calendarRange.value?.start;
  const end = calendarRange.value?.end;
  if (!key || !start || !end) {
    return;
  }

  const from = toCalendarDate(start);
  const to = toCalendarDate(end);
  calendarKey.value = null;
  calendarRange.value = null;
  commitFilter({ key, label: formatRange(from, to), range: { from, to } });
};

// Keyboard focus can sit inside the calendar, away from the input's handler.
const onPanelEscape = (event: KeyboardEvent) => {
  if (!calendarKey.value || event.key !== "Escape") {
    return;
  }
  event.preventDefault();
  closeCalendar();
};

const dismissPanel = () => {
  const active = trigger.value;
  if (active) {
    text.value = stripSearchTrigger(text.value, active);
  }
  isDismissed.value = true;
  isFilterListForced.value = false;
};

const moveHighlight = (step: number) => {
  const count = suggestions.value.length;
  if (count === 0) {
    return;
  }
  highlighted.value = (highlighted.value + step + count) % count;
};

const removeLastFilter = (event: KeyboardEvent) => {
  const input = inputRef.value;
  const atStart = input?.selectionStart === 0 && input.selectionEnd === 0;
  const last = filters.value.at(-1);
  if (!atStart || !last) {
    return;
  }
  event.preventDefault();
  filters.value = removeSearchFilter(filters.value, last);
};

const onKeydown = (event: KeyboardEvent) => {
  if (event.key === "Backspace") {
    removeLastFilter(event);
    return;
  }
  if (calendarKey.value) {
    onPanelEscape(event);
    return;
  }
  if (!isPanelOpen.value) {
    handleResultKey(event, openResult);
    return;
  }

  if (event.key === "ArrowDown" || event.key === "ArrowUp") {
    event.preventDefault();
    moveHighlight(event.key === "ArrowDown" ? 1 : -1);
  } else if (event.key === "Enter") {
    const item = suggestions.value[highlighted.value];
    if (item) {
      event.preventDefault();
      item.apply();
    }
  } else if (event.key === "Escape") {
    // The modal closes on Escape unless the event is already handled, so the
    // first press only closes the suggestions.
    event.preventDefault();
    dismissPanel();
  }
};

const handleNavigate = (id: string | null) => emit("close", id ?? "root");
</script>
