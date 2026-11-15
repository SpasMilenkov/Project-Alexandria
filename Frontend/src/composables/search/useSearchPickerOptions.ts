import type { CalendarDate } from "@internationalized/date";

import { useQuery } from "@pinia/colada";
import { refDebounced } from "@vueuse/core";
import { type Ref, computed } from "vue";

import type {
  SearchFilter,
  SearchFilterContext,
  SearchFilterKey,
  SearchTrigger,
} from "@/features/explorer/search-filters";

import { searchDirectory } from "@/queries/directories";
import { searchTag } from "@/queries/tags";

const PICKER_DEBOUNCE_MS = 200;
const PICKER_SUGGESTION_COUNT = 8;

const emptyTexts: Partial<Record<SearchFilterKey, string>> = {
  in: "No folders match",
  size: "Type a size like >10mb, <1gb or 10mb-1gb",
  tag: "No tags match",
};

// What the option lists draw on: the folders and tags an `in:` or `tag:`
// picker offers, looked up by name as the user types, and the message for a
// list that comes up empty.
export const useSearchPickerOptions = ({
  currentDirectory,
  filters,
  today,
  trigger,
}: {
  currentDirectory: () => { id: string; name: string } | null;
  filters: Readonly<Ref<SearchFilter[]>>;
  today: CalendarDate;
  trigger: Readonly<Ref<SearchTrigger | null>>;
}) => {
  const pickerQuery = computed(() => trigger.value?.query.trim() ?? "");
  const debouncedPickerQuery = refDebounced(pickerQuery, PICKER_DEBOUNCE_MS);

  const folderQuery = useQuery(() => ({
    ...searchDirectory({
      isDeleted: false,
      nameContains: debouncedPickerQuery.value || null,
      pageSize: PICKER_SUGGESTION_COUNT,
    }),
    enabled: trigger.value?.key === "in",
    placeholderData: (prev) => prev,
  }));

  const tagQuery = useQuery(() => ({
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
    currentDirectory: currentDirectory(),
    filters: filters.value,
    folders: folderQuery.data.value?.items.map((dir) => ({ id: dir.id, name: dir.name })),
    tags: tagQuery.data.value?.items.map((tag) => ({ id: tag.id, name: tag.name })),
    today,
  }));

  const emptyText = computed(() => {
    const key = trigger.value?.key;
    if (key === "in" && folderQuery.isPending.value) {
      return "Searching folders";
    }
    if (key === "tag" && tagQuery.isPending.value) {
      return "Searching tags";
    }
    if (key && emptyTexts[key]) {
      return emptyTexts[key];
    }
    return "No matching options";
  });

  return { emptyText, filterContext };
};
