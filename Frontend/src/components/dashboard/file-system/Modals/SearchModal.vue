<template>
  <UModal
    title="Search"
    description="Search files and folders by name and documents by their text, narrowed with filters"
    :ui="{ body: 'p-2 sm:p-2', content: modalContent, header: 'hidden' }"
  >
    <template #body>
      <div :class="layoutClass">
        <div class="min-w-0">
          <SearchChipsInput
            ref="inputRef"
            v-model="text"
            :chips="chips"
            :placeholder="placeholder"
            :filters-open="isPanelOpen"
            :listbox-id="isPanelOpen ? SUGGESTIONS_ID : RESULTS_ID"
            :expanded="isPanelOpen || hasResults"
            :active-descendant="activeDescendant"
            @typed="onInput"
            @keydown="onKeydown"
            @focus-change="isFocused = $event"
            @edit-chip="editFilter"
            @remove-chip="removeChip"
            @toggle-filters="toggleFilterList"
          />

          <div class="relative mt-2 h-[min(28rem,60dvh)]">
            <div class="h-full overflow-y-auto">
              <SearchEmptyState v-if="!hasCriteria" kind="idle" @filter="chooseKey" />

              <SearchEmptyState
                v-else-if="scope === 'none'"
                kind="conflict"
                has-filters
                @clear-filters="clearChips"
              />

              <div
                v-else-if="isWaiting"
                class="flex h-full items-center justify-center text-gray-500 dark:text-gray-400"
              >
                <BlockSpinner :size="32" />
              </div>

              <SearchEmptyState
                v-else-if="!hasResults"
                kind="no-results"
                :query="nameText"
                :has-filters="filters.length > 0"
                @clear-filters="clearChips"
                @clear-text="setText('')"
              />

              <SearchResultsList
                v-else
                :id="RESULTS_ID"
                :option-id-prefix="RESULT_OPTION_PREFIX"
                :directories="directoryResults"
                :files="fileResults"
                :active-index="activeResultIndex"
                :directory-count-label="directoryCountLabel"
                :file-count-label="fileCountLabel"
                :query="debouncedName"
                @activate="setActiveResult"
                @open="openResult"
                @preview="preview"
              />
            </div>

            <!-- Covers the results in place so closing the menu brings them back where they were. -->
            <Transition
              enter-active-class="transition-all duration-200 ease-out motion-reduce:transition-none"
              enter-from-class="-translate-y-1 opacity-0"
              leave-active-class="transition-all duration-150 ease-in motion-reduce:transition-none"
              leave-to-class="-translate-y-1 opacity-0"
            >
              <SearchSuggestionsPanel
                v-if="isPanelOpen"
                :id="SUGGESTIONS_ID"
                v-model:highlighted="highlighted"
                v-model:range="calendarRange"
                class="absolute inset-0"
                :option-id-prefix="SUGGESTION_OPTION_PREFIX"
                :title="panelTitle"
                :items="suggestions"
                :empty-text="emptySuggestionText"
                :calendar-open="isCalendarOpen"
                :can-apply="canApplyRange"
                :max-date="todayDate"
                @apply-calendar="applyCalendar"
                @cancel-calendar="closeCalendar"
              />
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
            <SearchResultPreview
              v-if="shownPreview"
              :target="shownPreview"
              :query="debouncedName"
            />
          </div>
        </div>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { computed, nextTick, useTemplateRef, watch } from "vue";

import type { SearchFilter } from "@/features/explorer/search-filters";
import type { SearchPreviewTarget } from "@/types/search-preview-target";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import SearchChipsInput from "@/components/dashboard/file-system/Modals/search/SearchChipsInput.vue";
import SearchEmptyState from "@/components/dashboard/file-system/Modals/search/SearchEmptyState.vue";
import SearchResultPreview from "@/components/dashboard/file-system/Modals/search/SearchResultPreview.vue";
import SearchResultsList from "@/components/dashboard/file-system/Modals/search/SearchResultsList.vue";
import SearchSuggestionsPanel from "@/components/dashboard/file-system/Modals/search/SearchSuggestionsPanel.vue";
import { useSearchPreview } from "@/composables/search/useSearchPreview";
import { useSearchQuery } from "@/composables/search/useSearchQuery";
import { useSearchResults } from "@/composables/search/useSearchResults";
import { useSearchSuggestions } from "@/composables/search/useSearchSuggestions";
import { useListNavigation } from "@/composables/useListNavigation";
import { useModalBackGuard } from "@/composables/useModalBackGuard";

