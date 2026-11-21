import { useQuery } from "@pinia/colada";
import { type MaybeRefOrGetter, computed, ref, toValue } from "vue";

import type { SearchTagsSchema } from "@/schemas/tag";

import { searchTag } from "@/queries/tags";
import { getIconByValue } from "@/utils/files/icons";

interface TagSearchOptions {
  // Tags already on this file are left out of the results.
  excludeFileId: MaybeRefOrGetter<string>;
  // Search only while a picker is open, so showing a file costs no tag search.
  enabled: MaybeRefOrGetter<boolean>;
}

const PAGE_SIZE = 25;

/** Tags a file could get, as options for a select menu, filtered by what the user types. */
export const useTagSearch = ({ excludeFileId, enabled }: TagSearchOptions) => {
  const searchQuery = ref("");

  const filters = computed<SearchTagsSchema>(() => ({
    excludeOnFile: toValue(excludeFileId),
    nameContains: searchQuery.value || undefined,
    ownerScope: "all",
    page: 1,
    pageSize: PAGE_SIZE,
  }));

  // Tag edits don't invalidate this search, so every opening fetches it fresh; otherwise a tag
  // removed a moment ago would be missing from the choices.
  const { data, isPending: isSearching } = useQuery(() => ({
    ...searchTag(filters.value),
    enabled: toValue(enabled),
    staleTime: 0,
  }));

  const tagOptions = computed(() =>
    (data.value?.items ?? []).map((tag) => ({
      icon: getIconByValue(tag.icon),
      label: tag.name,
      value: tag.id,
    })),
  );

  const resetSearch = () => {
    searchQuery.value = "";
  };

  return { isSearching, resetSearch, searchQuery, tagOptions };
};