// The input points screen readers at the highlighted row by id, so the lists
// and the input share these.
const SUGGESTIONS_ID = "search-suggestions";
const SUGGESTION_OPTION_PREFIX = "search-option";
const RESULTS_ID = "search-results";
const RESULT_OPTION_PREFIX = "search-result";

const { currentDirectoryId = null, currentDirectoryName = "" } = defineProps<{
  currentDirectoryId?: string | null;
  currentDirectoryName?: string;
}>();

const emit = defineEmits<{ close: [string | "root" | "close"] }>();

useModalBackGuard(() => emit("close", "close"));

const input = useTemplateRef<InstanceType<typeof SearchChipsInput>>("inputRef");

const focusInput = (caret?: number) => {
  input.value?.focus(caret);
};

// Query and suggestions

const query = useSearchQuery();
const { chips, debouncedName, filters, hasCriteria, nameText, placeholder, text } = query;

const {
  activeSuggestionIndex,
  applyCalendar,
  calendarRange,
  canApplyRange,
  chooseKey,
  closeCalendar,
  editFilter,
  emptySuggestionText,
  handleKey: handleSuggestionKey,
  highlighted,
  isCalendarOpen,
  isFocused,
  isPanelOpen,
  onInput,
  panelTitle,
  setText,
  suggestions,
  toggleFilterList,
  todayDate,
} = useSearchSuggestions({
  currentDirectory: () => {
    if (!currentDirectoryId || !currentDirectoryName) {
      return null;
    }
    return { id: currentDirectoryId, name: currentDirectoryName };
  },
  focusInput,
  query,
});

const removeChip = (filter: SearchFilter) => {
  query.removeFilter(filter);
  focusInput();
};

const clearChips = () => {
  query.clearFilters();
  focusInput();
};

// Results, preview and moving through them

const {
  directoryCountLabel,
  directoryResults,
  fileCountLabel,
  fileResults,
  hasResults,
  isWaiting,
  resultKey,
  results,
  scope,
} = useSearchResults(debouncedName, filters);

const {
  canPreview,
  isPreviewOpen,
  layoutClass,
  modalContent,
  paneContentClass,
  paneEdgeClass,
  preview,
  shownPreview,
} = useSearchPreview(fileResults, directoryResults);

const {
  activeIndex: activeResultIndex,
  activeItem: activeResult,
  handleKey: handleResultKey,
  setActive: setActiveResult,
} = useListNavigation(() => results.value, resultKey);

const handleNavigate = (id: string | null) => emit("close", id ?? "root");

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
  preview(result);

  await nextTick();
  document
    .getElementById(`${RESULT_OPTION_PREFIX}-${activeResultIndex.value}`)
    ?.scrollIntoView({ block: "nearest" });
});

// Keyboard

const activeDescendant = computed(() => {
  if (isPanelOpen.value) {
    const index = activeSuggestionIndex.value;
    return index === -1 ? undefined : `${SUGGESTION_OPTION_PREFIX}-${index}`;
  }
  const index = activeResultIndex.value;
  return index === -1 ? undefined : `${RESULT_OPTION_PREFIX}-${index}`;
});

// Backspace on an empty spot removes the last chip; the suggestion list takes
// its keys while open, and the results get them otherwise.
const onKeydown = (event: KeyboardEvent) => {
  if (event.key === "Backspace") {
    if (input.value?.isCaretAtStart() && query.removeLastFilter()) {
      event.preventDefault();
    }
    return;
  }
  if (handleSuggestionKey(event)) {
    return;
  }
  if (!isPanelOpen.value) {
    handleResultKey(event, openResult);
  }
};
</script>
